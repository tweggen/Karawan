using System.Numerics;
using BepuPhysics;

namespace engine.physics.actions;

public class SetBodyLinearVelocity : ABase
{
    public int IntHandle;
    public Vector3 LinearVelocity;
    
    static public int Execute(Log? plog, Simulation simulation, ref BodyReference pref, in Vector3 linearVelocity)
    {
        pref.Velocity.Linear = linearVelocity;

        if (plog != null)
        {
            plog.Append(new SetBodyLinearVelocity()
            {
                LinearVelocity = linearVelocity,
                IntHandle = pref.Handle.Value
            });
        }

        return 0;
    }
    
    
    public override int Execute(Log? plog, Simulation simulation)
    {
        BodyReference pref = simulation.Bodies.GetBodyReference(new BodyHandle(IntHandle));
        return Execute(plog, simulation, ref pref, LinearVelocity);
    }
}
