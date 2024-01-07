using BepuPhysics;
using System;
using System.Collections.Generic;

namespace engine.physics.components;


public struct Body
{
    public BepuPhysics.BodyReference Reference;
    public CollisionProperties CollisionProperties;
    public physics.Object? PhysicsObject;
    
    public Body(
        in physics.Object? physicsObject,
        in BodyReference bodyReference, 
        in CollisionProperties collisionProperties)
    {
        Reference = bodyReference;
        CollisionProperties = collisionProperties;
        PhysicsObject = physicsObject;
    }
}