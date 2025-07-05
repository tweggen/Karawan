using System;
using System.Numerics;
using engine;
using engine.joyce.components;
using static engine.Logger;

namespace builtin.modules.satnav;

public class PlayerWaypoint : IWaypoint
{
    private Engine _engine;
    private DefaultEcs.Entity _eCarrot;
    private Vector3 _v3LastPosition;
    // private DateTime _timestampLastPosition;
    
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
        if (!_engine.Player.TryGet(out _eCarrot))
        {
            /*
             * Leave default object if no player is available right now.
             * This will just return the previous location
             */
            Trace("No player available. Returning last known position.");
            _eCarrot = default;
        }

        if (_eCarrot == default)
        {
            return _v3LastPosition;
        }
        
        if (!_eCarrot.IsAlive) 
        {
            Trace("Player entity is not alive at this moment.");
            return _v3LastPosition;
        }

        if (!_eCarrot.IsEnabled())
        {
            
            Error("Player entity is not enabled at this moment.");
            return _v3LastPosition;
        }

        if (!_eCarrot.Has<Transform3ToWorld>())
        {
            // TXWTODO: We reach this with an entity that has NewParent and Transform.
            Error("Player entity has no transform to world at this moment. Returning last known position.");
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


    public PlayerWaypoint()
    {
        _engine = I.Get<Engine>();
    }
        
}