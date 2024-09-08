using System;
using System.Collections.Generic;
using engine;
using System.Numerics;
using builtin.modules.satnav.desc;
using engine.world;
using engine.world.components;
using static engine.Logger;

namespace builtin.modules.satnav;

public class Module : AModule
{
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        //new MyModule<MapDB>() {}
    };

    
    /**
      * Create a route from one waypoint to another.
      */
    public Route CreateRoute(IWaypoint wFrom, IWaypoint wTo)
    {
        /*
         * Do a short sanity check if we suoport this right now. 
         */

        Vector3 v3From = wFrom.GetLocation();
        Vector3 v3To = wTo.GetLocation();
        ClusterDesc? clusterFrom = ClusterList.Instance().GetClusterAt(v3From); 
        ClusterDesc? clusterTo = ClusterList.Instance().GetClusterAt(v3To);

        if (null == clusterFrom || null == clusterTo || clusterFrom != clusterTo)
        {
            ErrorThrow<ArgumentException>("Route waypoints are not inside the same cluster.");
        }
        
        Route route = new Route(I.Get<NavMap>(), wFrom, wTo);
        
        return route;
    }
    
    
    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }


    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _engine.AddModule(this);
    }
}