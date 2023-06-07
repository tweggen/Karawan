using System.Collections.Generic;
using BepuPhysics;

namespace engine.physics;

public class API
{
    private object _lo = new();
    
    private Engine _engine;
    
    private SortedDictionary<BodyHandle, CollisionProperties> _mapCollisionProperties = new();

    public bool GetCollisionProperties(in BodyHandle bodyHandle, out CollisionProperties collisionProperties)
    {
        lock (_lo)
        {
            return _mapCollisionProperties.TryGetValue(bodyHandle, out collisionProperties);
        }
    }
    
    /**
     * Add a record of collision properties for the given body
     */
    public void AddCollisionEntry(in BodyHandle bodyHandle, CollisionProperties collisionProperties)
    {
        lock (_lo)
        {
            _mapCollisionProperties[bodyHandle] = collisionProperties;
        }
    }

    /**
     * Remove a record of collision properties for the given body.
     */
    public void RemoveCollisionEntry(in BodyHandle bodyHandle)
    {
        lock (_lo)
        {
            _mapCollisionProperties.Remove(bodyHandle);
        }
    }
    
    public API(Engine engine)
    {
        _engine = engine;
    }
}
