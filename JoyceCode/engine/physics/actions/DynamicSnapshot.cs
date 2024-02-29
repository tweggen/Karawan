using BepuPhysics;

namespace engine.physics.actions;

public class DynamicSnapshot : ABase
{
    public int IntHandle;
    public bool IsAwake;
    public MotionState MotionState;
    
    public static int Execute(Log? plog, Simulation simulation, in BodyReference pref)
    {
        if (plog != null)
        {
            plog.Append(new DynamicSnapshot()
            {
                IntHandle = pref.Handle.Value,
                IsAwake = pref.Awake,
                MotionState = pref.MotionState
            });

            return 0;
        }

        return 0;
    }
    
    public override int Execute(Player player, Simulation simulation)
    {
        int currentHandle = player.MapperBodies.GetNew(IntHandle);
        BodyReference pref = simulation.Bodies.GetBodyReference(new BodyHandle(currentHandle));
        pref.MotionState = MotionState;
        pref.Awake = IsAwake;
        return 0;
    }
}