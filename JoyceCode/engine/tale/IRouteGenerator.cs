using System;
using System.Numerics;
using System.Threading.Tasks;
using builtin.tools;
using engine.joyce;
using engine.world;

namespace engine.tale;

/// <summary>
/// Computes a walkable route between two world positions.
/// Returns null if pathfinding fails — callers must handle null as "use straight-line fallback".
/// Pluggable interface to support multiple transport types (NavMesh, public transit, cars, etc.).
/// </summary>
public interface IRouteGenerator
{
    /// <summary>
    /// Compute a route from fromPos to toPos.
    /// </summary>
    /// <param name="fromPos">Starting world position</param>
    /// <param name="toPos">Destination world position</param>
    /// <param name="startPod">Starting position context (cluster/quarter/fragment)</param>
    /// <returns>SegmentRoute representing the path, or null if pathfinding fails</returns>
    Task<SegmentRoute> GetRouteAsync(Vector3 fromPos, Vector3 toPos, PositionDescription startPod);
}
