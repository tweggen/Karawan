using System;
using System.Collections.Generic;
using System.Linq;
using builtin.modules.satnav.desc;

namespace engine.navigation;

/// <summary>
/// A directed graph of navigation lanes for a specific transportation type.
/// </summary>
public class RoutingGraph
{
    /// <summary>
    /// Adjacency list: for each lane, which lanes can be reached from it.
    /// </summary>
    public Dictionary<NavLane, List<NavLane>> Edges { get; set; } = new();

    /// <summary>
    /// All lanes accessible for this transportation type.
    /// </summary>
    public List<NavLane> AllLanes { get; set; } = new();

    /// <summary>
    /// The transportation type this graph supports.
    /// </summary>
    public TransportationType SupportedType { get; set; }

    /// <summary>
    /// Find all lanes reachable from a starting lane.
    /// </summary>
    public List<NavLane> GetNeighbors(NavLane lane)
    {
        if (Edges.TryGetValue(lane, out var neighbors))
            return neighbors;
        return new List<NavLane>();
    }

    /// <summary>
    /// Check if this graph contains the lane.
    /// </summary>
    public bool ContainsLane(NavLane lane) => AllLanes.Contains(lane);
}
