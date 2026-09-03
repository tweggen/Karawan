using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using builtin.modules.satnav;
using engine.streets;
using engine.world;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * The kerb: where a block's pavement stands relative to the road beside it.
 *
 * There is no separate sidewalk geometry. GenerateClusterQuartersOperator extrudes the
 * block polygon up by QuarterSidewalkOffset; the top face is the pavement and the sides
 * are the kerb. So the block's outline IS the kerb line, and its height has to be the
 * road's height there or the pavement floats over the carriageway or sinks under it.
 *
 * The mesh emission itself needs a fragment and a physics world and is not exercised.
 * What is exercised is the outline it is built from, against real generated cities.
 */
public class QuarterFloorTests
{
    /**
     * The road surface at a junction, written out here rather than borrowed, so that the
     * floor and the road are compared through two independent expressions.
     */
    private static float _roadSurfaceAt(IStreetHeightSource source, StreetPoint sp)
    {
        /*
         * No deck elevation term, and that is an assumption rather than an omission: a
         * block is traced on the ground only. Asserted rather than assumed, because the
         * floor would silently stay on the ground under a raised junction if a ruleset
         * ever produced one.
         */
        Assert.Equal(0, sp.Level);

        return source.GroundHeightAt(sp) + MetaGen.CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE;
    }


    private static (ClusterDesc, StrokeStore, QuarterStore) _city(
        string idString, float size, Func<float, float, float> fHeight)
    {
        var cd = StreetHarness.MakeCluster(idString, size);
        cd.AverageHeight = 20f;
        cd.StreetHeightSource = null == fHeight
            ? new FlatStreetHeight(cd)
            : new FuncStreetHeight(fHeight);

        var store = StreetHarness.Generate(idString, size);

        return (cd, store, StreetHarness.GenerateQuarters(cd, store, idString));
    }


    /**
     * Every corner of every block is a section point of its own delimiter's junction, and
     * of no neighbouring delimiter's.
     *
     * This is the claim the kerb rests on, so it is asserted over whole generated cities
     * rather than argued. The neighbours are checked too because they are the wrong
     * answers that are actually reachable: the junctions round a block are a ring, so the
     * one before and the one after a corner are what a re-introduced off-by-one would give
     * it - a whole street away.
     */
    [Theory]
    [InlineData("seed000", 500f)]
    [InlineData("Yelukhdidru", 800f)]
    [InlineData("seed000", 1500f)]
    public void ACornerIsASectionPointOfItsOwnJunctionAndNoOther(string idString, float size)
    {
        var (_, _, quarters) = _city(idString, size, (x, z) => 20f + 0.01f * x);

        int nCorners = 0;
        var ownDistances = new List<float>();
        var otherDistances = new List<float>();

        static bool InSection(StreetPoint sp, Vector2 p)
            => sp.GetSectionArray().Any(s => (s - p).LengthSquared() < 1e-4f);

        foreach (var q in quarters.GetQuarters())
        {
            var delims = q.GetDelims();
            int n = delims.Count;
            if (n < 3) continue;

            for (int i = 0; i < n; ++i)
            {
                var d = delims[i];
                var before = delims[(i + n - 1) % n];
                var after = delims[(i + 1) % n];

                Assert.True(InSection(d.StreetPoint, d.StartPoint),
                    $"corner {d.StartPoint} is not a section point of its own junction");
                Assert.False(InSection(before.StreetPoint, d.StartPoint),
                    $"corner {d.StartPoint} is also a section point of the PREVIOUS "
                    + "delimiter's junction - this city cannot tell them apart");
                Assert.False(InSection(after.StreetPoint, d.StartPoint),
                    $"corner {d.StartPoint} is also a section point of the NEXT "
                    + "delimiter's junction - this city cannot tell them apart");

                ownDistances.Add((d.StartPoint - d.StreetPoint.Pos).Length());
                otherDistances.Add(Single.Min(
                    (d.StartPoint - before.StreetPoint.Pos).Length(),
                    (d.StartPoint - after.StreetPoint.Pos).Length()));
                ++nCorners;
            }
        }

        Assert.True(nCorners > 0);

        /*
         * A corner sits about half a carriageway from its own junction and a whole street
         * from its neighbours': 7 to 12 m against 70 to 97 m at the median over the
         * baselines. Compared as medians and not as ranges, because the ranges DO overlap
         * - a short street brings a neighbouring junction to within 25.5 m of a corner
         * whose own junction is 25.7 m away. Which is why the assertions above are on
         * section-point identity and not on distance: nothing metric separates these
         * everywhere.
         */
        static float Median(List<float> xs)
        {
            xs.Sort();
            return xs[xs.Count / 2];
        }

        Assert.True(Median(otherDistances) > 3f * Median(ownDistances),
            $"neighbouring junctions are {Median(otherDistances):F1} m from a corner at "
            + $"the median against {Median(ownDistances):F1} m for its own - too close "
            + "together for this city to be evidence of anything");
    }


    /**
     * The kerb is exactly QuarterSidewalkOffset at every corner of every block, over
     * ground that is not flat.
     *
     * Pinned on the terms and not only on the value: ClusterStreetHeight and
     * CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE are two names for 2.0 that nothing else
     * relates, and the claim "the pavement is one kerb above the road" is only true while
     * they agree.
     */
    [Theory]
    [InlineData("seed000", 500f)]
    [InlineData("Yelukhdidru", 800f)]
    [InlineData("seed000", 1500f)]
    public void ThePavementStandsExactlyOneKerbAboveTheRoad(string idString, float size)
    {
        var (cd, _, quarters) = _city(
            idString, size, (x, z) => 20f + 0.05f * x - 0.03f * z);

        int nCorners = 0;
        var heights = new HashSet<float>();

        foreach (var q in quarters.GetQuarters())
        {
            var outline = GenerateClusterQuartersOperator.FloorOutlineOf(
                q, 0f, 0f);
            var delims = q.GetDelims();
            Assert.Equal(delims.Count, outline.Count);

            for (int i = 0; i < delims.Count; ++i)
            {
                float pavement = outline[i].Y + MetaGen.QuarterSidewalkOffset;
                float road = _roadSurfaceAt(cd.StreetHeightSource, delims[i].StreetPoint);

                Assert.Equal(MetaGen.QuarterSidewalkOffset, pavement - road, 3);

                /*
                 * And the outline stays in plan where the polygon is.
                 */
                Assert.Equal(delims[i].StartPoint.X, outline[i].X, 4);
                Assert.Equal(delims[i].StartPoint.Y, outline[i].Z, 4);

                heights.Add(outline[i].Y);
                ++nCorners;
            }
        }

        Assert.True(nCorners > 0);
        Assert.True(heights.Count > 1,
            "every corner of this city came out at the same height, so a floor at one "
            + "height for the whole city would pass this too");
    }


    /**
     * The block outline follows the cluster into the fragment, and nothing else does.
     */
    [Fact]
    public void TheOutlineIsOffsetByTheClusterOrigin()
    {
        var (_, _, quarters) = _city("seed000", 500f, (x, z) => 20f + 0.05f * x);
        var q = quarters.GetQuarters().First();

        var atOrigin = GenerateClusterQuartersOperator.FloorOutlineOf(q, 0f, 0f);
        var moved = GenerateClusterQuartersOperator.FloorOutlineOf(
            q, 137f, -911f);

        Assert.Equal(atOrigin.Count, moved.Count);
        for (int i = 0; i < atOrigin.Count; ++i)
        {
            Assert.Equal(atOrigin[i].X + 137f, moved[i].X, 4);
            Assert.Equal(atOrigin[i].Z - 911f, moved[i].Z, 4);
            Assert.Equal(atOrigin[i].Y, moved[i].Y, 4);
        }
    }


    /**
     * The flat city is bit for bit what it was: every corner of every block at
     * AverageHeight plus ClusterStreetHeight, the same number the pad used to hand back.
     *
     * Asserted over whole generated cities rather than over a fixture, and with an
     * average that has no exact binary form, because "unchanged" is the claim.
     */
    [Theory]
    [InlineData("seed000", 500f)]
    [InlineData("Yelukhdidru", 800f)]
    [InlineData("seed000", 1500f)]
    public void AFlatCitysFloorIsUnchanged(string idString, float size)
    {
        var (cd, _, quarters) = _city(idString, size, null);
        cd.AverageHeight = 20.1f;

        float expected = 20.1f + MetaGen.ClusterStreetHeight;
        int nCorners = 0;

        foreach (var q in quarters.GetQuarters())
        {
            var outline = GenerateClusterQuartersOperator.FloorOutlineOf(
                q, 0f, 0f);

            foreach (var v in outline)
            {
                Assert.Equal(expected, v.Y);
                ++nCorners;
            }

            /*
             * And what the pad answers, which is what the floor used to read, is the same
             * number - so the two paths cannot have parted company here either.
             */
            foreach (var d in q.GetDelims())
            {
                Assert.Equal(20.1f, q.GroundHeightAt(d.StartPoint));
                Assert.Equal(20.1f, q.CornerGroundHeightAt(d));
            }
        }

        Assert.True(nCorners > 0);
    }


    /**
     * The pavement an NPC walks on is the pavement that is drawn.
     *
     * A sidewalk nav junction stands at a block corner and takes that corner's junction
     * height, so a walker's feet land on the kerb the block's floor emits there:
     * WalkingHeightOf is ClusterStreetHeight plus QuarterSidewalkOffset above the ground,
     * and the floor's top face is exactly that. Two independent expressions, compared.
     */
    [Theory]
    [InlineData("seed000", 500f)]
    [InlineData("Yelukhdidru", 800f)]
    [InlineData("seed000", 1500f)]
    public void AWalkerStandsOnThePavementThatIsDrawn(string idString, float size)
    {
        var (cd, _, quarters) = _city(
            idString, size, (x, z) => 20f + 0.05f * x - 0.03f * z);

        cd.Pos = new Vector3(4000f, 999f, -7000f);
        int nCorners = 0;
        var grounds = new HashSet<float>();

        foreach (var q in quarters.GetQuarters())
        {
            var outline = GenerateClusterQuartersOperator.FloorOutlineOf(q, 0f, 0f);
            var delims = q.GetDelims();

            for (int i = 0; i < delims.Count; ++i)
            {
                var nj = GenerateNavMapOperator.SidewalkJunctionFor(
                    delims[i], cd.Pos, cd.StreetHeightSource);

                /*
                 * In plan the junction is the block's corner, carried into the world.
                 */
                Assert.Equal(cd.Pos.X + delims[i].StartPoint.X, nj.Position.X, 3);
                Assert.Equal(cd.Pos.Z + delims[i].StartPoint.Y, nj.Position.Z, 3);

                /*
                 * The cluster's own Y is not a ground height and must not reach the
                 * junction.
                 */
                Assert.Equal(
                    outline[i].Y + MetaGen.QuarterSidewalkOffset,
                    global::builtin.modules.satnav.desc.NavJunction.WalkingHeightOf(
                        nj.GroundHeight), 3);

                grounds.Add(nj.GroundHeight);
                ++nCorners;
            }
        }

        Assert.True(nCorners > 0);
        Assert.True(grounds.Count > 1,
            "every sidewalk junction of this city came out on the same ground");
    }


    /**
     * The pad sits on the corners of a real block, and on its OWN corners.
     *
     * The pad is fitted to (corner position, corner height) pairs, so pairing a corner
     * with a neighbouring junction still produces a plane, still leaves the plan geometry
     * exactly right and is still invisible in a flat city - it just tilts the block the
     * wrong way. On a fixture ring that shows up because the fixture says so; here it
     * shows up against generated cities, which is where the pairing actually comes from.
     */
    [Theory]
    [InlineData("seed000", 500f)]
    [InlineData("Yelukhdidru", 800f)]
    [InlineData("seed000", 1500f)]
    public void ThePadSitsOnTheCornersOfARealBlock(string idString, float size)
    {
        var (cd, _, quarters) = _city(
            idString, size, (x, z) => 20f + 0.05f * x - 0.03f * z);

        var own = new List<float>();
        var neighbour = new List<float>();

        foreach (var q in quarters.GetQuarters())
        {
            var delims = q.GetDelims();
            int n = delims.Count;
            if (n < 3) continue;

            for (int i = 0; i < n; ++i)
            {
                float pad = q.GroundHeightAt(delims[i].StartPoint);
                own.Add(Single.Abs(pad - q.CornerGroundHeightAt(delims[i])));
                neighbour.Add(Single.Abs(
                    pad - q.CornerGroundHeightAt(delims[(i + 1) % n])));
            }
        }

        Assert.True(own.Count > 0);

        static float Median(List<float> xs)
        {
            xs.Sort();
            return xs[xs.Count / 2];
        }

        /*
         * Not zero: the corners of a block are not coplanar even over an exactly planar
         * hillside, so the pad answers at a corner with a fit residual - 0.02 m at the
         * median over these cities, which is why the FLOOR takes CornerGroundHeightAt
         * instead. What matters here is that it is a residual and not a whole street's
         * worth of slope.
         */
        Assert.True(Median(own) < 0.25f,
            $"the pad is {Median(own):F2} m off its own corners at the median");
        Assert.True(Median(neighbour) > 4f * Median(own),
            $"the pad is {Median(neighbour):F2} m off the NEXT corner at the median "
            + $"against {Median(own):F2} m off its own - too close together for this to "
            + "be evidence that it took the right one");
    }


    /**
     * What snapping the boundary costs the block's interior, which is the trade this
     * makes: the pad is still what buildings, trees, shop fronts and TALE doors stand on,
     * and it is no longer exactly the floor.
     *
     * At the block's centroid it costs nothing at all, and not approximately nothing. The
     * fit is parametrised about the centroid of the corners, so the plane there IS the
     * mean of the corner heights - and a triangulation of those same corner heights reads
     * the mean there too. So the floor and the pad part company at the kerb, where nothing
     * stands, and coincide in the middle, where everything does.
     */
    [Theory]
    [InlineData("seed000", 500f)]
    [InlineData("Yelukhdidru", 800f)]
    [InlineData("seed000", 1500f)]
    public void ThePadStillAgreesWithTheFloorInTheMiddleOfTheBlock(string idString, float size)
    {
        var (cd, _, quarters) = _city(
            idString, size, (x, z) => 20f + 0.05f * x - 0.03f * z);

        int nBlocks = 0;

        foreach (var q in quarters.GetQuarters())
        {
            var delims = q.GetDelims();
            if (delims.Count < 3) continue;

            Vector2 centroid = Vector2.Zero;
            float sum = 0f;
            foreach (var d in delims)
            {
                centroid += d.StartPoint;
                sum += q.CornerGroundHeightAt(d);
            }

            centroid /= delims.Count;

            /*
             * An absolute bound rather than Assert.Equal(..., 2), which ROUNDS both sides to
             * two decimals and so straddles: a corner moving by 2e-6 m took 18.3149986 and
             * 18.3150005 to different sides of 18.315 and failed a property that is exact.
             * Two decimals is +-0.005, which this asserts directly; the measured worst is
             * 0.0000 m over whole cities.
             */
            Assert.True(Single.Abs(sum / delims.Count - q.GroundHeightAt(centroid)) < 0.005f,
                $"the pad at the centroid is {q.GroundHeightAt(centroid)} against a mean "
                + $"corner height of {sum / delims.Count}");
            ++nBlocks;
        }

        Assert.True(nBlocks > 0);
    }
}
