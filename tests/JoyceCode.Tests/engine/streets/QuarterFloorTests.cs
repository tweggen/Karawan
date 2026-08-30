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
     * Every corner of every block is a section point of the junction the delimiter names
     * as its corner, and of no other.
     *
     * This is the claim the whole fix rests on, so it is asserted over whole generated
     * cities rather than argued: QuarterGenerator writes StreetPoint = the junction the
     * edge leaves and the corner = a section point of the junction it arrives at, and the
     * two are a whole street apart.
     */
    [Theory]
    [InlineData("seed000", 500f)]
    [InlineData("Yelukhdidru", 800f)]
    [InlineData("seed000", 1500f)]
    public void ACornerIsASectionPointOfItsOwnJunctionAndNoOther(string idString, float size)
    {
        var (_, _, quarters) = _city(idString, size, (x, z) => 20f + 0.01f * x);

        int nCorners = 0;
        float ownDistanceMax = 0f;
        float otherDistanceMin = Single.MaxValue;

        static bool InSection(StreetPoint sp, Vector2 p)
            => sp.GetSectionArray().Any(s => (s - p).LengthSquared() < 1e-4f);

        foreach (var q in quarters.GetQuarters())
        {
            foreach (var d in q.GetDelims())
            {
                Assert.True(InSection(d.CornerStreetPoint, d.StartPoint),
                    $"corner {d.StartPoint} is not a section point of the junction it names");
                Assert.False(InSection(d.StreetPoint, d.StartPoint),
                    $"corner {d.StartPoint} is also a section point of the delimiter's own "
                    + "StreetPoint - this city cannot tell the two apart, pick another");

                ownDistanceMax = Single.Max(
                    ownDistanceMax, (d.StartPoint - d.CornerStreetPoint.Pos).Length());
                otherDistanceMin = Single.Min(
                    otherDistanceMin, (d.StartPoint - d.StreetPoint.Pos).Length());
                ++nCorners;
            }
        }

        Assert.True(nCorners > 0);

        /*
         * A corner sits about half a carriageway from its own junction and a whole street
         * from the delimiter's. Measured over the baselines: 7 to 12 m at the median
         * against 70 to 97 m, so the two never overlap.
         */
        Assert.True(otherDistanceMin > ownDistanceMax,
            $"nearest wrong junction {otherDistanceMin:F1} m, furthest right one "
            + $"{ownDistanceMax:F1} m - they overlap, so this test proves nothing");
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
                float road = _roadSurfaceAt(cd.StreetHeightSource, delims[i].CornerStreetPoint);

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
     * Nothing in the engine takes a height from a delimiter's own StreetPoint.
     *
     * There is exactly one right pairing and the wrong one compiles, leaves a flat city
     * bit for bit identical, keeps the plan geometry and the routing exactly right, and
     * takes its height from a junction at the far end of a whole street. Both sites in the
     * engine were of that shape before this - the block pad and the sidewalk nav junction
     * - so the rule is worth policing rather than remembering.
     */
    [Fact]
    public void NoHeightIsTakenFromADelimitersOwnStreetPoint()
    {
        string root = global::engine.GameRoot.PathTo("JoyceCode");
        Assert.False(String.IsNullOrEmpty(root), "could not locate the checkout");

        var pattern = new System.Text.RegularExpressions.Regex(
            @"GroundHeightAt\(\s*[A-Za-z_][A-Za-z0-9_\[\]\.]*\.StreetPoint\s*\)");

        var offenders = System.IO.Directory
            .EnumerateFiles(root, "*.cs", System.IO.SearchOption.AllDirectories)
            .Where(f => pattern.IsMatch(System.IO.File.ReadAllText(f)))
            .Select(f => System.IO.Path.GetRelativePath(root, f).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        Assert.True(0 == offenders.Count,
            "these take a block corner's height from the delimiter's own StreetPoint, "
            + "which is the junction at the OTHER end of its edge - use CornerStreetPoint:"
            + "\n  " + String.Join("\n  ", offenders));
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

            Assert.Equal(sum / delims.Count, q.GroundHeightAt(centroid), 2);
            ++nBlocks;
        }

        Assert.True(nBlocks > 0);
    }
}
