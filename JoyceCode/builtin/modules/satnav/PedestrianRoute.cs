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


    /**
     * The waypoint at one END of a route: the walker's own position, or the destination,
     * carrying the height of the lane it stands beside.
     *
     * "The terrain has to answer here, since there is no road node to ask" is what the
     * comment on this said, and it is not true - there IS a road node to ask, and the route
     * has already found it. TryCreateCursor snaps each end of the route to its nearest
     * lane, and that lane's two junctions are street nodes carrying exact ground heights.
     * So the ends come off the same source every waypoint between them comes off, and the
     * terrain leaves the walker entirely.
     *
     * That matters because the terrain is not the pavement. Measured over every block edge
     * of the four baseline cities on the shipped terrain, the CONFORMED terrain plus the
     * walking offset sits between 5.5 m below the block floor and 6.3 m above it, and below
     * it on 43 to 51 % of edges. Taken off the lane instead the same measurement is 0.00 m
     * at every percentile from p05 to p95, because a sidewalk lane runs between two block
     * corners at exactly their two heights and the pavement rim is level across its width
     * (§7k) - so the lane's own linear interpolation IS the pavement's ground height there.
     *
     * The plan position is the caller's, not the lane's: this end of the route is wherever
     * the walker or the destination actually is. Only the height comes from the lane, and
     * only along it - so a destination beside the middle of a lane gets the middle of the
     * lane's height rather than one of its ends.
     */
    public static Vector3 EndWaypointFor(NavLane lane, in Vector3 v3Position)
    {
        if (null == lane || null == lane.Start || null == lane.End)
        {
            return v3Position;
        }

        Vector3 v3A = lane.Start.Position;
        Vector3 v3B = lane.End.Position;

        Vector2 a = new(v3A.X, v3A.Z);
        Vector2 ab = new(v3B.X - v3A.X, v3B.Z - v3A.Z);

        float l2 = Vector2.Dot(ab, ab);
        float t = l2 > 1e-6f
            ? float.Clamp(Vector2.Dot(new Vector2(v3Position.X, v3Position.Z) - a, ab) / l2, 0f, 1f)
            : 0f;

        return v3Position with
        {
            Y = desc.NavJunction.WalkingHeightOf(
                float.Lerp(lane.Start.GroundHeight, lane.End.GroundHeight, t))
        };
    }
}
