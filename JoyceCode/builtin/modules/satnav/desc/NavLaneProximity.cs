using System;
using System.Collections.Generic;
using System.Numerics;
using engine.navigation;

namespace builtin.modules.satnav.desc;

/// <summary>
/// Utility helper for finding closest points on NavLanes.
/// Used by spatial model, routing, and other satnav-dependent systems.
/// </summary>
public static class NavLaneProximity
{
    /// <summary>
    /// Find the closest point on the closest pedestrian-accessible NavLane.
    /// </summary>
    /// <param name="position">Reference position in world space</param>
    /// <param name="lanes">List of NavLanes to search</param>
    /// <param name="closestLane">Output: the closest lane found</param>
    /// <param name="closestPoint">Output: the closest point on that lane</param>
    /// <returns>Distance to the closest point, or float.MaxValue if no suitable lane found</returns>
    public static float FindClosestPedestrianLanePoint(
        Vector3 position,
        List<NavLane> lanes,
        out NavLane closestLane,
        out Vector3 closestPoint)
    {
        closestLane = null;
        closestPoint = Vector3.Zero;
        float minDistance = float.MaxValue;

        if (lanes == null || lanes.Count == 0)
            return minDistance;

        foreach (var lane in lanes)
        {
            // Only consider pedestrian-accessible lanes
            if (!lane.AllowedTypes.HasFlag(TransportationType.Pedestrian))
                continue;

            var (point, distance) = GetClosestPointOnLane(position, lane);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestLane = lane;
                closestPoint = point;
            }
        }

        return minDistance;
    }

    /// <summary>
    /// Find the closest point on a specific NavLane.
    /// </summary>
    private static (Vector3 point, float distance) GetClosestPointOnLane(Vector3 position, NavLane lane)
    {
        var start = lane.Start.Position;
        var end = lane.End.Position;

        // Vector from start to end
        var laneVec = end - start;
        float laneLength2 = Vector3.Dot(laneVec, laneVec);

        if (laneLength2 < 0.0001f)
        {
            // Degenerate lane (start == end), use start position
            float dist = Vector3.Distance(position, start);
            return (start, dist);
        }

        // Project position onto the lane
        var toPos = position - start;
        float t = Vector3.Dot(toPos, laneVec) / laneLength2;
        t = Math.Clamp(t, 0f, 1f);

        var closestPoint = start + t * laneVec;
        float distance = Vector3.Distance(position, closestPoint);

        return (closestPoint, distance);
    }

    /// <summary>
    /// Get a position on a NavLane at a fractional distance along it.
    /// Useful for distributing multiple NPCs along a street.
    /// </summary>
    /// <param name="lane">The NavLane to sample from</param>
    /// <param name="t">Fraction along lane [0, 1] where 0=start, 1=end</param>
    /// <returns>Position on the lane</returns>
    public static Vector3 GetPositionOnLane(NavLane lane, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return Vector3.Lerp(lane.Start.Position, lane.End.Position, t);
    }
}
