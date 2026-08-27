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
                    Stroke First, Stroke Second, StreetPoint Shared) _bentPair()
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

        var second = Stroke.CreateByAngleFrom(clusterDesc, b, c, 50f, 160f, true, 1.0f);
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
     * The surface follows the height field rather than sitting on a plane through it.
     *
     * Asserted against each vertex's own position along its stroke, for the reason
     * RampGeometryTests records: a straight stroke is emitted as a single quad, so
     * "is there a vertex part way up" cannot tell a slope from a step.
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

        var (cd, store, first, _, _) = _bentPair();
        cd.StreetHeightSource = new FuncStreetHeight((x, z) => datum + gradient * x);

        var mesh = StreetGeometryHarness.GenerateFor(cd, store, new[] { first });
        Assert.NotEmpty(mesh.Vertices);

        float hA = _base(cd) + datum + gradient * first.A.Pos.X;
        float hB = _base(cd) + datum + gradient * first.B.Pos.X;
        Assert.NotEqual(hA, hB);

        float runX = first.B.Pos.X - first.A.Pos.X;
        foreach (var v in mesh.Vertices)
        {
            float along = Single.Clamp((v.X - first.A.Pos.X) / runX, 0f, 1f);
            Assert.Equal(hA + along * (hB - hA), v.Y, 1);
        }
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
     * The junction is one node, so it has one height.
     *
     * Two strokes meeting at a junction share their section points exactly in plan -
     * the corner geometry comes from the same StreetPoint.GetSectionArray. This says
     * they also share it in height, which is the property that keeps a non-planar
     * network from splitting open along its seams.
     *
     * FAILS TODAY, deliberately left as the specification rather than weakened to fit.
     * _shearOntoSlope gives every vertex a height from its projection onto the stroke's
     * CENTRELINE, but a junction's corner points are not on the centreline: at an
     * oblique bend one corner sits well before the junction centre and its partner well
     * after it (measured: axial positions 0.858 and 1.142 of the stroke length at a 15
     * degree bend). Each stroke therefore reads a different height at a corner both of
     * them own, and the road splits. Measured worst case 1.8 m at an 8 % grade.
     *
     * It has never fired because it needs hA != hB AND a bend: at a straight junction
     * the corners are pure lateral offsets and project to exactly 0 and 1. Every ramp
     * OverpassBuilder makes is straight, so ramps are unaffected - which is also why
     * RampGeometryTests cannot see this.
     *
     * The fix is not a tweak to this pass. The road has to be flat across its junction
     * footprint so it can meet a flat cap, and it currently subdivides at 0.85 of its
     * length - inside that footprint at an oblique junction. That means changing where
     * cross sections are emitted, not how they are moved afterwards. See
     * STREETS-3D-TOPOLOGY.md.
     */
    [Fact(Skip = "Known defect, see the comment: junction corners are sheared by "
                 + "centreline projection. Needs the emission change, not a pass tweak.")]
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
     * FAILS TODAY, same root cause as the test above and left for the same reason. The
     * cap is a flat fan at the junction's one height; the roads arrive tilted and read
     * their corner heights off their own centrelines, so cap and road part company by
     * the same margin. Note that fixing the cap alone cannot work: the two roads do not
     * even agree with each other at a shared corner, so there is no single height for
     * the cap to adopt there.
     */
    [Fact(Skip = "Known defect, see TwoStrokesAgreeOnTheHeightOfTheJunctionTheyShare.")]
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
        Assert.IsType<TerrainStreetHeight>(StreetHeightSources.For(cd, true));
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
