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
        Vector3 v3From = wFrom.GetLocation();
        Vector3 v3To = wTo.GetLocation();

        Route route = new Route(I.Get<NavMap>(), wFrom, wTo);
        
        return route;
    }
}