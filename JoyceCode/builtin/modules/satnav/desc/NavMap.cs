using System;
using System.Collections.Generic;
using System.Linq;
using engine.navigation;
using static engine.Logger;

namespace builtin.modules.satnav.desc;

public class NavMap
{
    private object _lo = new();

    private NavCluster _topCluster;
    public NavCluster TopCluster
    {
        get {
            lock (_lo)
            {
                if (null == _topCluster)
                {
                    ErrorThrow<InvalidOperationException>($"No top cluster has been setup yet.");
                }
                return _topCluster;
            }
        }


        set
        {
            lock (_lo)
            {
                if (null != _topCluster)
                {
                    ErrorThrow<InvalidOperationException>($"No top cluster already had been setup yet.");
                }
                _topCluster = value;
            }
        }
    }

    /// <summary>
    /// All lanes in the navigation map.
    /// </summary>
    public List<NavLane> AllLanes { get; set; } = new();

    /// <summary>
    /// Builds type-specific routing graphs.
    /// </summary>
    private RoutingGraphBuilder _graphBuilder;

    public NavMap()
    {
        _graphBuilder = new RoutingGraphBuilder(this);
    }

    /// <summary>
    /// Get the routing graph for a specific transportation type.
    /// </summary>
    public RoutingGraph GetGraphFor(TransportationType type)
    {
        return _graphBuilder.BuildFor(type, AllLanes);
    }

    /// <summary>
    /// Invalidate routing graph cache (call when lanes change).
    /// </summary>
    public void InvalidateGraphCache()
    {
        _graphBuilder.InvalidateCache();
    }
}