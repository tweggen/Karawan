using System;
using System.Diagnostics;
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


    public override int Execute(Player player, Simulation simulation)
    {
        int loggedHandle = ResultIntHandle;
        uint currentTypeIndex;
        try
        {
            currentTypeIndex = player.MapperShapes.GetNew(PackedTypeIndex);
        }
        catch (Exception e)
        {
            // In case we do not have all shapes.
            currentTypeIndex = 2147483652;
        }
        int currentHandle =  Execute(null, simulation, Position, Orientation, new TypedIndex() { Packed = currentTypeIndex });
        player.MapperBodies.Add(loggedHandle, currentHandle);
        return currentHandle;
    }
}



