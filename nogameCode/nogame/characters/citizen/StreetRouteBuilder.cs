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
     * The terrain has to answer here, since there is no road node to ask. Everything in
     * between comes off the lanes instead. Returns zero without a cluster, which is what
     * it always did: a TALE pod with no ClusterDesc has no height to offer either.
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
             * Sampled at the walker's own position rather than taken as the cluster
             * average, or the first segment of every route starts at the wrong height in
             * a city that keeps its terrain. The walker is not standing on a lane, so
             * this end and the destination end are the only two heights that still have
             * to come from the terrain; everything between them comes from the lanes.
             */
            var startSegmentPos = fromPos;
            startSegmentPos.Y = _walkingHeightAt(startPod, fromPos);

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
             * Sampled at the destination, not at the start. A door on the far side of a
             * hill is not at the height the walker set off from.
             */
            var destPos = toPos;
            destPos.Y = _walkingHeightAt(startPod, toPos);

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
