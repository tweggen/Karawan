using System;

namespace builtin.modules.satnav;

public class Route : IDisposable
{
    public MapDB MapDB { get; }

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
    }


    public void Dispose()
    {
    }


    public Route(MapDB mapDB, IWaypoint a, IWaypoint b)
    {
        MapDB = mapDB;
        _a = a;
        _b = b;
    }
}