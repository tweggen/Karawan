using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using builtin.modules.satnav;
using builtin.modules.satnav.desc;
using engine.navigation;
using engine.world;
using Xunit;

namespace JoyceCode.Tests.builtin.modules.satnav;


/**
 * How high an NPC walks.
 *
 * The elevation of an NPC is meant to be a linear function of the street nodes, with no
 * raycast anywhere - and that function already existed and was being thrown away.
 * GenerateNavMapOperator gives every car junction the relaxed street height of the
 * StreetPoint it IS, and every sidewalk junction the height of the junction its quarter
 * delimiter belongs to; lanes measure with Vector3.Distance and split with Vector3.Lerp,
 * so interpolation along a lane is already linear in those nodes. StreetRouteBuilder then
 * gave every waypoint of a route ONE Y, computed once at the route's start and from the
 * TERRAIN rather than from the road, so a route across a hill came out flat.
 *
 * The offsets are the trap and they are why NavJunction carries the GROUND rather than
 * having its position reinterpreted. A junction's Position is at ground plus
 * ClusterNavigationHeight, which is the vehicle hover reference; a walker's feet go at
 * ground plus ClusterStreetHeight plus QuarterSidewalkOffset. Subtracting one constant to
 * add another is how those two silently drift apart.
 *
 * StreetRouteBuilder itself is in nogameCode, which the test assembly cannot reference,
 * so the part of it that decides a waypoint was moved into PedestrianRoute - in Joyce,
 * and exercised here.
 */
public class NavJunctionHeightTests
{
    private static NavLane _lane(NavJunction start, NavJunction end) => new()
    {
        Start = start,
        End = end,
        Length = Vector3.Distance(start.Position, end.Position),
        AllowedTypes = new TransportationTypeFlags(TransportationType.Pedestrian)
    };


    /**
     * The flat city, pinned on the TERM and not merely on the answer.
     *
     * In a flat city every junction stands on ClusterDesc.AverageHeight exactly - the
     * terrain really has been ironed to it - so the lane sits at average + 3 and the old
     * route height was average + ClusterStreetHeight + QuarterSidewalkOffset = average +
     * 2.15. The conversion has to reproduce that number, which makes the whole change
     * inert there. Reusing ClusterNavigationHeight because it is the number that says
     * "how high a nav junction is" would put every NPC 0.85 m into the air.
     */
    [Fact]
    public void AFlatCityWalksAtExactlyTheHeightItAlwaysDid()
    {
        const float average = 41.25f;

        var nj = NavJunction.At(new Vector3(120f, 999f, -300f), average);

        Assert.Equal(average, nj.GroundHeight, 4);

        /* unchanged: what GenerateNavMapOperator has always put in Position */
        Assert.Equal(average + MetaGen.ClusterNavigationHeight, nj.Position.Y, 4);
        Assert.Equal(44.25f, nj.Position.Y, 4);

        /* the old StreetRouteBuilder height, reproduced */
        Assert.Equal(
            average + MetaGen.ClusterStreetHeight + MetaGen.QuarterSidewalkOffset,
            nj.WalkingHeight, 4);
        Assert.Equal(43.40f, nj.WalkingHeight, 4);

        /* the plan position is untouched by either */
        Assert.Equal(120f, nj.Position.X, 4);
        Assert.Equal(-300f, nj.Position.Z, 4);
    }


    /**
     * The two heights are different heights, and by a specific amount. A conversion that
     * merely looked plausible - reusing the navigation clearance, or dropping the kerb -
     * would pass a test that only compared a waypoint against its own junction.
     */
    [Fact]
    public void AWalkerStandsLowerThanAVehicleHoversByTheDifferenceOfTheTwoOffsets()
    {
        const float ground = 7f;

        Assert.Equal(0.85f,
            NavJunction.NavigationHeightOf(ground) - NavJunction.WalkingHeightOf(ground), 4);

        Assert.NotEqual(
            NavJunction.NavigationHeightOf(ground), NavJunction.WalkingHeightOf(ground), 3);
    }


    /**
     * Both conversions are the same conversion, so a junction synthesised at a point
     * already on a lane - Route.cs truncating the last lane at the target - carries a
     * ground height consistent with the ones the lane was built from.
     */
    [Fact]
    public void APointOnALaneCarriesTheGroundUnderIt()
    {
        var nj = NavJunction.AtNavigationHeight(new Vector3(4f, NavJunction.NavigationHeightOf(31f), 9f));

        Assert.Equal(31f, nj.GroundHeight, 4);
        Assert.Equal(NavJunction.WalkingHeightOf(31f), nj.WalkingHeight, 4);
    }


    /**
     * A lane longer than MaxLaneLength is split, and the intermediate junctions are
     * built from an interpolated GROUND height. The position they end up at has to be
     * the one Vector3.Lerp always produced, or splitting a lane would move the road.
     */
    [Theory]
    [InlineData(0f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    public void SplittingALaneMovesNothing(float t)
    {
        var njA = NavJunction.At(new Vector3(0f, 0f, 0f), 10f);
        var njB = NavJunction.At(new Vector3(200f, 0f, 0f), 34f);

        Vector3 wasPosition = Vector3.Lerp(njA.Position, njB.Position, t);

        var njMid = NavJunction.Between(njA, njB, t);

        Assert.Equal(wasPosition.X, njMid.Position.X, 4);
        Assert.Equal(wasPosition.Y, njMid.Position.Y, 4);
        Assert.Equal(wasPosition.Z, njMid.Position.Z, 4);

        /* and the walking height it carries is linear in the two ends' ground */
        Assert.Equal(NavJunction.WalkingHeightOf(10f + t * 24f), njMid.WalkingHeight, 4);
    }


    /**
     * The reported defect: a route across a hill came out flat. Every waypoint now takes
     * the height of the junction it stands on.
     */
    [Fact]
    public void ARouteAcrossAHillIsNotFlat()
    {
        float[] ground = { 10f, 12f, 17f, 25f, 24f };

        var junctions = ground
            .Select((g, i) => NavJunction.At(new Vector3(i * 40f, 0f, 0f), g))
            .ToList();

        var lanes = Enumerable.Range(0, junctions.Count - 1)
            .Select(i => _lane(junctions[i], junctions[i + 1]))
            .ToList();

        var waypoints = lanes.Select(PedestrianRoute.WaypointFor).ToList();

        Assert.Equal(4, waypoints.Count);

        for (int i = 0; i < waypoints.Count; ++i)
        {
            Assert.Equal(
                NavJunction.WalkingHeightOf(ground[i + 1]), waypoints[i].Y, 4);
        }

        Assert.True(waypoints.Select(w => w.Y).Distinct().Count() > 1,
            "the route came out flat, which is the whole defect");

        /*
         * And it climbs and falls with the ground rather than with anything else - a
         * waypoint that took the height of the lane's START would be right about the
         * spread and wrong about which waypoint is where.
         */
        Assert.True(waypoints[2].Y > waypoints[1].Y);
        Assert.True(waypoints[3].Y < waypoints[2].Y);
    }


    /**
     * The same route in a flat city is the route that was always produced: every
     * waypoint at the average plus 2.15, which is what the single terrain-sampled Y used
     * to be.
     */
    [Fact]
    public void TheSameRouteInAFlatCityIsUnchanged()
    {
        const float average = 41.25f;

        var junctions = Enumerable.Range(0, 5)
            .Select(i => NavJunction.At(new Vector3(i * 40f, 0f, 0f), average))
            .ToList();

        var lanes = Enumerable.Range(0, junctions.Count - 1)
            .Select(i => _lane(junctions[i], junctions[i + 1]))
            .ToList();

        foreach (var waypoint in lanes.Select(PedestrianRoute.WaypointFor))
        {
            Assert.Equal(43.40f, waypoint.Y, 4);
        }
    }


    /**
     * The waypoint is still offset onto the sidewalk, by the lane's own kerb side.
     *
     * This test used to assert the offset was a fixed hand relative to travel, which was
     * the shape of the defect PedestrianKerbSideTests describes: sidewalk lanes exist in
     * both directions over the same ground, so a fixed hand put one of every pair in the
     * carriageway. The offset it pins is the same 1.5 m; what has changed is what decides
     * its direction, and a lane that names no side is now walked down its middle.
     */
    [Fact]
    public void TheWaypointStaysOnTheSidewalk()
    {
        var njA = NavJunction.At(new Vector3(0f, 0f, 0f), 5f);
        var njB = NavJunction.At(new Vector3(100f, 0f, 0f), 5f);

        var lane = _lane(njA, njB);
        lane.KerbSide = -Vector3.UnitZ;

        Vector3 waypoint = PedestrianRoute.WaypointFor(lane);

        Assert.Equal(100f, waypoint.X, 4);
        Assert.Equal(-PedestrianRoute.SidewalkOffset, waypoint.Z, 4);
        Assert.Equal(NavJunction.WalkingHeightOf(5f), waypoint.Y, 4);

        /*
         * The reverse lane over the same ground steps to the same side, which is the whole
         * point of the side belonging to the lane.
         */
        var back = _lane(njB, njA);
        back.KerbSide = lane.KerbSide;

        Assert.Equal(-PedestrianRoute.SidewalkOffset, PedestrianRoute.WaypointFor(back).Z, 4);
    }


    /**
     * Position and GroundHeight have to agree, and there are exactly three places that
     * may decide how - At, Between and AtNavigationHeight, all on NavJunction itself.
     *
     * A junction built with an object initialiser sets whichever half its author was
     * thinking about and leaves the other at zero, which is invisible: a junction with
     * the right Position and no GroundHeight renders and routes exactly as before and
     * drops the NPC walking over it to 2.15 m above sea level. Both sites in the engine
     * were of that shape before this change, which is why the rule is worth policing
     * rather than remembering.
     */
    [Fact]
    public void EveryNavJunctionInTheEngineIsBuiltByAFactory()
    {
        string root = global::engine.GameRoot.PathTo("JoyceCode");
        Assert.False(String.IsNullOrEmpty(root), "could not locate the checkout");

        const string declaring = "NavJunction.cs";

        var offenders = Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(f) != declaring)
            .Where(f => File.ReadAllText(f).Contains("new NavJunction"))
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        Assert.True(0 == offenders.Count,
            "these build a NavJunction directly instead of through NavJunction.At / "
            + "Between / AtNavigationHeight, so nothing makes them set GroundHeight:\n  "
            + String.Join("\n  ", offenders));
    }


    /**
     * The two ends of a route are not junctions - they are wherever the walker and the
     * destination happen to be - but they are each beside a LANE, and since §7m that is
     * where their heights come from too. Nothing on a route is a terrain sample any more.
     * StreetRouteBuilder is in nogameCode and cannot be referenced from here, so this is a
     * source scan, as it is for the friction and hover-probe sites.
     */
    [Fact]
    public void TheRouteBuilderTakesEveryWaypointFromItsOwnLane()
    {
        string path = global::engine.GameRoot.PathTo("nogameCode")
                      + "/nogame/characters/citizen/StreetRouteBuilder.cs";

        Assert.True(File.Exists(path), $"could not find the route builder at {path}");

        string source = File.ReadAllText(path);

        Assert.Contains("PedestrianRoute.WaypointFor(lane)", source);

        /*
         * The defect in its original form: one height for the whole route.
         */
        Assert.DoesNotContain("laneEndPos.Y = groundHeight", source);
        Assert.DoesNotContain("destPos.Y = groundHeight", source);

        /*
         * Both ends take their OWN position, and each one's own end of the route. Reusing
         * the start's for the destination is the same defect in miniature.
         *
         * ⚠️ This used to read `_walkingHeightAt(startPod, fromPos)` and
         * `_walkingHeightAt(startPod, toPos)` - the two terrain samples - under the claim
         * above that the ends "are the only two that do" and have to. **That claim is
         * wrong and §7m superseded it**: TryCreateCursor has already snapped each end to
         * its nearest lane, so there IS a road node to ask at both. The terrain expression
         * survives in the file for the case with no lane at all, which is why this is
         * stated on the CALL rather than on the absence of the function.
         */
        Assert.Contains("_endWaypoint(startCursor, startPod, fromPos)", source);
        Assert.Contains("_endWaypoint(endCursor, startPod, toPos)", source);
    }
}
