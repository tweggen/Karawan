using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using builtin.modules.satnav;
using builtin.modules.satnav.desc;
using engine.streets;
using engine.world;
using JoyceCode.Tests.engine.streets;
using Xunit;

namespace JoyceCode.Tests.builtin.modules.satnav;


/**
 * The two ENDS of a walker's route - where the walker is, and where it is going.
 *
 * Every waypoint between them has come off the lanes since §7c, and those are exact. The
 * ends were the two that still asked the terrain, under a comment saying the terrain had to
 * answer "since there is no road node to ask". **There is one, and the route has already
 * found it**: TryCreateCursor snaps each end to its nearest lane, and a sidewalk lane runs
 * between two block corners carrying their two junction heights.
 *
 * The difference is not academic. A city that keeps its terrain grades the ground toward
 * its streets on a 20 m grid with a 60 m smoothstep (§2c), and the median block is 28 m
 * deep to its kerb - so in the middle of a block the ground has only come about half way to
 * the road. Measured over every block edge of the four baseline cities on the shipped
 * terrain, at the point a walker actually stands: the conformed terrain plus the walking
 * offset is 5.5 m below the block floor at worst and below it on 43 to 51 % of edges.
 */
public class PedestrianRouteEndTests
{
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


    private static (ClusterDesc, StrokeStore, QuarterStore) _city(
        string idString, float size, bool isFlat)
    {
        var cd = StreetHarness.MakeCluster(idString, size);
        cd.AverageHeight = 20f;

        var store = StreetHarness.Generate(idString, size);

        cd.StreetHeightSource = isFlat
            ? new FlatStreetHeight(cd)
            : ShippedTerrain.StreetHeightsOf(cd, store);

        return (cd, store, StreetHarness.GenerateQuarters(cd, store, idString));
    }


    /**
     * One sidewalk lane along a block edge, exactly as GenerateNavMapOperator builds it:
     * a junction at each corner carrying that corner's own StreetPoint's ground height.
     */
    private static NavLane _laneOn(Quarter q, int i)
    {
        var delims = q.GetDelims();
        int n = delims.Count;

        Vector2 a = delims[i].StartPoint, b = delims[(i + 1) % n].StartPoint;

        return new NavLane
        {
            Start = NavJunction.At(
                new Vector3(a.X, 0f, a.Y), q.CornerGroundHeightAt(delims[i])),
            End = NavJunction.At(
                new Vector3(b.X, 0f, b.Y), q.CornerGroundHeightAt(delims[(i + 1) % n])),
            KerbSide = GenerateNavMapOperator._inwardOf(
                a, b, GenerateNavMapOperator._isCcwInPlan(delims))
        };
    }


    /**
     * A route end stands on the pavement, and the terrain would not have.
     *
     * Measured at the midpoint of every block edge, one SidewalkOffset in from the kerb -
     * which is where PedestrianRoute puts a walker - against the block floor's own
     * triangles. The terrain is carried alongside as the baseline, so "the end is on the
     * pavement" cannot be satisfied by a measurement too coarse to see the difference.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void ARouteEndStandsOnThePavementAndNotOnTheTerrain(string idString, float size)
    {
        var (cd, store, quarters) = _city(idString, size, false);
        var conformed = ShippedTerrain.ConformedOf(cd, store);

        var lane = new List<float>();
        var terrain = new List<float>();

        foreach (var q in quarters.GetQuarters())
        {
            var delims = q.GetDelims();
            int n = delims.Count;
            if (n < 3) continue;

            var outline = GenerateClusterQuartersOperator.FloorOutlineOf(q, 0f, 0f);
            if (outline.Count < 3) continue;

            var inset = GenerateClusterQuartersOperator.PavementInsetOf(q, outline);

            /*
             * Blocks too narrow to carry a pavement inset are excluded, and that is the
             * limit of the claim rather than a convenience: §7k refuses the inset on 1, 0,
             * 3 and 7 blocks of the four cities, and on those the cap is still the plain
             * warped fan with all of a block's cross-fall in it.
             */
            if (null == inset) continue;

            var tris = BlockFloor.CapOf(outline, inset);
            if (0 == tris.Count) continue;

            for (int i = 0; i < n; ++i)
            {
                NavLane nl = _laneOn(q, i);
                if (nl.KerbSide == Vector3.Zero) continue;

                Vector3 mid = 0.5f * (nl.Start.Position + nl.End.Position)
                              + PedestrianRoute.SidewalkOffset * nl.KerbSide;

                float? h = BlockFloor.SurfaceAt(tris, new Vector2(mid.X, mid.Z));
                if (!h.HasValue) continue;

                Vector3 w = PedestrianRoute.EndWaypointFor(nl, mid);

                /*
                 * The plan position is the caller's. Identity, because the whole point of
                 * this function is that only the HEIGHT comes from the lane.
                 */
                Assert.Equal(mid.X, w.X);
                Assert.Equal(mid.Z, w.Z);

                lane.Add(w.Y - h.Value);
                terrain.Add(NavJunction.WalkingHeightOf(
                                conformed.HeightAt(cd.Pos.X + mid.X, cd.Pos.Z + mid.Z))
                            - h.Value);
            }
        }

        Assert.True(lane.Count > 8, $"only {lane.Count} block edges measured");

        float p05 = BlockFloor.Percentile(lane, 0.05f);
        float p95 = BlockFloor.Percentile(lane, 0.95f);

        Assert.True(p05 > -0.01f && p95 < 0.01f,
            $"{idString}/{size}: a route end taken off its lane is {p05:F3} m below the "
            + $"block floor at p05 and {p95:F3} m above it at p95, over {lane.Count} edges");

        /*
         * ...and the terrain would not have. Stated as a FRACTION rather than as a
         * percentile, because seed000/500 has ten block edges and barely any relief: its
         * terrain is only 0.68 m below the floor at p05, which is still six times the whole
         * tolerance above but would not clear a metre.
         */
        int nOff = terrain.FindAll(x => Single.Abs(x) > 0.25f).Count;

        Assert.True(nOff * 5 >= terrain.Count,
            $"{idString}/{size}: the terrain disagreed with the block floor by more than "
            + $"0.25 m on only {nOff} of {terrain.Count} edges, so this measurement cannot "
            + "distinguish it from the pavement and proves nothing");
    }


    /**
     * The height varies ALONG the lane, and is not one of its ends repeated.
     *
     * "One Y for the whole route" is the defect §7c fixed for the middle of a route; taking
     * a lane's start height for a position at its far end would be the same thing again,
     * one waypoint smaller.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void ARouteEndTakesTheLanesHeightAtItsOwnProjection(string idString, float size)
    {
        var (cd, _, quarters) = _city(idString, size, false);
        int nSloped = 0;

        foreach (var q in quarters.GetQuarters())
        {
            var delims = q.GetDelims();
            int n = delims.Count;
            if (n < 3) continue;

            for (int i = 0; i < n; ++i)
            {
                NavLane nl = _laneOn(q, i);

                float ha = nl.Start.GroundHeight, hb = nl.End.GroundHeight;
                if (Single.Abs(hb - ha) < 1f) continue;

                ++nSloped;

                Assert.Equal(
                    NavJunction.WalkingHeightOf(ha),
                    PedestrianRoute.EndWaypointFor(nl, nl.Start.Position).Y, 3);
                Assert.Equal(
                    NavJunction.WalkingHeightOf(hb),
                    PedestrianRoute.EndWaypointFor(nl, nl.End.Position).Y, 3);
                Assert.True(Single.Abs(
                        NavJunction.WalkingHeightOf(0.5f * (ha + hb))
                        - PedestrianRoute.EndWaypointFor(
                            nl, 0.5f * (nl.Start.Position + nl.End.Position)).Y) < 1e-3f,
                    "the middle of a lane is not the mean of its two ends");

                /*
                 * And a point BEYOND either end is clamped to that end rather than
                 * extrapolated off the road.
                 */
                Assert.Equal(
                    NavJunction.WalkingHeightOf(ha),
                    PedestrianRoute.EndWaypointFor(
                        nl, nl.Start.Position + 4f * (nl.Start.Position - nl.End.Position)).Y,
                    3);
            }
        }

        Assert.True(nSloped > 0,
            $"no lane of {idString}/{size} on the shipped terrain has a metre of fall along "
            + "it, so this proves nothing about a slope");
    }


    /**
     * The default FLAT city, exactly: every route end is at the average plus the walking
     * offset, which is where the terrain expression put it too.
     *
     * So the flat city does not move at all here - the terrain inside a flattened cluster
     * IS the average, and ClusterDesc.GroundHeightAt short circuits to it.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void AFlatCityRouteEndDoesNotMove(string idString, float size)
    {
        var (cd, _, quarters) = _city(idString, size, true);
        int nEnds = 0;

        foreach (var q in quarters.GetQuarters())
        {
            var delims = q.GetDelims();
            int n = delims.Count;
            if (n < 3) continue;

            for (int i = 0; i < n; ++i)
            {
                NavLane nl = _laneOn(q, i);
                Vector3 mid = 0.5f * (nl.Start.Position + nl.End.Position);

                Assert.Equal(
                    NavJunction.WalkingHeightOf(cd.GroundHeightAt(mid)),
                    PedestrianRoute.EndWaypointFor(nl, mid).Y);

                ++nEnds;
            }
        }

        Assert.True(nEnds > 8);
    }


    /**
     * The straight-line fallback's own decision: on this block, or not on it.
     *
     * GoToStrategyPart is in nogameCode, so a scan can see that it NAMES the pavement
     * lookup and cannot see whether the branch that names it is ever taken - writing
     * `if (false)` round that branch passed the whole suite. So the decision itself lives
     * in BuildingFooting.TryPavementHeightAt, where it can be driven over real blocks, and
     * the scan only has to establish that the fallback calls it.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheFallbackAsksTheBlockOnItAndTheTerrainOffIt(string idString, float size)
    {
        var (_, _, quarters) = _city(idString, size, false);
        int nOn = 0, nOff = 0;

        foreach (var q in quarters.GetQuarters())
        {
            var delims = q.GetDelims();
            if (delims.Count < 3) continue;

            Assert.True(global::engine.streets.generation.BuildingFooting.TryPavementHeightAt(
                    q, q.GetCenterPoint(), out float onBlock),
                $"{idString}/{size}: a block does not contain its own centre");

            Assert.Equal(
                global::engine.streets.generation.BuildingFooting.PavementHeightAt(
                    q, q.GetCenterPoint()),
                onBlock);
            ++nOn;

            /*
             * A kilometre away is off every block of every baseline city, and the walker
             * has to fall back to the terrain there rather than be told this block's
             * height at a point it does not cover.
             */
            Assert.False(global::engine.streets.generation.BuildingFooting.TryPavementHeightAt(
                q, q.GetCenterPoint() + new Vector2(4000f, 4000f), out _));
            ++nOff;
        }

        Assert.True(nOn > 0 && nOff > 0);
        Assert.False(global::engine.streets.generation.BuildingFooting.TryPavementHeightAt(
            null, Vector2.Zero, out _));
    }


    /**
     * The route builder and the straight-line fallback ask for the surface, not the terrain.
     *
     * Both live in nogameCode, which this assembly does not reference. Absence as well as
     * presence: the terrain expression still exists in StreetRouteBuilder for the case with
     * no lane at all, so a scan for the new call alone would pass with the old one still
     * wired to the ends.
     */
    [Fact]
    public void TheRouteEndsAndTheFallbackAskForTheSurface()
    {
        string root = global::engine.GameRoot.PathTo("JoyceCode");
        Assert.False(String.IsNullOrEmpty(root), "could not locate the checkout");

        string dir = Path.GetFullPath(Path.Combine(
            root, "..", "nogameCode", "nogame", "characters", "citizen"));

        string builder = File.ReadAllText(Path.Combine(dir, "StreetRouteBuilder.cs"));
        Assert.Contains("PedestrianRoute.EndWaypointFor", builder);
        Assert.Contains("_endWaypoint(startCursor", builder);
        Assert.Contains("_endWaypoint(endCursor", builder);
        Assert.DoesNotContain("startSegmentPos.Y = _walkingHeightAt", builder);
        Assert.DoesNotContain("destPos.Y = _walkingHeightAt", builder);

        string goTo = File.ReadAllText(Path.Combine(dir, "GoToStrategyPart.cs"));
        Assert.Contains("BuildingFooting.TryPavementHeightAt", goTo);
        Assert.DoesNotContain("ClusterDesc.GroundHeightAt(startPos)", goTo);
        Assert.DoesNotContain("ClusterDesc.GroundHeightAt(endPos)", goTo);
    }
}
