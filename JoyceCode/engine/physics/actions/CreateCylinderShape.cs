using System.Diagnostics;
using BepuPhysics;
using BepuPhysics.Collidables;

namespace engine.physics.actions;

public class CreateCylinderShape : ABase
{
    public uint ResultPackedTypeIndex;
    public float Radius;
    public float Length;
    
    static public int Execute(Log? plog, Simulation simulation, float radius, float length, out BepuPhysics.Collidables.Cylinder body)
    {
        body = new BepuPhysics.Collidables.Cylinder(radius, length);
        var shape = simulation.Shapes.Add(body);

        if (plog != null)
        {
            plog.Append(new CreateCylinderShape()
            {
                ResultPackedTypeIndex = shape.Packed,
                Radius = radius
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
        uint currentTypeIndex = (uint) Execute(null, simulation, Radius, Length, out var _);
        player.MapperShapes.Add(loggedTypeIndex, currentTypeIndex);
        return (int) currentTypeIndex;
    }
}
