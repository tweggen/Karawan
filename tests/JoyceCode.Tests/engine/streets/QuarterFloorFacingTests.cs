using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using engine.streets;
using engine.world;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * Which way a block's pavement faces, which is what decides whether it is there at all.
 *
 * GlThreeD enables GL_CULL_FACE with FrontFace(Ccw), so a triangle wound the wrong way
 * round is not a shading artefact - it is not drawn. The block floor's top face IS the
 * pavement, so a back-facing cap is a missing sidewalk, with a complete mesh, no
 * exception, and nothing in the log.
 *
 * That is what "I'm only seeing very few sidewalks" was. builtin.tools.Triangulate.ToMesh
 * was handed Vector3.Zero for the tessellation plane by the one ExtrudePoly caller that
 * did not want per vertex normals - the two were a single parameter - so LibTess derived
 * the projection plane from the polygon, and for a block outline that is no longer planar
 * (which is every block on a hillside since the kerb fix) the derivation flips. Measured
 * over the generated cities: 211 of 445 block floors in the 3000 m city came out facing
 * DOWN on a 5.8 % plane, and 8 of 445 did so even on flat ground.
 *
 * These tests run against real generated cities rather than a fixture ring, because a
 * fixture ring is convex and planar and the flip needs neither.
 */
public class QuarterFloorFacingTests
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
     * The floor mesh of one block, with the cap as a plain fan over the outline.
     *
     * ExtrudePoly is what the operator uses, and it is used here rather than
     * re-implemented: the winding this test is about is decided inside it, so a
     * re-implementation would be testing the test.
     *
     * Deliberately WITHOUT the pavement inset. That is still what a flat city gets, and
     * what any block gets whose outline is too sharp or too narrow to carry one, so the
     * plain fan is a shipping surface and not a legacy one. The inset floor's own facing is
     * measured over the same cities in PavementCrossFallTests, which also builds the kerb
     * sides - they come from the outline alone and the inset does not touch them.
     */
    private static global::engine.joyce.Mesh _floorOf(Quarter q)
    {
        var edges = GenerateClusterQuartersOperator.FloorOutlineOf(q, 0f, 0f);
        var path = new List<Vector3> { new(0f, MetaGen.QuarterSidewalkOffset, 0f) };

        var mesh = new global::engine.joyce.Mesh($"floor");
        new global::builtin.tools.ExtrudePoly(edges, path, 27, 10000f, false, false, true)
            .BuildGeom(mesh);

        return mesh;
    }


    /**
     * Triangles of a mesh, as (a, b, c) in emission order.
     */
    private static IEnumerable<(Vector3 a, Vector3 b, Vector3 c)> _triangles(
        global::engine.joyce.Mesh m)
    {
        for (int i = 0; i + 2 < m.Indices.Count; i += 3)
        {
            yield return (m.Vertices[(int)m.Indices[i]],
                m.Vertices[(int)m.Indices[i + 1]],
                m.Vertices[(int)m.Indices[i + 2]]);
        }
    }


    public static IEnumerable<object[]> Cities()
    {
        foreach (var (idString, size) in new[]
                 {
                     ("seed000", 500f), ("Yelukhdidru", 800f),
                     ("seed000", 1500f), ("Yelukhdidru", 3000f)
                 })
        {
            yield return new object[] { idString, size };
        }
    }


    /**
     * Every triangle of every block's pavement points upward, on every baseline city, over
     * ground that is flat, planar and rough.
     *
     * The flat case is in here on purpose and is not redundant: 8 of the 3000 m city's 445
     * blocks were back-facing on flat ground too, so this had been shipping - as eight
     * missing pavements in a city nobody counted - long before any terrain followed.
     *
     * A triangle whose vertices are at three different heights still has to face up: the
     * outline is deliberately non-planar (QuarterFloorTests), so "up" here means the
     * geometric normal has a positive Y and not that the face is level.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void EveryBlocksPavementFacesUpward(string idString, float size)
    {
        foreach (var (tname, fHeight) in new (string, Func<float, float, float>)[]
                 {
                     ("flat", null),
                     ("a 5.8 % plane", (x, z) => 20f + 0.058f * x),
                     ("rolling ground", (x, z)
                         => 20f + 25f * Single.Sin(x / 220f) + 20f * Single.Cos(z / 190f)),
                 })
        {
            var (_, quarters) = _city(idString, size, fHeight);

            int nBlocks = 0, nTriangles = 0;

            foreach (var q in quarters.GetQuarters())
            {
                if (q.GetDelims().Count < 3) continue;

                var mesh = _floorOf(q);

                /*
                 * The cap is the last (n - 2) triangles of the mesh - ExtrudePoly emits the
                 * sides first. Picked by height rather than by counting, so that a change to
                 * how many rows the sides need cannot quietly make this test look at the
                 * kerb instead: a cap triangle is the one whose three vertices are all on
                 * the raised ring.
                 */
                float top = mesh.Vertices.Max(v => v.Y);
                int nCap = 0;

                foreach (var (a, b, c) in _triangles(mesh))
                {
                    bool isCap = _isTop(a, mesh) && _isTop(b, mesh) && _isTop(c, mesh);
                    if (!isCap) continue;

                    Vector3 n = Vector3.Cross(b - a, c - a);
                    Assert.True(n.Y > 0f,
                        $"{idString}/{size} on {tname}: a pavement triangle of the block at "
                        + $"{q.GetCenterPoint()} faces {(n.Y < 0f ? "down" : "edge on")} "
                        + "and is culled away");
                    ++nCap;
                    ++nTriangles;
                }

                Assert.True(nCap >= q.GetDelims().Count - 2,
                    $"only {nCap} pavement triangles for a block with "
                    + $"{q.GetDelims().Count} corners");
                ++nBlocks;
            }

            Assert.True(nBlocks > 0);
            Assert.True(nTriangles > nBlocks,
                "no city here has a block with more than three corners, so this proves "
                + "nothing about a real outline");
        }
    }


    /**
     * A vertex is on the raised ring - i.e. part of the pavement surface rather than the
     * bottom of the kerb.
     *
     * The extrusion is QuarterSidewalkOffset tall and the outline spans metres of height
     * across a block, so "the highest vertex" is not a usable test; what separates the two
     * rings is that each outline corner appears twice, once at the road's height and once
     * exactly QuarterSidewalkOffset above it.
     */
    private static bool _isTop(in Vector3 v, global::engine.joyce.Mesh mesh)
    {
        foreach (var w in mesh.Vertices)
        {
            if (Single.Abs(w.X - v.X) < 1e-3f
                && Single.Abs(w.Z - v.Z) < 1e-3f
                && Single.Abs((v.Y - w.Y) - MetaGen.QuarterSidewalkOffset) < 1e-3f)
            {
                return true;
            }
        }

        return false;
    }


    /**
     * The kerb faces out of the block, on flat ground and on a slope alike.
     *
     * Checked because the pavement being right says nothing about the sides: they are
     * wound from the ring's index order and never go near the tessellator, so they are
     * the control. If they ever disagreed with the cap the block would be inside out
     * rather than merely missing a lid.
     *
     * The block outline is convex nowhere, so "outward" is tested against the nearest
     * outline EDGE's own outward direction rather than against the centroid, which a
     * concave block would fail for reasons that have nothing to do with winding.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheKerbFacesOutOfTheBlock(string idString, float size)
    {
        foreach (var fHeight in new Func<float, float, float>[]
                 {
                     null,
                     (x, z) => 20f + 0.058f * x
                 })
        {
            var (_, quarters) = _city(idString, size, fHeight);
            int nSides = 0;

            foreach (var q in quarters.GetQuarters())
            {
                var delims = q.GetDelims();
                int n = delims.Count;
                if (n < 3) continue;

                var mesh = _floorOf(q);

                foreach (var (a, b, c) in _triangles(mesh))
                {
                    if (_isTop(a, mesh) && _isTop(b, mesh) && _isTop(c, mesh)) continue;

                    Vector3 nrm = Vector3.Cross(b - a, c - a);
                    if (nrm.LengthSquared() < 1e-8f) continue;

                    /*
                     * Which outline edge is this side on? The one whose midpoint is
                     * nearest the triangle's own centre in plan.
                     */
                    Vector3 mid = (a + b + c) / 3f;
                    Vector2 pm = new(mid.X, mid.Z);

                    int best = -1;
                    float bestD = Single.MaxValue;
                    for (int i = 0; i < n; ++i)
                    {
                        Vector2 e0 = delims[i].StartPoint, e1 = delims[(i + 1) % n].StartPoint;
                        float d = (pm - 0.5f * (e0 + e1)).LengthSquared();
                        if (d < bestD) { bestD = d; best = i; }
                    }

                    Vector2 p0 = delims[best].StartPoint, p1 = delims[(best + 1) % n].StartPoint;
                    Vector2 along = p1 - p0;

                    /*
                     * The rings are traced clockwise in plan (measured: every ring of every
                     * baseline city), so the outward side of an edge is to its left in XZ.
                     */
                    Vector2 outward = new(-along.Y, along.X);

                    Assert.True(Vector2.Dot(new Vector2(nrm.X, nrm.Z), outward) > 0f,
                        $"{idString}/{size}: a kerb face of the block at "
                        + $"{q.GetCenterPoint()} faces into the block");
                    ++nSides;
                }
            }

            Assert.True(nSides > 0);
        }
    }


    /**
     * With no inset, the pavement is exactly the block's outline, raised - no vertex moved,
     * none invented, none dropped.
     *
     * This is what makes the facing fix a facing fix. Naming the tessellation plane does
     * reorder the cap's vertices for the blocks that used to come out backwards, so
     * "identical mesh" is not true and is not claimed; what is true, and is what the flat
     * city's invariant actually needs, is that the SET of emitted positions is the outline
     * raised by the kerb, before and after, on flat ground and on a slope.
     *
     * Together with QuarterFloorTests.AFlatCitysFloorIsUnchanged - which pins the outline
     * itself at AverageHeight + ClusterStreetHeight over whole flat cities - that says no
     * flat city geometry has moved anywhere.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void ThePavementIsTheOutlineRaisedByExactlyOneKerb(string idString, float size)
    {
        foreach (var fHeight in new Func<float, float, float>[]
                 {
                     null,
                     (x, z) => 20f + 0.058f * x
                 })
        {
            var (_, quarters) = _city(idString, size, fHeight);
            int nBlocks = 0;

            foreach (var q in quarters.GetQuarters())
            {
                var outline = GenerateClusterQuartersOperator.FloorOutlineOf(q, 0f, 0f);
                if (outline.Count < 3) continue;

                var mesh = _floorOf(q);
                var wanted = outline
                    .Select(v => v + new Vector3(0f, MetaGen.QuarterSidewalkOffset, 0f))
                    .ToList();

                var cap = mesh.Vertices.Where(v => _isTop(v, mesh)).ToList();

                foreach (var w in wanted)
                {
                    Assert.Contains(cap, v => (v - w).Length() < 1e-3f);
                }

                foreach (var v in cap)
                {
                    Assert.Contains(wanted, w => (v - w).Length() < 1e-3f);
                }

                ++nBlocks;
            }

            Assert.True(nBlocks > 0);
        }
    }


    /**
     * Every block is claimed by exactly one fragment.
     *
     * The operator emits a block's floor from the fragment its CENTRE falls in, and
     * Fragment.IsInsideLocal is half open, so the fragments partition the plane and no
     * block can be dropped by its own fragment and its neighbour both - or drawn twice.
     * Measured here over whole generated cities rather than argued from the comparison
     * operators, because a block is placed by its AABB centre and a block's AABB is not
     * something a reader of that method can see.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void EveryBlockIsClaimedByExactlyOneFragment(string idString, float size)
    {
        var (cd, quarters) = _city(idString, size, (x, z) => 20f + 0.058f * x);
        cd.Pos = new Vector3(10000f, 0f, -4000f);

        int nBlocks = 0;
        var fragments = new HashSet<(int, int)>();

        foreach (var q in quarters.GetQuarters())
        {
            Vector2 centre = q.GetCenterPoint() + new Vector2(cd.Pos.X, cd.Pos.Z);

            int claims = 0;
            (int, int) claimant = default;

            /*
             * Every fragment the block could possibly belong to, and one ring beyond, so
             * that a half open comparison written the other way round shows up as two
             * claims rather than as nothing at all.
             */
            int i0 = (int)Single.Floor((centre.X - MetaGen.FragmentSize)
                                       / MetaGen.FragmentSize);
            int k0 = (int)Single.Floor((centre.Y - MetaGen.FragmentSize)
                                       / MetaGen.FragmentSize);

            for (int i = i0; i <= i0 + 3; ++i)
            for (int k = k0; k <= k0 + 3; ++k)
            {
                Vector3 fragPos = new(i * MetaGen.FragmentSize, 0f, k * MetaGen.FragmentSize);

                /*
                 * The operator's own test, called and not restated - a copy of the
                 * comparison here would pass whatever the comparison became.
                 */
                if (!Fragment.PartitionContains(
                        new Vector3(centre.X, 0f, centre.Y) - fragPos))
                {
                    continue;
                }

                ++claims;
                claimant = (i, k);
            }

            Assert.Equal(1, claims);
            fragments.Add(claimant);
            ++nBlocks;
        }

        Assert.True(nBlocks > 0);
    }


    /**
     * The fragments partition the plane: a point on a shared edge belongs to exactly one.
     *
     * Separate from the city-wide test above, and it is the one with teeth. Widening
     * Fragment.PartitionContains to be closed on both sides SURVIVES that test - no
     * generated block's centre lands exactly on a 400 m boundary, so a rule that would
     * draw such a block twice is invisible to any amount of real data. The boundary has to
     * be asked about directly.
     */
    [Fact]
    public void TheFragmentsPartitionThePlane()
    {
        float fs = MetaGen.FragmentSize;
        float fsh = fs / 2f;

        /*
         * Positions on and around a shared edge, in world space, and how many of the
         * fragments around them claim each.
         */
        foreach (var world in new[]
                 {
                     new Vector3(fsh, 0f, 0f),           // on the +x edge of fragment 0
                     new Vector3(-fsh, 0f, 0f),          // on its -x edge
                     new Vector3(0f, 0f, fsh),
                     new Vector3(0f, 0f, -fsh),
                     new Vector3(fsh, 0f, fsh),          // the shared corner of four
                     new Vector3(fsh, 0f, -fsh),
                     new Vector3(0f, 0f, 0f),            // and one plainly inside
                 })
        {
            int claims = 0;

            for (int i = -2; i <= 2; ++i)
            for (int k = -2; k <= 2; ++k)
            {
                if (Fragment.PartitionContains(world - new Vector3(i * fs, 0f, k * fs)))
                {
                    ++claims;
                }
            }

            Assert.True(1 == claims,
                $"{claims} fragments claim {world}; the fragments no longer partition the "
                + "plane, so a block centred there is drawn "
                + (claims > 1 ? "more than once" : "by nobody"));
        }
    }


    /**
     * A block floor is drawn as far away as the loader is willing to keep its fragment.
     *
     * This was 400 m - shorter than the diagonal of a fragment - while the roads on the
     * same ground were emitted at 100000 m and the terrain under them at 3000 m, so the
     * street grid ran to the horizon over blocks with no pavement on them. Half of "very
     * few sidewalks" was that; the other half is the facing above.
     *
     * The bound is recomputed here from the loader's own reach rather than compared with
     * the operator's expression, so that changing one of the two does not silently change
     * both. DrawInstancesSystem measures from the camera to the instance's origin, which
     * for a fragment's static geometry is the fragment's own position.
     */
    [Fact]
    public void BlockFloorsAreDrawnAsFarAsTheirFragmentIsLoaded()
    {
        int n = global::engine.world.PlayerViewer.LoadNSurroundingFragments;

        /*
         * Worst case: the camera at the corner of its own fragment, the fragment at the
         * opposite corner of what the loader keeps.
         */
        float dx = (n + 0.5f) * MetaGen.FragmentSize;
        float furthest = Single.Sqrt(dx * dx + dx * dx);

        Assert.True(GenerateClusterQuartersOperator.MaxDrawDistance >= furthest,
            $"block floors are culled at {GenerateClusterQuartersOperator.MaxDrawDistance} m "
            + $"while the loader keeps fragments up to {furthest} m away, so the outer "
            + "fragments show roads on ground with no pavements");
    }


    /**
     * A block that fails to build says so.
     *
     * Every exception around the floor's geometry and physics used to be swallowed into a
     * Trace, and Trace is filtered off by default - so a block that produced nothing
     * produced no evidence either. CLAUDE.md records a whole investigation derailed by
     * exactly that. Error and Warning are never filtered, which is what makes them the
     * right level here: how much detail to keep is a per category decision, whether to
     * report a problem is not.
     *
     * A source scan, because reaching those catches needs a fragment, a physics world and
     * a failure to arrange inside them.
     */
    [Fact]
    public void ABlockThatFailsToBuildIsReported()
    {
        string path = global::engine.GameRoot.PathTo("JoyceCode")
                      + "/engine/streets/GenerateClusterQuartersOperator.cs";
        Assert.True(File.Exists(path), $"could not find the operator at {path}");

        string[] lines = File.ReadAllLines(path);
        int nCatches = 0;

        for (int i = 0; i < lines.Length; ++i)
        {
            if (!lines[i].Contains("catch (Exception")) continue;

            ++nCatches;

            /*
             * The handler's body, by brace count rather than by a line budget - a comment
             * explaining the level is exactly the kind of thing that pushes the call past
             * a fixed window and turns this into a test that passes for the wrong reason.
             */
            bool reports = false;
            int depth = 0;
            bool opened = false;

            for (int j = i; j < lines.Length; ++j)
            {
                foreach (char ch in lines[j])
                {
                    if ('{' == ch) { ++depth; opened = true; }
                    else if ('}' == ch) --depth;
                }

                if (lines[j].Contains("Error(") || lines[j].Contains("Warning("))
                {
                    reports = true;
                }

                if (lines[j].Contains("Trace("))
                {
                    Assert.Fail(
                        $"{path}:{j + 1} swallows an exception into a Trace, which is "
                        + "filtered off by default - a block that fails to build would "
                        + "vanish with nothing in the log");
                }

                if (opened && 0 == depth) break;
            }

            Assert.True(reports,
                $"{path}:{i + 1} catches an exception and reports nothing");
        }

        Assert.True(nCatches >= 3,
            $"expected the three swallowing catches to still be here, found {nCatches}");
    }
}
