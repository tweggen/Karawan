using BepuPhysics;

namespace engine.physics.actions;

public class RemoveBody : ABase
{
    public int IntHandle;

    static public int Execute(Log? plog, Simulation simulation, int intHandle)
    {
        simulation.Bodies.Remove(new BodyHandle(intHandle));

        if (plog != null)
        {
            plog.Append(new RemoveBody() { IntHandle = intHandle });
        }

        return 0;
    }
    
    
    public override int Execute(Player player, Simulation simulation)
    {
        int currentHandle = player.MapperBodies.GetNew(IntHandle);
        int result = Execute(null, simulation, currentHandle);
        player.MapperBodies.Remove(IntHandle);
        return result;
    }
}