using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using engine.streets;
using engine.streets.generation;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * The seam that lets a city stop being flat.
 *
 * Street geometry used to read ClusterDesc.AverageHeight directly; it now asks an
 * IStreetHeightSource per junction. These tests cover the two things that has to be
 * true: that the flat source reproduces the flat city exactly (so the change is
 * inert until something asks for terrain), and that a non-flat source produces a
 * surface which actually follows it and stays joined to itself.
 */
public class StreetHeightSourceTests
{
    private const float ClusterSize = 1000f;

    private static float _base(global::engine.world.ClusterDesc cd)
        => global::engine.world.MetaGen.CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE;


    /**
     * Two strokes meeting at a shared junction, bent so that a height field varying in
     * both axes gives all three junctions different heights.
     */
    private static (global::engine.world.ClusterDesc Cluster, StrokeStore Store,
                    Stroke First, Stroke Second, StreetPoint Shared) _bentPair(float bend = 50f)
    {
        var clusterDesc = StreetHarness.MakeCluster("heightsource", ClusterSize);
        var store = new StrokeStore(ClusterSize);

        var a = new StreetPoint() { ClusterId = 0 };
        a.SetPos(310f, -140f);
        var b = new StreetPoint() { ClusterId = 0 };
        var c = new StreetPoint() { ClusterId = 0 };

        /*
         * CreateByAngleFrom writes the far point's position, so b and c are placed by
         * these calls rather than by hand.
         */
        var first = Stroke.CreateByAngleFrom(clusterDesc, a, b, 0f, 160f, true, 1.0f);
        store.AddStroke(first);

        var second = Stroke.CreateByAngleFrom(clusterDesc, b, c, bend, 160f, true, 1.0f);
        store.AddStroke(second);

        return (clusterDesc, store, first, second, b);
    }


    /**
     * A source returning a constant must produce exactly the geometry the old code
     * produced from AverageHeight - not merely a similar one.
     *
     * This is what makes the whole seam safe to land: the existing geometry baselines
     * are the flat path, and this says the new mechanism reaches the same place.
     */
    [Fact]
    public void AConstantHeightSourceReproducesTheFlatCityExactly()
    {
        var flat = StreetGeometryHarness.Generate("seed001", 500f);

        var clusterDesc = StreetHarness.MakeCluster("seed001", 500f);
        clusterDesc.StreetHeightSource = new FuncStreetHeight((x, z) => clusterDesc.AverageHeight);
        var explicitly = StreetGeometryHarness.GenerateWith(clusterDesc, "seed001", 500f);

        Assert.Equal(StreetGeometryFingerprint.Of(flat), StreetGeometryFingerprint.Of(explicitly));
    }


    /**
     * Raising the height source by a constant raises the whole city by that constant
     * and changes nothing else. Separates "the source is consulted" from "the source
     * is consulted correctly".
     */
    [Fact]
    public void AnOffsetHeightSourceMovesTheCityAndNothingElse()
    {
        const float offset = 37.5f;

        var flat = StreetGeometryHarness.Generate("seed001", 500f);

        var clusterDesc = StreetHarness.MakeCluster("seed001", 500f);
        clusterDesc.StreetHeightSource = new FuncStreetHeight((x, z) => offset);
        var raised = StreetGeometryHarness.GenerateWith(clusterDesc, "seed001", 500f);

        Assert.Equal(flat.Vertices.Count, raised.Vertices.Count);
        for (int i = 0; i < flat.Vertices.Count; ++i)
        {
            Assert.Equal(flat.Vertices[i].X, raised.Vertices[i].X, 4);
            Assert.Equal(flat.Vertices[i].Z, raised.Vertices[i].Z, 4);
            Assert.Equal(flat.Vertices[i].Y + offset, raised.Vertices[i].Y, 3);
        }
    }


    /**
     * A lone stroke, both ends dead ends. Both junctions are then straight by
     * construction - a dead end's corners come from the street normal and are purely
     * lateral - so the road's climb runs over its whole plan length and the height at
     * any point is unambiguous.
     */
    private static (global::engine.world.ClusterDesc Cluster, StrokeStore Store, Stroke Only)
        _straightStroke()
    {
        var clusterDesc = StreetHarness.MakeCluster("heightsource-straight", ClusterSize);
        var store = new StrokeStore(ClusterSize);

        var a = new StreetPoint() { ClusterId = 0 };
        a.SetPos(310f, -140f);
        var b = new StreetPoint() { ClusterId = 0 };

        var only = Stroke.CreateByAngleFrom(clusterDesc, a, b, 0f, 160f, true, 1.0f);
        store.AddStroke(only);

        return (clusterDesc, store, only);
    }


    /**
     * The surface follows the height field rather than sitting on a plane through it.
     *
     * Asserted against each vertex's own position along its stroke, for the reason
     * RampGeometryTests records: a straight stroke is emitted as a single quad, so
     * "is there a vertex part way up" cannot tell a slope from a step.
     *
     * Deliberately a stroke with two STRAIGHT ends. Where a junction bends, the road is
     * held flat across the junction's footprint and climbs only over the carriageway
     * between them, so "height is linear in plan distance" stops being the right claim -
     * and restating the footprint arithmetic here would only restate the implementation.
     * The bent case is covered by the monotonicity and junction agreement tests instead.
     */
    [Fact]
    public void AStrokeSurfaceFollowsASlopingHeightField()
    {
        /*
         * A constant term as well as a gradient, and both ends away from x = 0. A
         * height field that happens to be zero at the A end cannot tell "the source is
         * consulted" from "the source is ignored" - which is how the first version of
         * this test survived a mutation that dropped the source at A entirely.
         */
        const float gradient = 0.04f;
        const float datum = 120f;

        var (cd, store, only) = _straightStroke();
        cd.StreetHeightSource = new FuncStreetHeight((x, z) => datum + gradient * x);

        var mesh = StreetGeometryHarness.GenerateFor(cd, store, new[] { only });
        Assert.NotEmpty(mesh.Vertices);

        float hA = _base(cd) + datum + gradient * only.A.Pos.X;
        float hB = _base(cd) + datum + gradient * only.B.Pos.X;
        Assert.NotEqual(hA, hB);

        float runX = only.B.Pos.X - only.A.Pos.X;
        foreach (var v in mesh.Vertices)
        {
            float along = Single.Clamp((v.X - only.A.Pos.X) / runX, 0f, 1f);
            Assert.Equal(hA + along * (hB - hA), v.Y, 1);
        }
    }


    /**
     * Over a bend, each SIDE of the road climbs from one junction's height to the other's,
     * never reverses, and never overshoots either end.
     *
     * This is what replaces "linear in plan distance" once a bend is involved: where the
     * rise happens is the implementation's business, but that it happens once, in one
     * direction, between exactly those two heights, is not.
     *
     * **Superseded, per side.** It used to read "over a bend, the road still climbs ...",
     * sorting EVERY vertex by axial distance and requiring one monotone sequence, on the
     * grounds that the road was held flat over each junction's footprint and climbed over a
     * single window between them. The two sides of a bend do not span the same window - at a
     * 50 degree bend one junction's corners project to 0.79 and 1.21 of the stroke length -
     * and a road that climbs over one window meets the kerb chord of neither block beside
     * it. Each side now climbs between its own two section points, so the surface is warped
     * and a single sequence over both sides is no longer the right claim: it fell back by
     * 0.04 m here, purely from interleaving the two. Within one side nothing is weakened.
     */
    [Fact]
    public void HeightRisesMonotonicallyAlongEachSideOfABentStroke()
    {
        const float gradient = 0.04f;
        const float datum = 120f;

        var (cd, store, first, _, _) = _bentPair();
        cd.StreetHeightSource = new FuncStreetHeight((x, z) => datum + gradient * x);

        var mesh = StreetGeometryHarness.GenerateFor(cd, store, new[] { first });
        Assert.NotEmpty(mesh.Vertices);

        float hA = _base(cd) + datum + gradient * first.A.Pos.X;
        float hB = _base(cd) + datum + gradient * first.B.Pos.X;
        Assert.True(hB > hA);

        foreach (float side in new[] { -1f, 1f })
        {
            /*
             * Axial distance along the stroke, which for this stroke is simply x, and the
             * lateral offset, which is simply z about the centre line.
             */
            var byDistance = mesh.Vertices
                .Where(v => side * (v.Z - first.A.Pos.Y) > 0f)
                .OrderBy(v => v.X)
                .ToList();

            Assert.True(byDistance.Count >= 2,
                $"the side at {side} carries {byDistance.Count} vertices");

            Assert.Equal(hA, byDistance.First().Y, 2);
            Assert.Equal(hB, byDistance.Last().Y, 2);

            float previous = Single.NegativeInfinity;
            foreach (var v in byDistance)
            {
                Assert.InRange(v.Y, hA - 0.001f, hB + 0.001f);
                Assert.True(v.Y >= previous - 0.001f,
                    $"height must not fall back along the road: {v.Y} after {previous}");
                previous = v.Y;
            }
        }
    }


    /**
     * The road is flat where it meets a junction, which is the property that lets it
     * meet a flat cap at all.
     *
     * Stated as: the vertices nearest the junction centre in plan all sit at that
     * junction's height. Nearest in PLAN and by the junction's own height, so this does
     * not need to know how wide the footprint is or where the road decides to start
     * climbing.
     */
    [Fact]
    public void TheRoadIsFlatWhereItMeetsABentJunction()
    {
        const float gradient = 0.04f;
        const float datum = 120f;

        var (cd, store, first, _, shared) = _bentPair();
        cd.StreetHeightSource = new FuncStreetHeight((x, z) => datum + gradient * x);

        var mesh = StreetGeometryHarness.GenerateFor(cd, store, new[] { first });

        float hShared = _base(cd) + datum + gradient * shared.Pos.X;

        /*
         * The corners of a bent junction straddle its centre - one before it, one after -
         * so "the two nearest the centre" picks up both sides of the footprint.
         */
        var nearest = mesh.Vertices
            .OrderBy(v => new Vector2(v.X - shared.Pos.X, v.Z - shared.Pos.Y).LengthSquared())
            .Take(2)
            .ToList();

        Assert.Equal(2, nearest.Count);
        Assert.All(nearest, v => Assert.Equal(hShared, v.Y, 2));
    }


    /**
     * A climbing surface must not be lit as though it were flat.
     */
    [Fact]
    public void ASlopingSurfaceIsNotLitAsFlat()
    {
        var (cd, store, first, _, _) = _bentPair();
        cd.StreetHeightSource = new FuncStreetHeight((x, z) => 120f + 0.04f * x);

        var mesh = StreetGeometryHarness.GenerateFor(cd, store, new[] { first });

        Assert.NotNull(mesh.Normals);
        Assert.NotEmpty(mesh.Normals);
        Assert.All(mesh.Normals, n =>
            Assert.True(Vector3.Dot(Vector3.Normalize(n), Vector3.UnitY) < 0.9995f,
                $"a sloping road must not carry an up normal, got {n}"));
    }


    /**
     * The normal must describe the surface it sits on, not merely lean somewhere.
     *
     * Derived from the emitted vertices rather than recomputed from the stroke: take two
     * vertices on the part that actually climbs, measure the gradient BETWEEN them, and
     * require the normal to be perpendicular to that. "Is it non-vertical" passes happily
     * while the normal is tilted by the wrong amount - which it was, when the slope was
     * taken over the full plan length while the road climbed over the shorter run
     * between the two junction footprints.
     *
     * **Superseded, per side, for the reason the monotonicity test above was.** It used to
     * take the lowest and highest climbing vertex of the WHOLE stroke and require every
     * normal perpendicular to that one gradient. The two sides of a bend climb over
     * different runs, so they are at different angles and there is no single gradient for
     * both: measured across them the mixture came out at 8.9 % against the 13.1 % each side
     * actually carries. Measured within one side, the requirement is what it was.
     */
    [Fact]
    public void TheSlopeNormalMatchesTheGradientOfTheSurface()
    {
        /*
         * A shallow bend and a gentle gradient cannot see this. The quantity under test
         * is the DIFFERENCE between the plan length and the run that climbs, and at a 50
         * degree bend those differ by under a percent - small enough that a normal
         * computed from the wrong one still looks perpendicular. A 15 degree bend pulls
         * one junction corner back to 0.86 of the length, and a steep grade turns that
         * into an angle a test can see.
         */
        const float gradient = 0.15f;
        const float datum = 120f;

        var (cd, store, first, _, _) = _bentPair(15f);
        cd.StreetHeightSource = new FuncStreetHeight((x, z) => datum + gradient * x);

        var mesh = StreetGeometryHarness.GenerateFor(cd, store, new[] { first });

        float hA = _base(cd) + datum + gradient * first.A.Pos.X;
        float hB = _base(cd) + datum + gradient * first.B.Pos.X;

        int nSides = 0;

        foreach (float side in new[] { -1f, 1f })
        {
            /*
             * Strictly between the two junction heights, so both are on the carriageway
             * rather than on a flat footprint, and on one side of the centre line, so the
             * gradient measured is one surface's rather than a mixture of two.
             */
            var climbing = Enumerable.Range(0, mesh.Vertices.Count)
                .Where(i => mesh.Vertices[i].Y > hA + 0.01f && mesh.Vertices[i].Y < hB - 0.01f)
                .Where(i => side * (mesh.Vertices[i].Z - first.A.Pos.Y) > 0f)
                .ToList();

            if (climbing.Count < 2) continue;

            int lo = climbing.OrderBy(i => mesh.Vertices[i].X).First();
            int hi = climbing.OrderBy(i => mesh.Vertices[i].X).Last();

            float run = mesh.Vertices[hi].X - mesh.Vertices[lo].X;
            float rise = mesh.Vertices[hi].Y - mesh.Vertices[lo].Y;
            if (run <= 1f) continue;

            ++nSides;

            /*
             * This stroke runs along +X, so the surface tangent along the road is simply
             * (run, rise) in the XY plane.
             */
            Vector3 tangent = Vector3.Normalize(new Vector3(run, rise, 0f));

            Assert.All(climbing, i =>
                Assert.True(
                    Math.Abs(Vector3.Dot(Vector3.Normalize(mesh.Normals[i]), tangent)) < 0.001f,
                    $"normal {mesh.Normals[i]} is not perpendicular to the surface "
                    + $"(gradient {rise / run:F4})"));
        }

        Assert.True(nSides > 0, "the road must have vertices part way up");
    }


    /**
     * The junction is one node, so it has one height.
     *
     * Two strokes meeting at a junction share their section points exactly in plan -
     * the corner geometry comes from the same StreetPoint.GetSectionArray. This says
     * they also share it in height, which is the property that keeps a non-planar
     * network from splitting open along its seams.
     *
     * This used to fail by up to 1.8 m on an 8 % grade. _shearOntoSlope heighted every
     * vertex by its projection onto the stroke CENTRELINE, and a junction's corners are
     * not on the centreline - at a 15 degree bend they project to 0.858 and 1.142 of the
     * stroke length, so each stroke read a different height at a corner both of them
     * owned. The pass now holds each junction footprint flat at that junction's height
     * and spreads the rise over the carriageway between them.
     */
    [Fact]
    public void TwoStrokesAgreeOnTheHeightOfTheJunctionTheyShare()
    {
        var (cd, store, first, second, shared) = _bentPair();
        cd.StreetHeightSource = new FuncStreetHeight((x, z) => 120f + 0.05f * x + 0.03f * z);

        var meshFirst = StreetGeometryHarness.GenerateFor(cd, store, new[] { first });
        var meshSecond = StreetGeometryHarness.GenerateFor(cd, store, new[] { second });

        int compared = 0;
        foreach (var v in meshFirst.Vertices)
        {
            foreach (var w in meshSecond.Vertices)
            {
                Vector2 dPlan = new(v.X - w.X, v.Z - w.Z);
                if (dPlan.LengthSquared() > 1e-4f) continue;

                ++compared;
                Assert.True(Math.Abs(v.Y - w.Y) < 0.01f,
                    $"the two strokes disagree at their shared corner {v.X},{v.Z}: "
                    + $"{v.Y} against {w.Y}");
            }
        }

        Assert.True(compared > 0, "the two strokes must share at least one corner");
    }


    /**
     * The junction cap must meet the surfaces that run into it.
     *
     * The cap is a flat fan at the junction's one height, so this holds exactly when the
     * roads arriving are flat over the same footprint - which is what the shear now
     * guarantees. Fixing the cap instead could never have worked: before the change the
     * two roads did not agree with each other at a shared corner, so there was no single
     * height for a cap to adopt there.
     */
    [Fact]
    public void TheJunctionCapMeetsTheStrokesThatEndThere()
    {
        var (cd, store, first, second, shared) = _bentPair();
        cd.StreetHeightSource = new FuncStreetHeight((x, z) => 120f + 0.05f * x + 0.03f * z);

        var meshStrokes = StreetGeometryHarness.GenerateFor(cd, store, new[] { first, second });
        var meshCap = StreetGeometryHarness.GenerateJunctionsFor(cd, store, new[] { shared });

        Assert.NotEmpty(meshCap.Vertices);

        int compared = 0;
        foreach (var v in meshCap.Vertices)
        {
            foreach (var w in meshStrokes.Vertices)
            {
                Vector2 dPlan = new(v.X - w.X, v.Z - w.Z);
                if (dPlan.LengthSquared() > 1e-4f) continue;

                ++compared;
                Assert.True(Math.Abs(v.Y - w.Y) < 0.01f,
                    $"the cap and the road disagree at {v.X},{v.Z}: {v.Y} against {w.Y}");
            }
        }

        Assert.True(compared > 0, "the cap must share corners with the roads");
    }


    /**
     * A junction is asked about from every fragment its strokes reach into, so a source
     * that answers twice must answer the same twice. TerrainStreetHeight caches for
     * exactly this reason; FuncStreetHeight does too, so that a test cannot accidentally
     * describe a network no implementation could produce.
     */
    [Fact]
    public void AHeightSourceIsAskedOncePerJunction()
    {
        int calls = 0;
        var source = new FuncStreetHeight((x, z) =>
        {
            ++calls;
            return 0.05f * x;
        });

        var sp = new StreetPoint() { ClusterId = 0 };
        sp.SetPos(20f, 0f);

        float first = source.GroundHeightAt(sp);
        float second = source.GroundHeightAt(sp);

        Assert.Equal(1, calls);
        Assert.Equal(first, second);
    }


    /**
     * The flag picks the source, and a city is flat unless it says otherwise.
     *
     * Exercised through StreetHeightSources.For rather than by writing the real global
     * setting: that write would leak into every test running beside this one, and a
     * cluster that picked up TerrainStreetHeight in a harness with no elevation cache
     * would fail somewhere with nothing to do with heights.
     */
    [Fact]
    public void TerrainFollowingIsOffUnlessAskedFor()
    {
        var cd = StreetHarness.MakeCluster("selection", ClusterSize);

        Assert.IsType<FlatStreetHeight>(StreetHeightSources.For(cd, false));

        /*
         * Terrain is never used raw: the sample would carry whatever gradients the noise
         * produced. Checking the whole chain rather than just the outer layer, since
         * "relaxed" over the wrong thing would pass a shallower assertion.
         */
        var following = Assert.IsType<RelaxedStreetHeight>(StreetHeightSources.For(cd, true));
        Assert.IsType<TerrainStreetHeight>(following.Base);
    }


    /**
     * The flat source is the shipping behaviour written down, so it must report the
     * cluster average and keep reporting it if the average is computed later - which is
     * what happens in the world, since ClusterBaseElevationOperator sets it well after
     * the descriptor exists.
     */
    [Fact]
    public void TheFlatSourceTracksTheClusterAverage()
    {
        var cd = StreetHarness.MakeCluster("flatsource", ClusterSize);
        var source = new FlatStreetHeight(cd);

        var sp = new StreetPoint() { ClusterId = 0 };
        sp.SetPos(123f, -45f);

        Assert.Equal(0f, source.GroundHeightAt(sp), 4);

        cd.AverageHeight = 88.5f;
        Assert.Equal(88.5f, source.GroundHeightAt(sp), 4);
    }
}
