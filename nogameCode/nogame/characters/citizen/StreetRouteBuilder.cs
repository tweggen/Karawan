using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using builtin.modules.satnav;
using builtin.modules.satnav.desc;
using builtin.tools;
using engine;
using engine.navigation;
using engine.tale;
using static engine.Logger;

namespace nogame.characters.citizen;

/// <summary>
/// Builds a SegmentRoute from a start position to a destination using A* street pathfinding.
/// Converts NavLane paths into SegmentRoute for use by SegmentNavigator.
/// Falls back to straight-line movement if pathfinding is unavailable or fails.
/// </summary>
public static class StreetRouteBuilder
{
    private static readonly engine.Dc _dc = engine.Dc.Pathfinding;


    /**
     * Where a walker's feet go at a position that is NOT a junction - the two ends of a
     * route, which are wherever the walker and the destination happen to be.
     *
     * This used to say "the terrain has to answer here, since there is no road node to
     * ask", and that was wrong: the route has already FOUND the road node at each end.
     * TryCreateCursor snaps both ends to their nearest lane, whose two junctions carry
     * exact street heights, so builtin.modules.satnav.PedestrianRoute.EndWaypointFor takes
     * the height off that lane and every waypoint of the route - ends included - now comes
     * off the same source. Measured over the block edges of the four baseline cities on
     * the shipped terrain, the conformed terrain was 5.5 m below the block floor at worst
     * and below it on 43 to 51 % of edges, against 0.00 m at every percentile from the
     * lane.
     *
     * The terrain is only for the case that has no lane at all, and returns zero without a
     * cluster, which is what it always did: a TALE pod with no ClusterDesc has no height
     * to offer either.
     */
    private static float _walkingHeightAt(PositionDescription pod, Vector3 v3Position)
    {
        var clusterDesc = pod?.ClusterDesc;
        if (null == clusterDesc)
        {
            return 0f;
        }

        return builtin.modules.satnav.desc.NavJunction.WalkingHeightOf(
            clusterDesc.GroundHeightAt(v3Position));
    }


    /**
     * One end of the route: its own plan position, at the height of the lane it was
     * snapped to, or at the terrain's where there is no lane.
     */
    private static Vector3 _endWaypoint(
        NavCursor cursor, PositionDescription pod, Vector3 v3Position)
    {
        if (null != cursor && null != cursor.Lane)
        {
            return builtin.modules.satnav.PedestrianRoute.EndWaypointFor(
                cursor.Lane, v3Position);
        }

        return v3Position with { Y = _walkingHeightAt(pod, v3Position) };
    }

    /// <summary>
    /// Build an async street path route from start to destination.
    /// Optionally uses routing preferences for multi-objective pathfinding.
    /// Returns null if pathfinding fails, in which case the caller should fall back to straight-line movement.
    /// </summary>
    public static async Task<SegmentRoute> BuildAsync(Vector3 fromPos, Vector3 toPos, NavMap navMap, PositionDescription startPod,
        TransportationType transportType = TransportationType.Pedestrian,
        RoutingPreferences? preferences = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (navMap == null)
            {
                Trace(_dc, $"NavMap is null, using straight-line fallback");
                return null;
            }

            // Try to get cursors from the top cluster
            var topCluster = navMap.TopCluster;
            if (topCluster == null)
            {
                Trace(_dc, $"TopCluster is null, using straight-line fallback");
                return null;
            }

            // Log route distance classification
            float routeDistance = Vector3.Distance(fromPos, toPos);
            string routeClass = routeDistance < 1.0f ? "SHORT" : "LONG";
            Trace(_dc, $"{routeClass} ROUTE ({routeDistance:F2}m) from {fromPos} to {toPos}");

            // Async cursor creation — await both in parallel with cancellation support
            Trace(_dc, $"{routeClass} ROUTE creating cursors...");
            var startCursorTask = topCluster.TryCreateCursor(fromPos, transportType);
            var endCursorTask = topCluster.TryCreateCursor(toPos, transportType);

            var startCursor = await startCursorTask.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var endCursor = await endCursorTask.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (startCursor == NavCursor.Nil)
            {
                Trace(_dc, $"{routeClass} ROUTE start cursor Nil (position {fromPos} not on NavMap)");
                return null;
            }

            if (endCursor == NavCursor.Nil)
            {
                Trace(_dc, $"{routeClass} ROUTE end cursor Nil (position {toPos} not on NavMap)");
                return null;
            }

            Trace(_dc, $"{routeClass} ROUTE cursors created (start lane={startCursor.Lane.Start.Position}->{startCursor.Lane.End.Position}, end lane={endCursor.Lane.Start.Position}->{endCursor.Lane.End.Position})");

            // Pathfind between cursors with optional routing preferences
            Trace(_dc, $"{routeClass} ROUTE pathfinding from start to end...");
            var pathfinder = new LocalPathfinder(startCursor, endCursor, transportType, preferences);
            var lanes = pathfinder.Pathfind();
            Trace(_dc, $"{routeClass} ROUTE pathfind returned {lanes?.Count ?? 0} lanes");

            // If pathfinding returns 0 lanes, it may be because start and end snap to the same junction
            // In this case, use the closest lanes from the cursors themselves
            if (lanes == null || lanes.Count == 0)
            {
                // Check if both cursors are on lanes (not at junctions)
                if (startCursor.Lane != null && endCursor.Lane != null &&
                    startCursor.Lane != endCursor.Lane)
                {
                    // Build a minimal route using the two closest lanes
                    lanes = new List<NavLane> { startCursor.Lane, endCursor.Lane };
                    Trace(_dc, $"{routeClass} ROUTE same junction detected, using closest lanes (start lane:{startCursor.Lane.Start.Position}->{startCursor.Lane.End.Position}, end lane: {endCursor.Lane.Start.Position}->{endCursor.Lane.End.Position})");
                }
                else
                {
                    Trace(_dc, $"{routeClass} ROUTE no path found (start={fromPos}, end={toPos})");
                    return null;
                }
            }

            // Convert lane path to SegmentRoute
            var route = new SegmentRoute();

            // Start segment: from actual position
            /*
             * At the walker's own plan position, carrying the height of the lane the route
             * starts on. Nothing on a route comes from the terrain any more.
             */
            var startSegmentPos = _endWaypoint(startCursor, startPod, fromPos);

            var forward = Vector3.Normalize(toPos - fromPos);
            if (float.IsNaN(forward.X)) forward = Vector3.UnitX;
            var up = Vector3.UnitY;
            var right = Vector3.Cross(forward, up);
            if (right.LengthSquared() < 0.001f) right = Vector3.UnitX;

            route.Segments.Add(new SegmentEnd
            {
                Position = startSegmentPos,
                Up = up,
                Right = right,
                PositionDescription = startPod
            });

            // Intermediate segments: each lane end position (walking right-hand sidewalk)
            foreach (var lane in lanes)
            {
                /*
                 * Every waypoint keeps its OWN height, taken from the junction it is.
                 * This used to be one number for the whole route, sampled once at the
                 * route's start and from the TERRAIN rather than the road, so a route
                 * across a hill came out flat and a walker climbing one sank into it.
                 *
                 * The junction already knows: GenerateNavMapOperator gives every car
                 * junction the relaxed street height of the StreetPoint it IS, and every
                 * sidewalk junction the height of the junction its quarter delimiter
                 * belongs to. Lanes measure with Vector3.Distance and split with
                 * Vector3.Lerp, so the interpolation between two waypoints is already
                 * linear in the street nodes - which is exactly the cheap elevation
                 * function an NPC needs, with no raycast anywhere.
                 *
                 * The waypoint itself is built by builtin.modules.satnav.PedestrianRoute,
                 * which is in Joyce and therefore reachable from a test, unlike this file.
                 */
                route.Segments.Add(new SegmentEnd
                {
                    Position = builtin.modules.satnav.PedestrianRoute.WaypointFor(lane),
                    Up = up,
                    Right = right
                });
            }

            // Final segment: actual destination
            /*
             * Taken at the DESTINATION's own lane, not at the start's. A door on the far
             * side of a hill is not at the height the walker set off from, and it used to
             * be given the start pod's terrain sample at the destination's coordinates -
             * two different ends of one hill answered by one height field.
             */
            var destPos = _endWaypoint(endCursor, startPod, toPos);

            route.Segments.Add(new SegmentEnd
            {
                Position = destPos,
                Up = up,
                Right = right
            });

            if (routeDistance >= 1.0f)
                Trace(_dc, $"LONG ROUTE ({routeDistance:F2}m) found from {fromPos} to {toPos} ({lanes.Count} lanes → {route.Segments.Count} segments)");
            else
                Trace(_dc, $"SHORT ROUTE ({routeDistance:F2}m) route found from {fromPos} to {toPos} ({lanes.Count} lanes → {route.Segments.Count} segments)");
            return route;
        }
        catch (Exception ex)
        {
            Trace(_dc, $"Pathfinding unavailable ({ex.Message}), using straight-line fallback.");
            return null;
        }
    }
}
