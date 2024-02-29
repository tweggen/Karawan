using BepuPhysics;

namespace engine.physics.actions;

public class SetBodyAwake : ABase
{
    public int IntHandle;
    public bool IsAwake;
    
    static public int Execute(Log? plog, Simulation simulation, ref BodyReference pref, bool isAwake)
    {
        pref.Awake = isAwake;

        if (plog != null)
        {
            plog.Append(new SetBodyAwake()
            {
                IsAwake = isAwake,
                IntHandle = pref.Handle.Value
            });
        }

        return 0;
    }
    
    
    public override int Execute(Player player, Simulation simulation)
    {
        int currentHandle = player.MapperBodies.GetNew(IntHandle);
        BodyReference pref = simulation.Bodies.GetBodyReference(new BodyHandle(currentHandle));
        return Execute(null, simulation, ref pref, IsAwake);
    }
}