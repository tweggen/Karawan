using System;
using System.Numerics;

namespace builtin.modules.satnav;


public interface IWaypoint : IDisposable
{
    public DateTime LastMovedAt { get; }
    
    public Vector3 GetLocation();
    

    public bool IsValid();
}
