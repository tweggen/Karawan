using BepuPhysics;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using engine.behave;

namespace engine.physics.components;

public struct Kinetic
{
    public static uint DONT_FREE_PHYSICS = 1;

    public Vector3 LastPosition;
    public physics.CollisionProperties CollisionProperties;
    public float MaxDistance = 50f;
    public uint Flags = 0;
    
    physics.Object? PhysicsObject;
    
    public Kinetic(in physics.Object po, in CollisionProperties collisionProperties)
    {
        PhysicsObject = po;
        CollisionProperties = collisionProperties;
    }
}