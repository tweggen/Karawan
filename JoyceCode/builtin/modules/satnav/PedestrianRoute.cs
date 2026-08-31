using System.Numerics;
using builtin.modules.satnav.desc;

namespace builtin.modules.satnav;


/**
 * Turning a path of nav lanes into the waypoints a walker actually walks.
 *
 * The elevation of an NPC is this and nothing else: no raycast, no terrain sample, no
 * per-frame query. A NavJunction is a street node and carries the exact height of the
 * road at that node, and a lane between two of them measures itself with
 * Vector3.Distance and splits itself with Vector3.Lerp - so a walker interpolating from
 * waypoint to waypoint is already interpolating linearly in the street heights, for free.
 *
 * Extracted out of nogame's StreetRouteBuilder because that is where the height was being
 * thrown away: every waypoint of a route used to be given ONE Y, computed once at the
 * route's start and from the terrain rather than from the road, so a route across a hill
 * came out flat. nogameCode has no test harness; this does.
 */
public static class PedestrianRoute
{
    /**
     * How far off the lane centre a walker keeps. A sidewalk lane runs along a block's kerb
     * line, so a walker on the centre line is standing on the kerb itself; this is how far
     * onto the pavement they step.
     */
    public const float SidewalkOffset = 1.5f;


    /**
     * The waypoint at the far end of one lane.
     *
     * The offset is toward the lane's OWN kerb side, not to a fixed hand relative to travel.
     * It used to be 1.5 m to the right of travel unconditionally, and sidewalk lanes are
     * created in both directions over the same ground - so measured over the block edges of
     * the generated cities, one lane of every pair put the walker 1.5 m OUTSIDE the block,
     * in the carriageway at pavement height, whichever way round the A* happened to route.
     * That was true in the flat city too. builtin.tools.QuarterLoopRouteGenerator, the other
     * pedestrian system, offsets the other way and has always been right.
     *
     * A lane with no kerb side - every pedestrian crossing, since a crossing is in the
     * roadway by definition - keeps the centre line.
     */
    public static Vector3 WaypointFor(NavLane lane)
    {
        /*
         * The junction's own height, not the previous waypoint's and not the terrain's.
         * WalkingHeight rather than Position.Y because Position carries the VEHICLE
         * clearance - see NavJunction.
         */
        Vector3 v3End = lane.End.Position with { Y = lane.End.WalkingHeight };

        return v3End + lane.KerbSide * SidewalkOffset;
    }
}
