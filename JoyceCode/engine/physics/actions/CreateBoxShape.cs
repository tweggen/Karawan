using System.Diagnostics;
using BepuPhysics;
using BepuPhysics.Collidables;

namespace engine.physics.actions;

public class CreateBoxShape : ABase
{
    public uint ResultPackedTypeIndex;
    public float Width;
    public float Height;
    public float Length;
    
    static public int Execute(Log? plog, Simulation simulation, float width, float height, float length, out BepuPhysics.Collidables.Box body)
    {
        body = new BepuPhysics.Collidables.Box(width, height, length);
        var shape = simulation.Shapes.Add(body);

        if (plog != null)
        {
            plog.Append(new CreateBoxShape()
            {
                ResultPackedTypeIndex = shape.Packed,
                Width = width,
                Height = height,
                Length = length
            });
        }
        else
        {
            int a = 0;
        }

        return (int) shape.Packed;
    }


    public override int Execute(Player player, Simulation simulation)
    {
        uint loggedTypeIndex = ResultPackedTypeIndex;
        uint currentTypeIndex = (uint) Execute(null, simulation, Width, Height, Length, out var _);
        player.MapperShapes.Add(loggedTypeIndex, currentTypeIndex);
        return (int) currentTypeIndex;
    }
}
