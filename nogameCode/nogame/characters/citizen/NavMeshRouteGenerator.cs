using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using builtin.modules.satnav.desc;
using builtin.tools;
using engine;
using engine.joyce;
using engine.tale;
using engine.world;
using static engine.Logger;

namespace nogame.characters.citizen;

/// <summary>
/// IRouteGenerator implementation using NavMesh pathfinding.
/// Computes realistic street routes by running A* over the NavMap street graph.
/// Falls back gracefully to null (straight-line movement) on pathfinding failure or timeout.
/// </summary>
public class NavMeshRouteGenerator : IRouteGenerator
{
    private const float TimeoutMs = 100f; // Max pathfinding time before giving up (allows fallback)

    public async Task<SegmentRoute> GetRouteAsync(Vector3 fromPos, Vector3 toPos, PositionDescription startPod)
    {
        try
        {
            // Create cancellation token to prevent pathfinding from blocking NPC movement indefinitely
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(TimeoutMs));

            // Get NavMap from DI
            var navMap = I.Get<NavMap>();
            if (navMap == null)
            {
                Trace("NavMeshRouteGenerator: NavMap not available, using straight-line fallback");
                return null;
            }

            // Run pathfinding with timeout
            var route = await StreetRouteBuilder.BuildAsync(fromPos, toPos, navMap, startPod, cts.Token);
            return route; // null is fine here — GoToStrategyPart will use straight-line fallback
        }
        catch (OperationCanceledException)
        {
            Trace("NavMeshRouteGenerator: pathfinding timeout, using straight-line fallback");
            return null;
        }
        catch (Exception e)
        {
            Warning($"NavMeshRouteGenerator: pathfinding failed: {e.Message}");
            return null;
        }
    }
}
