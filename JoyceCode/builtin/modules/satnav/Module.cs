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
      *
      * @param transportType
      *     What is travelling it. Deliberately without a default: the nav map holds car
      *     lanes down the carriageway and pedestrian lanes along the pavement, and a
      *     route over the wrong ones is drawn on the wrong surface.
      */
    public Route CreateRoute(
        IWaypoint wFrom, IWaypoint wTo, engine.navigation.TransportationType transportType)
    {
        return new Route(I.Get<NavMap>(), wFrom, wTo, transportType);
    }
}