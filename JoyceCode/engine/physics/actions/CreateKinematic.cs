using System.Numerics;
using System.Text.Json;
using BepuPhysics;
using BepuPhysics.Collidables;

namespace engine.physics.actions;

public class CreateKinematic : ABase
{
    public int ResultIntHandle;
    public Vector3 Position;
    public Quaternion Orientation;
    public uint PackedTypeIndex;
    
    static public int Execute(Log? plog, Simulation simulation, Vector3 v3Position, Quaternion qOrientation, TypedIndex shape)
    {
        int intHandle = simulation.Bodies.Add(
            BodyDescription.CreateKinematic(
                new RigidPose(v3Position, qOrientation),
                new CollidableDescription(shape, 0.1f),
                new BodyActivityDescription(0.01f))).Value;

        if (plog != null)
        {
            plog.Append(new CreateKinematic()
            {
                ResultIntHandle = intHandle,
                Position = v3Position,
                Orientation = qOrientation,
                PackedTypeIndex = shape.Packed
            });
        }

        return intHandle;
    }


    public override int Execute(Log? plog, Simulation simulation)
    {
        return Execute(plog, simulation, Position, Orientation, new TypedIndex() { Packed = PackedTypeIndex });
    }
}



