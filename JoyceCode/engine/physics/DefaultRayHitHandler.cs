using System;
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Trees;

namespace engine.physics;

public class DefaultRayHitHandler : IRayHitHandler
{
    private static object _classLock = new();
    private static int _nextRayHitId = 0;

    private physics.API _api;
    private int _rayHitId;
    private Action<CollidableReference, CollisionProperties, Vector3> _action;


    public int GetRayHitId()
    {
        return _rayHitId;
    }
    
    public bool AllowTest(CollidableReference collidable)
    {
#if true
        /*
         * Currently we want to test everything.
         */
        return true;
#else
        CollisionProperties collisionProperties = null;
        
        switch (collidable.Mobility)
        {
            case CollidableMobility.Dynamic:
                _api.GetCollisionProperties(collidable.BodyHandle, out collisionProperties);
                break;
            case CollidableMobility.Kinematic:
                _api.GetCollisionProperties(collidable.BodyHandle, out collisionProperties);
                break;
            case CollidableMobility.Static:
                // Don't have that yet.
                // _api.GetCollisionProperties(collidable.StaticHandle, out collisionProperties);
                break;
        }
#endif
        
    }

    public bool AllowTest(CollidableReference collidable, int childIndex)
    {
        return true;
    }

    public void OnRayHit(in RayData ray, ref float maximumT, float t, Vector3 normal, CollidableReference collidable,
        int childIndex)
    {
        CollisionProperties collisionProperties = null;
        
        switch (collidable.Mobility)
        {
            case CollidableMobility.Dynamic:
                _api.GetCollisionProperties(collidable.BodyHandle, out collisionProperties);
                break;
            case CollidableMobility.Kinematic:
                _api.GetCollisionProperties(collidable.BodyHandle, out collisionProperties);
                break;
            case CollidableMobility.Static:
                // Don't have that yet.
                // _api.GetCollisionProperties(collidable.StaticHandle, out collisionProperties);
                break;
        }

        _action(collidable, collisionProperties, normal);
    }
    

    public DefaultRayHitHandler(physics.API api, Action<CollidableReference, CollisionProperties, Vector3> action)
    {
        lock (_classLock)
        {
            _rayHitId = _nextRayHitId++;
        }

        _api = api;
        _action = action;
    }
}