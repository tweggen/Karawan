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
     * How far right of the lane centre a walker keeps. Lanes are centre lines, and two
     * walkers on the same lane in opposite directions should pass rather than collide.
     */
    public const float SidewalkOffset = 1.5f;


    /**
     * The waypoint at the far end of one lane.
     */
    public static Vector3 WaypointFor(NavLane lane)
    {
        /*
         * The junction's own height, not the previous waypoint's and not the terrain's.
         * WalkingHeight rather than Position.Y because Position carries the VEHICLE
         * clearance - see NavJunction.
         */
        Vector3 v3End = lane.End.Position with { Y = lane.End.WalkingHeight };

        var laneDir = Vector3.Normalize(lane.End.Position - lane.Start.Position);
        var laneRight = Vector3.Cross(laneDir, Vector3.UnitY);
        if (laneRight.LengthSquared() < 0.001f) laneRight = Vector3.UnitX;

        return v3End + laneRight * SidewalkOffset;
    }
}
