using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using builtin.tools;
using engine.streets;
using engine.streets.generation;
using engine.world;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * How steep a block's pavement is ACROSS its width.
 *
 * The player's report was that "sidewalks shall be up/downwards only in the direction of
 * walking, not in the direction to the street". They were not: the block floor was a
 * single triangle fan over the boundary ring, spanning kerb to kerb with no interior
 * vertices at all, so a block whose corners are at different heights is a warped quad and
 * which way each triangle tilts is decided by the tessellator's sweep. Measured here within
 * a pavement's own width over the generated cities on rolling ground, the fan fell 7.5 %
 * ACROSS at the median against an along-edge slope of 7.0 % - the surface tilted diagonally
 * at about 45 degrees to the street - with a p95 of 16 % and a worst edge of 63 %. A real
 * footway is built at 2 %. With the per-edge inset it is 0.0 % at every percentile, on all
 * 2823 measured edges, because the rim is level by construction rather than by tuning.
 *
 * These tests measure the cap's OWN triangles, barycentrically, on real generated cities -
 * not the ring it was built from. That distinction is the whole point: the ring was never
 * in doubt, the surface spanned across it was.
 */
public class PavementCrossFallTests
{
    private static (ClusterDesc, QuarterStore) _city(
        string idString, float size, Func<float, float, float> fHeight)
    {
        var cd = StreetHarness.MakeCluster(idString, size);
        cd.AverageHeight = 20f;
        cd.StreetHeightSource = null == fHeight
            ? new FlatStreetHeight(cd)
            : new FuncStreetHeight(fHeight);

        var store = StreetHarness.Generate(idString, size);

        return (cd, StreetHarness.GenerateQuarters(cd, store, idString));
    }


    /**
     * The four baselines this work stream measures on, from CITY-3D-OPEN-POINTS - 659
     * blocks and 3547 boundary edges between them.
     */
    public static IEnumerable<object[]> Cities()
    {
        foreach (var (idString, size) in new[]
                 {
                     ("seed000", 800f), ("Yelukhdidru", 1500f),
                     ("seed000", 1500f), ("Yelukhdidru", 3000f)
                 })
        {
            yield return new object[] { idString, size };
        }
    }


    /**
     * A block's floor, built through the shipping path - the operator's own outline, the
     * operator's own inset, ExtrudePoly.
     */
    internal static global::engine.joyce.Mesh FloorOf(Quarter q)
    {
        var edges = GenerateClusterQuartersOperator.FloorOutlineOf(q, 0f, 0f);
        var path = new List<Vector3> { new(0f, MetaGen.QuarterSidewalkOffset, 0f) };

        var mesh = new global::engine.joyce.Mesh("floor");
        new ExtrudePoly(edges, path, 27, 10000f, false, false, true)
        {
            CapInsetEdges = GenerateClusterQuartersOperator.PavementInsetOf(q, edges)
        }.BuildGeom(mesh);

        return mesh;
    }


    /**
     * The triangles of the pavement surface, i.e. the cap rather than the kerb.
     *
     * Selected by height: every cap vertex is exactly QuarterSidewalkOffset above the
     * outline the floor was built from, and every kerb vertex is either on the outline or
     * on its raised copy. So a triangle is on the cap when all three of its vertices are at
     * the raised level for their own plan position, which is a property of the surface and
     * not of how many triangles the sides happen to need.
     */
    private static List<(Vector3 a, Vector3 b, Vector3 c)> _capTriangles(
        global::engine.joyce.Mesh m, IList<Vector3> outline, IList<CapInsetEdge> inset)
    {
        float up = MetaGen.QuarterSidewalkOffset;

        var wanted = new List<Vector3>();
        foreach (var v in outline) wanted.Add(v + new Vector3(0f, up, 0f));
        if (null != inset)
        {
            foreach (var e in inset)
            {
                wanted.Add(e.Start + new Vector3(0f, up, 0f));
                wanted.Add(e.End + new Vector3(0f, up, 0f));
            }
        }

        bool IsCap(Vector3 v) => wanted.Any(w => (w - v).Length() < 1e-3f);

        var tris = new List<(Vector3, Vector3, Vector3)>();
        for (int i = 0; i + 2 < m.Indices.Count; i += 3)
        {
            Vector3 a = m.Vertices[(int)m.Indices[i]];
            Vector3 b = m.Vertices[(int)m.Indices[i + 1]];
            Vector3 c = m.Vertices[(int)m.Indices[i + 2]];

            if (IsCap(a) && IsCap(b) && IsCap(c))
            {
                tris.Add((a, b, c));
            }
        }

        return tris;
    }


    /**
     * The height of the pavement surface at a plan position, read off the cap's own
     * triangles, or null where the position is not on the cap at all.
     */
    private static float? _surfaceAt(
        List<(Vector3 a, Vector3 b, Vector3 c)> tris, Vector2 p)
    {
        foreach (var (a, b, c) in tris)
        {
            Vector2 pa = new(a.X, a.Z), pb = new(b.X, b.Z), pc = new(c.X, c.Z);

            float d = (pb.Y - pc.Y) * (pa.X - pc.X) + (pc.X - pb.X) * (pa.Y - pc.Y);
            if (Single.Abs(d) < 1e-9f) continue;

            float l1 = ((pb.Y - pc.Y) * (p.X - pc.X) + (pc.X - pb.X) * (p.Y - pc.Y)) / d;
            float l2 = ((pc.Y - pa.Y) * (p.X - pc.X) + (pa.X - pc.X) * (p.Y - pc.Y)) / d;
            float l3 = 1f - l1 - l2;

            if (l1 < -1e-4f || l2 < -1e-4f || l3 < -1e-4f) continue;

            return l1 * a.Y + l2 * b.Y + l3 * c.Y;
        }

        return null;
    }


    /**
     * The pavement is level across its width, everywhere a block could be inset.
     *
     * Measured the way the report was: from the midpoint of every boundary edge, step
     * inward along the edge's own perpendicular and read the cap's triangles at both ends
     * of the step. Two step lengths, both inside the rim: a quarter and three quarters of
     * the block's own pavement width.
     *
     * The assertion is absolute rather than a percentage because the rim is level EXACTLY,
     * not approximately: all four vertices of a rim quad carry the height its edge has
     * directly across from them, so they lie on the plane h = h0 + s*x and the surface has
     * no cross-gradient at all. A millimetre is single-precision noise on coordinates of a
     * couple of thousand metres.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void ThePavementIsLevelAcrossItsWidth(string idString, float size)
    {
        foreach (var (tname, fHeight) in _slopes())
        {
            var (_, quarters) = _city(idString, size, fHeight);

            int nEdges = 0, nInset = 0;
            float worst = 0f;

            foreach (var q in quarters.GetQuarters())
            {
                var outline = GenerateClusterQuartersOperator.FloorOutlineOf(q, 0f, 0f);
                if (outline.Count < 3) continue;

                var inset = GenerateClusterQuartersOperator.PavementInsetOf(q, outline);
                if (null == inset) continue;

                ++nInset;
                var tris = _capTriangles(FloorOf(q), outline, inset);
                int n = outline.Count;
                float w = q.SidewalkWidth;

                for (int i = 0; i < n; ++i)
                {
                    Vector2 o0 = new(outline[i].X, outline[i].Z);
                    Vector2 o1 = new(outline[(i + 1) % n].X, outline[(i + 1) % n].Z);

                    Vector2 along = o1 - o0;
                    if (along.Length() < 4f * w) continue;

                    /*
                     * The edge's own perpendicular, turned toward whichever side the inset
                     * is on - which is the direction "across the pavement" means.
                     */
                    Vector2 u = Vector2.Normalize(along);
                    Vector2 perp = new(-u.Y, u.X);
                    Vector2 inward = Vector2.Dot(
                        perp, new Vector2(inset[i].Start.X, inset[i].Start.Z) - o0) > 0f
                        ? perp
                        : -perp;

                    Vector2 mid = 0.5f * (o0 + o1);

                    float? hNear = _surfaceAt(tris, mid + 0.25f * w * inward);
                    float? hFar = _surfaceAt(tris, mid + 0.75f * w * inward);
                    if (!hNear.HasValue || !hFar.HasValue) continue;

                    float drop = Single.Abs(hFar.Value - hNear.Value);
                    worst = Single.Max(worst, drop);
                    ++nEdges;

                    Assert.True(drop < 1e-3f,
                        $"{idString}/{size} on {tname}: the pavement of the block at "
                        + $"{q.GetCenterPoint()} falls {drop:F3} m across half of its "
                        + $"{w} m width");
                }
            }

            Assert.True(nInset > 0, $"no block of {idString}/{size} on {tname} was inset");
            Assert.True(nEdges > 8,
                $"only {nEdges} edges of {idString}/{size} on {tname} were long enough to "
                + "measure, which proves too little");
        }
    }


    /**
     * ...and it was NOT level across before, on the same edges, by a lot.
     *
     * Without this the test above is satisfied by any surface at all that happens to be
     * flat where it is sampled - including, for instance, one that has collapsed to a
     * single height. The plain fan is built here from the same outline and measured at the
     * same points, so the comparison is between two surfaces over one ring.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void ThePlainFanWasSteeperSidewaysThanAlong(string idString, float size)
    {
        var (_, quarters) = _city(idString, size,
            (x, z) => 20f + 25f * Single.Sin(x / 220f) + 20f * Single.Cos(z / 190f));

        var crossFalls = new List<float>();

        foreach (var q in quarters.GetQuarters())
        {
            var outline = GenerateClusterQuartersOperator.FloorOutlineOf(q, 0f, 0f);
            if (outline.Count < 3) continue;
            if (null == GenerateClusterQuartersOperator.PavementInsetOf(q, outline)) continue;

            var path = new List<Vector3> { new(0f, MetaGen.QuarterSidewalkOffset, 0f) };
            var fan = new global::engine.joyce.Mesh("fan");
            new ExtrudePoly(outline, path, 27, 10000f, false, false, true).BuildGeom(fan);

            var tris = _capTriangles(fan, outline, null);
            int n = outline.Count;
            float w = q.SidewalkWidth;

            for (int i = 0; i < n; ++i)
            {
                Vector2 o0 = new(outline[i].X, outline[i].Z);
                Vector2 o1 = new(outline[(i + 1) % n].X, outline[(i + 1) % n].Z);
                Vector2 along = o1 - o0;
                if (along.Length() < 4f * w) continue;

                Vector2 nrm = Vector2.Normalize(new Vector2(-along.Y, along.X));
                Vector2 mid = 0.5f * (o0 + o1);

                /*
                 * Whichever perpendicular points into the block.
                 */
                foreach (float s in new[] { 1f, -1f })
                {
                    float? hNear = _surfaceAt(tris, mid + s * 0.25f * w * nrm);
                    float? hFar = _surfaceAt(tris, mid + s * 0.75f * w * nrm);
                    if (!hNear.HasValue || !hFar.HasValue) continue;

                    crossFalls.Add(Single.Abs(hFar.Value - hNear.Value) / (0.5f * w));
                    break;
                }
            }
        }

        Assert.True(crossFalls.Count > 8, $"only {crossFalls.Count} edges measured");

        crossFalls.Sort();
        float median = crossFalls[crossFalls.Count / 2];

        Assert.True(median > 0.03f,
            $"the plain fan's median cross-fall over {idString}/{size} came out at "
            + $"{median * 100f:F1} %, so this baseline no longer describes the defect the "
            + "inset was built to remove and the test above proves nothing");
    }


    /**
     * Every triangle of the pavement still points upward, rim and interior alike.
     *
     * GlThreeD culls back faces, so a rim quad wound the other way round is not a shading
     * artefact - it is a missing pavement with a complete mesh, no exception and nothing in
     * the log, which is exactly how half a hillside city's block floors went missing once
     * already. The rim's winding is derived from the ring's own signed area about the cap
     * plane rather than assumed, and this is what says the derivation is right on real
     * blocks rather than on a square.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void EveryPavementTriangleFacesUpward(string idString, float size)
    {
        foreach (var (tname, fHeight) in _slopes())
        {
            var (_, quarters) = _city(idString, size, fHeight);
            int nTriangles = 0;

            foreach (var q in quarters.GetQuarters())
            {
                var outline = GenerateClusterQuartersOperator.FloorOutlineOf(q, 0f, 0f);
                if (outline.Count < 3) continue;

                var inset = GenerateClusterQuartersOperator.PavementInsetOf(q, outline);

                foreach (var (a, b, c) in _capTriangles(FloorOf(q), outline, inset))
                {
                    Vector3 nrm = Vector3.Cross(b - a, c - a);
                    if (nrm.LengthSquared() < 1e-12f) continue;

                    Assert.True(nrm.Y > 0f,
                        $"{idString}/{size} on {tname}: a pavement triangle of the block at "
                        + $"{q.GetCenterPoint()} faces down and is culled away");
                    ++nTriangles;
                }
            }

            Assert.True(nTriangles > 0);
        }
    }


    /**
     * The pavement covers the block: no crack between the rim and the interior, and no
     * part of the block left uncovered.
     *
     * The rim and the interior are two separately emitted surfaces meeting along the inner
     * ring, which is the one place this construction could leave a gap the player would see
     * straight through. Checked by sampling the cap at points spread over the block rather
     * than by comparing vertex lists, since a crack is a hole in the SURFACE.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void ThePavementHasNoCrackAlongTheInnerRing(string idString, float size)
    {
        var (_, quarters) = _city(idString, size, (x, z) => 20f + 0.058f * x);
        int nSamples = 0;

        foreach (var q in quarters.GetQuarters())
        {
            var outline = GenerateClusterQuartersOperator.FloorOutlineOf(q, 0f, 0f);
            if (outline.Count < 3) continue;

            var inset = GenerateClusterQuartersOperator.PavementInsetOf(q, outline);
            if (null == inset) continue;

            var tris = _capTriangles(FloorOf(q), outline, inset);
            int n = outline.Count;

            for (int i = 0; i < n; ++i)
            {
                /*
                 * Straddle the inner ring: a little outside it, on it, and a little inside.
                 */
                Vector2 o = 0.5f * (new Vector2(outline[i].X, outline[i].Z)
                                   + new Vector2(outline[(i + 1) % n].X, outline[(i + 1) % n].Z));
                Vector2 d = 0.5f * (new Vector2(inset[i].Start.X, inset[i].Start.Z)
                                    + new Vector2(inset[i].End.X, inset[i].End.Z)) - o;

                foreach (float t in new[] { 0.9f, 1.0f, 1.1f })
                {
                    Vector2 p = o + t * d;
                    Assert.True(_surfaceAt(tris, p).HasValue,
                        $"{idString}/{size}: the pavement of the block at "
                        + $"{q.GetCenterPoint()} has a hole at {p}, {t:F1} of the way "
                        + "across its width");
                    ++nSamples;
                }
            }
        }

        Assert.True(nSamples > 100);
    }


    /**
     * A flat city gets no inset at all, and its floor is the mesh it always was.
     *
     * Vertex for vertex and index for index against the same block built without one. This
     * whole line of work is gated on the shipped flat city not moving, and an inset ring
     * would add vertices to every block floor in it for no benefit whatsoever - every
     * corner of a flat block is at the same height, so there is no cross-fall to remove.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void AFlatCitysFloorIsUntouched(string idString, float size)
    {
        var (_, quarters) = _city(idString, size, null);
        int nBlocks = 0;

        foreach (var q in quarters.GetQuarters())
        {
            var outline = GenerateClusterQuartersOperator.FloorOutlineOf(q, 0f, 0f);
            if (outline.Count < 3) continue;

            Assert.Null(GenerateClusterQuartersOperator.PavementInsetOf(q, outline));

            var path = new List<Vector3> { new(0f, MetaGen.QuarterSidewalkOffset, 0f) };
            var before = new global::engine.joyce.Mesh("before");
            new ExtrudePoly(outline, path, 27, 10000f, false, false, true).BuildGeom(before);

            var after = FloorOf(q);

            Assert.Equal(before.Vertices.Count, after.Vertices.Count);
            Assert.Equal(before.Indices.Count, after.Indices.Count);

            for (int i = 0; i < before.Vertices.Count; ++i)
            {
                Assert.Equal(before.Vertices[i], after.Vertices[i]);
            }

            for (int i = 0; i < before.Indices.Count; ++i)
            {
                Assert.Equal(before.Indices[i], after.Indices[i]);
            }

            ++nBlocks;
        }

        Assert.True(nBlocks > 0);
    }


    /**
     * Most blocks of a hillside city actually get an inset.
     *
     * InsetOf refuses rather than repairs - a mitre that shoots past the opposite side, a
     * ring that folds through itself - and a refusal is silent by design, so without a
     * number here the whole change could quietly be doing nothing at all. Recorded as a
     * floor rather than an equality because which blocks refuse depends on the terrain.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void MostBlocksCarryAPavement(string idString, float size)
    {
        var (_, quarters) = _city(idString, size, (x, z) => 20f + 0.058f * x);

        int nBlocks = 0, nInset = 0;

        foreach (var q in quarters.GetQuarters())
        {
            var outline = GenerateClusterQuartersOperator.FloorOutlineOf(q, 0f, 0f);
            if (outline.Count < 3) continue;

            ++nBlocks;
            if (null != GenerateClusterQuartersOperator.PavementInsetOf(q, outline))
            {
                ++nInset;
            }
        }

        Assert.True(nInset > 0.6f * nBlocks,
            $"only {nInset} of {nBlocks} blocks of {idString}/{size} could be inset");
    }


    /**
     * Every inset point is inside its block, one pavement width in from the kerb.
     *
     * The direction of the offset is derived from the ring's own signed area, so getting it
     * backwards produces a perfectly well-formed ring OUTSIDE the block - a pavement drawn
     * down the middle of the road. That is the mutation this exists for; it is not
     * something the level-across test above would notice, since an outset rim is just as
     * level as an inset one.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void EveryInsetPointIsInsideItsBlockAtTheRightDistance(string idString, float size)
    {
        var (_, quarters) = _city(idString, size, (x, z) => 20f + 0.058f * x);
        int nPoints = 0;

        foreach (var q in quarters.GetQuarters())
        {
            var outline = GenerateClusterQuartersOperator.FloorOutlineOf(q, 0f, 0f);
            if (outline.Count < 3) continue;

            var inset = GenerateClusterQuartersOperator.PavementInsetOf(q, outline);
            if (null == inset) continue;

            int n = outline.Count;
            float w = q.SidewalkWidth;

            for (int i = 0; i < n; ++i)
            {
                foreach (var v in new[] { inset[i].Start, inset[i].End })
                {
                    Assert.True(_containsInPlan(outline, new Vector2(v.X, v.Z)),
                        $"{idString}/{size}: an inset point of the block at "
                        + $"{q.GetCenterPoint()} is outside the block, i.e. in the road");
                    ++nPoints;
                }

                /*
                 * Leave is one width from the edge it leaves along, measured to the LINE
                 * that edge lies on - which is what "one pavement width in from the kerb"
                 * means for a point that may sit beyond the edge's end.
                 */
                Vector2 o0 = new(outline[i].X, outline[i].Z);
                Vector2 o1 = new(outline[(i + 1) % n].X, outline[(i + 1) % n].Z);
                Vector2 dir = Vector2.Normalize(o1 - o0);
                Vector2 leave = new(inset[i].Start.X, inset[i].Start.Z);

                float across = Single.Abs(
                    (leave.X - o0.X) * dir.Y - (leave.Y - o0.Y) * dir.X);

                /*
                 * Exactly the width: the inset point is its own edge's perpendicular offset
                 * and nothing else pulls it about.
                 */
                Assert.True(across > w - 1e-2f,
                    $"{idString}/{size}: an inset point of the block at "
                    + $"{q.GetCenterPoint()} is only {across:F2} m from a {w} m kerb");
            }
        }

        Assert.True(nPoints > 20);
    }


    /**
     * Each inset point carries the height of the outer vertex it belongs to.
     *
     * This is the construction itself, stated where a change to InsetOf has to face it:
     * interpolating a height, or taking the pad's value there, or averaging the two
     * neighbours all produce a plausible ring and put the cross-fall straight back.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void AnInsetPointTakesItsOwnEdgesHeightAcrossFromIt(string idString, float size)
    {
        var (_, quarters) = _city(idString, size,
            (x, z) => 20f + 25f * Single.Sin(x / 220f) + 20f * Single.Cos(z / 190f));

        int nChecked = 0, nDistinct = 0;

        foreach (var q in quarters.GetQuarters())
        {
            var outline = GenerateClusterQuartersOperator.FloorOutlineOf(q, 0f, 0f);
            if (outline.Count < 3) continue;

            var inset = GenerateClusterQuartersOperator.PavementInsetOf(q, outline);
            if (null == inset) continue;

            int n = outline.Count;

            for (int i = 0; i < n; ++i)
            {
                Vector3 a = outline[i], b = outline[(i + 1) % n];
                Vector2 pa = new(a.X, a.Z), pb = new(b.X, b.Z);
                float l = (pb - pa).Length();

                foreach (var v in new[] { inset[i].Start, inset[i].End })
                {
                    /*
                     * Project the inset point back onto its own edge and ask the edge what
                     * height it has there. That is the condition for level across, stated
                     * directly rather than through the surface it produces.
                     */
                    float t = Vector2.Dot(new Vector2(v.X, v.Z) - pa, (pb - pa) / l) / l;

                    Assert.True(Single.Abs((a.Y + t * (b.Y - a.Y)) - v.Y) < 1e-2f,
                        $"{idString}/{size}: an inset point of the block at "
                        + $"{q.GetCenterPoint()} is at {v.Y} where its edge is at "
                        + $"{a.Y + t * (b.Y - a.Y)}");
                    ++nChecked;
                }

                if (Single.Abs(a.Y - b.Y) > 1f) ++nDistinct;
            }
        }

        Assert.True(nChecked > 20);

        /*
         * ...over edges whose two ends really are at different heights, or the equality
         * above holds for reasons that have nothing to do with the rule.
         */
        Assert.True(nDistinct > 4, $"only {nDistinct} edges had any fall along them");
    }


    /**
     * The pavement the floor leaves and the pavement the building footprint leaves are one
     * number.
     *
     * QuarterGenerator computed a sidewalk width, used it to inset the estate, and threw it
     * away; the floor now insets its cap by the same width, and if the two ever drifted
     * apart the pavement and the building wall would stop meeting all the way round every
     * block. A source scan, because what has to hold is that there is only ONE place the
     * number comes from - a second correct copy would pass any test of the value.
     */
    [Fact]
    public void OnlyOnePlaceDecidesHowWideAPavementIs()
    {
        string path = global::engine.GameRoot.PathTo("JoyceCode")
                      + "/engine/streets/QuarterGenerator.cs";
        Assert.True(File.Exists(path), $"could not find the quarter generator at {path}");

        string source = File.ReadAllText(path);

        Assert.Contains("quarter.SidewalkWidth", source);
        Assert.DoesNotContain("sidewalkWidth = 1f", source);
        Assert.DoesNotContain("sidewalkWidth = 2f", source);
        Assert.DoesNotContain("sidewalkWidth = 4f", source);
        Assert.DoesNotContain("sidewalkWidth = 6f", source);
    }


    private static (string, Func<float, float, float>)[] _slopes()
        => new (string, Func<float, float, float>)[]
        {
            ("a 5.8 % plane", (x, z) => 20f + 0.058f * x),
            ("rolling ground", (x, z)
                => 20f + 25f * Single.Sin(x / 220f) + 20f * Single.Cos(z / 190f)),
        };


    private static bool _containsInPlan(IList<Vector3> poly, Vector2 p)
    {
        int n = poly.Count;
        bool inside = false;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector2 a = new(poly[i].X, poly[i].Z), b = new(poly[j].X, poly[j].Z);
            if (a.Y > p.Y != b.Y > p.Y
                && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
