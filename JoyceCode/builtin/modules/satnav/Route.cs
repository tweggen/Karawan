using System;
using System.Collections.Generic;
using System.Numerics;
using builtin.modules.satnav.desc;
using engine;
using engine.world;
using SharpNav;
using SharpNav.Pathfinding;

namespace builtin.modules.satnav;

public class Route : IDisposable
{
    public NavMap NavMap { get; }

    private IWaypoint _a;
    private IWaypoint _b;
    
    public IWaypoint A
    {
        get
        {
            return _a;
        }
    }

    public IWaypoint B
    {
        get
        {
            return _b;
        }
    }


    public void Suspend()
    {
        
    }
    
    
    public void Activate()
    {
        Vector3 v3StartCenter = _a.GetLocation();
        Vector3 v3StartExtents = new(10f, 10f, 10f);
        
        Vector3 v3EndCenter = _b.GetLocation();
        Vector3 v3EndExtents = new(10f, 10f, 10f);

        var tStart = NavMap.TopCluster.TryCreateCursor(v3StartCenter);
        var tEnd = NavMap.TopCluster.TryCreateCursor(v3EndCenter);
    }


    public void Dispose()
    {
    }


    public Route(NavMap nm, IWaypoint a, IWaypoint b)
    {
        NavMap = nm;
        _a = a;
        _b = b;
    }
}
