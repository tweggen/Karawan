using BepuPhysics;

namespace engine.physics.components;


public struct Body
{
    public BepuPhysics.BodyReference Reference;
    public physics.Object? PhysicsObject;
    
    public Body(
        in physics.Object? physicsObject,
        in BodyReference bodyReference)
    {
        Reference = bodyReference;
        PhysicsObject = physicsObject;
    }
}