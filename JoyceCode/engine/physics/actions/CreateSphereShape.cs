using BepuPhysics;
using BepuPhysics.Collidables;

namespace engine.physics.actions;

public class CreateSphereShape : ABase
{
    public uint ResultPackedTypeIndex;
    public float Radius;
    
    static public int Execute(Log? plog, Simulation simulation, float radius)
    {
        var body = new BepuPhysics.Collidables.Sphere(radius);
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


    public override int Execute(Log? plog, Simulation simulation)
    {
        return Execute(plog, simulation, Radius);
    }
}