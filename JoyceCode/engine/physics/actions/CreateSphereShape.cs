using System.Diagnostics;
using BepuPhysics;
using BepuPhysics.Collidables;

namespace engine.physics.actions;

public class CreateSphereShape : ABase
{
    public uint ResultPackedTypeIndex;
    public float Radius;
    
    static public int Execute(Log? plog, Simulation simulation, float radius, out BepuPhysics.Collidables.Sphere body)
    {
        body = new BepuPhysics.Collidables.Sphere(radius);
        var shape = simulation.Shapes.Add(body);

        if (plog != null)
        {
            plog.Append(new CreateSphereShape()
            {
                ResultPackedTypeIndex = shape.Packed,
                Radius = radius
            });
        }

        return (int) shape.Packed;
    }


    public override int Execute(Player player, Simulation simulation)
    {
        uint loggedTypeIndex = ResultPackedTypeIndex;
        uint currentTypeIndex = (uint) Execute(null, simulation, Radius, out var _);
        player.MapperShapes.Add(loggedTypeIndex, currentTypeIndex);
        return (int) currentTypeIndex;
    }
}