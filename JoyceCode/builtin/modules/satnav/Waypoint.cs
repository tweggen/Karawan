using System;
using System.Numerics;

namespace builtin.modules.satnav;


public interface IWaypoint
{
    public DateTime LastMovedAt { get; }
    
    public Vector3 GetLocation();


    public bool IsValid();
}
