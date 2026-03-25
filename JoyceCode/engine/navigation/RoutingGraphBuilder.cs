using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using builtin.modules.satnav.desc;

namespace engine.navigation;

/// <summary>
/// Builds type-specific routing graphs from a collection of NavLanes.
/// </summary>
public class RoutingGraphBuilder
{
    private builtin.modules.satnav.desc.NavMap _navMap;
    private Dictionary<TransportationType, RoutingGraph> _graphCache;

    public RoutingGraphBuilder(builtin.modules.satnav.desc.NavMap navMap)
    {
        _navMap = navMap;
        _graphCache = new Dictionary<TransportationType, RoutingGraph>();
    }

    /// <summary>
    /// Build (or retrieve cached) routing graph for a transportation type.
    /// </summary>
    public RoutingGraph BuildFor(TransportationType type, IEnumerable<NavLane> allLanes)
    {
        // Return cached graph if available
        if (_graphCache.TryGetValue(type, out var cached))
            return cached;

        var graph = new RoutingGraph { SupportedType = type };

        // Step 1: Filter lanes by type
        graph.AllLanes = allLanes
            .Where(lane => lane.AllowedTypes.HasFlag(type))
            .ToList();

        // Step 2: Build adjacency (connectivity)
        graph.Edges = new Dictionary<NavLane, List<NavLane>>();
        foreach (var lane in graph.AllLanes)
        {
            var outgoing = new List<NavLane>();

            foreach (var other in graph.AllLanes)
            {
                // Lane A connects to Lane B if A's end junction == B's start junction
                if (AreConnected(lane, other))
                    outgoing.Add(other);
            }

            graph.Edges[lane] = outgoing;
        }

        // Cache and return
        _graphCache[type] = graph;
        return graph;
    }

    /// <summary>
    /// Check if two lanes are connected (A.End == B.Start).
    /// </summary>
    private bool AreConnected(NavLane from, NavLane to)
    {
        // Lanes are connected if the end junction of 'from' is the start junction of 'to'
        return from.End == to.Start;
    }

    /// <summary>
    /// Invalidate cache when lanes change.
    /// </summary>
    public void InvalidateCache()
    {
        _graphCache.Clear();
    }

    /// <summary>
    /// Get cache statistics (for debugging).
    /// </summary>
    public Dictionary<TransportationType, int> GetCacheStats()
    {
        return _graphCache
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.AllLanes.Count);
    }
}
