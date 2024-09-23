using System;
using System.Numerics;
using engine.joyce.components;
using static engine.Logger;

namespace builtin.modules.satnav;

public class EntityWaypoint : IWaypoint
{
    private DefaultEcs.Entity _eCarrot;
    private Vector3 _v3LastPosition;
    // private DateTime _timestampLastPosition;
    
    public required DefaultEcs.Entity Carrot { 
        get
        {
            return _eCarrot;
        }
        init
        {
            _eCarrot = value;
        }
    }
    
    
    public DateTime LastMovedAt
    {
        get
        {
            /*
             * Compute a datetime that changes every 4 seconds.
             */
            var utcNow = DateTime.UtcNow;
            return new DateTime(utcNow.Ticks & ~3);
        }
    }
    
    
    public Vector3 GetLocation()
    {
        if (_eCarrot == default)
        {
            Error("Asked an entity waypoint with unset entity for its position.");
            return _v3LastPosition;
        }
        if (!_eCarrot.IsAlive) 
        {
            Error("Asked an entity waypoint which is not alive.");
            return _v3LastPosition;
        }

        if (!_eCarrot.IsEnabled())
        {
            
            Error("Asked an entity waypoint which is not enabled.");
            return _v3LastPosition;
        }

        if (!_eCarrot.Has<Transform3ToWorld>())
        {
            // TXWTODO: We reach this with an entity that has NewParent and Transform.
            Error("Asked an entity waypoint which has no transform to world.");
            return _v3LastPosition;
        }
        
        _v3LastPosition = _eCarrot.Get<Transform3ToWorld>().Matrix.Translation;
        return _v3LastPosition;
    }


    public bool IsValid()
    {
        return _eCarrot != default 
               && _eCarrot.IsAlive 
               && _eCarrot.IsEnabled() 
               && _eCarrot.Has<Transform3ToWorld>();
    }


    public void Dispose()
    {
    }
}
