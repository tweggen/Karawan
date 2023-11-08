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
    private Action<CollidableReference, CollisionProperties, float, Vector3> _action;


    public int GetRayHitId()
    {
        return _rayHitId;
    }
    
    public bool AllowTest(CollidableReference collidable)
    {
        /*
         * This does not belong here, but we don't want to raycast the player today.
         */
        CollisionProperties collisionProperties = null;
        
        switch (collidable.Mobility)
        {
            case CollidableMobility.Dynamic:
                _api.GetCollisionProperties(collidable.BodyHandle, out collisionProperties);
                if (collisionProperties != null)
                {
                    if (collisionProperties.Name == "nogame.playerhover")
                    {
                        return false;
                    }
                }
                break;
            case CollidableMobility.Kinematic:
                _api.GetCollisionProperties(collidable.BodyHandle, out collisionProperties);
                break;
            case CollidableMobility.Static:
                // Don't have that yet.
                // _api.GetCollisionProperties(collidable.StaticHandle, out collisionProperties);
                break;
        }

        return true;
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

        _action(collidable, collisionProperties, t, normal);
    }
    

    public DefaultRayHitHandler(physics.API api, Action<CollidableReference, CollisionProperties, float, Vector3> action)
    {
        lock (_classLock)
        {
            _rayHitId = _nextRayHitId++;
        }

        _api = api;
        _action = action;
    }
}