using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using BepuPhysics;
using BepuPhysics.Collidables;

namespace engine.physics.actions;

public class CreateDynamic : ABase
{
    public int ResultIntHandle;
    public Vector3 Position;
    public Quaternion Orientation;
    public BodyInertia Inertia;
    public uint PackedTypeIndex;
    
    static public int Execute(Log? plog, Simulation simulation,
        Vector3 v3Position, Quaternion qOrientation, 
        BodyInertia inertia, TypedIndex shape)
    {
        int intHandle = simulation.Bodies.Add(
            BodyDescription.CreateDynamic(
                new RigidPose(v3Position, qOrientation),
                inertia, 
                new CollidableDescription(shape, 0.1f),
                new BodyActivityDescription(0.01f))).Value;

        if (plog != null)
        {
            plog.Append(new CreateDynamic()
            {
                ResultIntHandle = intHandle,
                Position = v3Position,
                Orientation = qOrientation,
                Inertia = inertia,
                PackedTypeIndex = shape.Packed
            });
        }

        return intHandle;
    }


    public override int Execute(Player player, Simulation simulation)
    {
        int loggedHandle = ResultIntHandle;
        uint currentTypeIndex = player.MapperShapes.GetNew(PackedTypeIndex);
        int currentHandle = Execute(null, simulation, Position, Orientation, Inertia, new TypedIndex() { Packed = currentTypeIndex });
        player.MapperBodies.Add(loggedHandle, currentHandle);
        return currentHandle;
    }
}