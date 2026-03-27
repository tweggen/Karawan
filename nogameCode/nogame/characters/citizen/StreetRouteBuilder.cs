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
                Trace("StreetRouteBuilder: NavMap is null, using straight-line fallback");
                return null;
            }

            // Try to get cursors from the top cluster
            var topCluster = navMap.TopCluster;
            if (topCluster == null)
            {
                Trace("StreetRouteBuilder: TopCluster is null, using straight-line fallback");
                return null;
            }

            // Log route distance classification
            float routeDistance = Vector3.Distance(fromPos, toPos);
            string routeClass = routeDistance < 1.0f ? "SHORT" : "LONG";
            Trace($"StreetRouteBuilder: {routeClass} ROUTE ({routeDistance:F2}m) from {fromPos} to {toPos}");

            // Async cursor creation — await both in parallel with cancellation support
            Trace($"StreetRouteBuilder: {routeClass} ROUTE creating cursors...");
            var startCursorTask = topCluster.TryCreateCursor(fromPos);
            var endCursorTask = topCluster.TryCreateCursor(toPos);

            var startCursor = await startCursorTask.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var endCursor = await endCursorTask.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (startCursor == NavCursor.Nil)
            {
                Trace($"StreetRouteBuilder: {routeClass} ROUTE start cursor Nil (position {fromPos} not on NavMap)");
                return null;
            }

            if (endCursor == NavCursor.Nil)
            {
                Trace($"StreetRouteBuilder: {routeClass} ROUTE end cursor Nil (position {toPos} not on NavMap)");
                return null;
            }

            Trace($"StreetRouteBuilder: {routeClass} ROUTE cursors created (start lane={startCursor.Lane.Start.Position}->{startCursor.Lane.End.Position}, end lane={endCursor.Lane.Start.Position}->{endCursor.Lane.End.Position})");

            // Pathfind between cursors with optional routing preferences
            Trace($"StreetRouteBuilder: {routeClass} ROUTE pathfinding from start to end...");
            var pathfinder = new LocalPathfinder(startCursor, endCursor, preferences, transportType);
            var lanes = pathfinder.Pathfind();
            Trace($"StreetRouteBuilder: {routeClass} ROUTE pathfind returned {lanes?.Count ?? 0} lanes");
            if (lanes == null || lanes.Count == 0)
            {
                Trace($"StreetRouteBuilder: {routeClass} ROUTE no path found (start={fromPos}, end={toPos})");
                return null;
            }

            // Convert lane path to SegmentRoute
            var route = new SegmentRoute();

            // Start segment: from actual position
            float groundHeight = startPod?.ClusterDesc?.AverageHeight ?? 0f;
            if (startPod?.ClusterDesc != null)
            {
                groundHeight += engine.world.MetaGen.ClusterStreetHeight +
                               engine.world.MetaGen.QuarterSidewalkOffset;
            }

            var startSegmentPos = fromPos;
            startSegmentPos.Y = groundHeight;

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
                var laneEndPos = lane.End.Position;
                laneEndPos.Y = groundHeight;

                // Offset to right-hand sidewalk (1.5m right of lane center)
                var laneDir = Vector3.Normalize(lane.End.Position - lane.Start.Position);
                var laneUp = Vector3.UnitY;
                var laneRight = Vector3.Cross(laneDir, laneUp);
                if (laneRight.LengthSquared() < 0.001f) laneRight = Vector3.UnitX;

                var sidewalkPos = laneEndPos + laneRight * 1.5f;

                route.Segments.Add(new SegmentEnd
                {
                    Position = sidewalkPos,
                    Up = up,
                    Right = right
                });
            }

            // Final segment: actual destination
            var destPos = toPos;
            destPos.Y = groundHeight;

            route.Segments.Add(new SegmentEnd
            {
                Position = destPos,
                Up = up,
                Right = right
            });

            if (routeDistance >= 1.0f)
                Trace($"StreetRouteBuilder: LONG ROUTE ({routeDistance:F2}m) found from {fromPos} to {toPos} ({lanes.Count} lanes → {route.Segments.Count} segments)");
            else
                Trace($"StreetRouteBuilder: SHORT ROUTE ({routeDistance:F2}m) route found from {fromPos} to {toPos} ({lanes.Count} lanes → {route.Segments.Count} segments)");
            return route;
        }
        catch (Exception ex)
        {
            Trace($"StreetRouteBuilder: Pathfinding unavailable ({ex.Message}), using straight-line fallback.");
            return null;
        }
    }
}
