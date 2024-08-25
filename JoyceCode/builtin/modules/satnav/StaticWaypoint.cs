using System;
using System.Numerics;
using engine.world;

namespace builtin.modules.satnav;

public class StaticWaypoint : IWaypoint
{
    public Vector3 Location { get; set; }
    
    
    public DateTime LastMovedAt { get => DateTime.MinValue; }
    
    
    public Vector3 GetLocation()
    {
        return Location;
    }

    
    public bool IsValid()
    {
        return true;
    }

    
    public void Dispose()
    {
    }
}