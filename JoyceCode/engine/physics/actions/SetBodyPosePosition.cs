using System.Numerics;
using BepuPhysics;

namespace engine.physics.actions;

public class SetBodyPosePosition : ABase
{
    public int IntHandle;
    public Vector3 PosePosition;
    
    static public int Execute(Log? plog, Simulation simulation, ref BodyReference pref, in Vector3 posePosition)
    {
        pref.Pose.Position = posePosition;

        if (plog != null)
        {
            plog.Append(new SetBodyPosePosition()
            {
                PosePosition = posePosition,
                IntHandle = pref.Handle.Value
            });
        }

        return 0;
    }
    
    
    public override int Execute(Player player, Simulation simulation)
    {
        int currentHandle = player.MapperBodies.GetNew(IntHandle);
        BodyReference pref = simulation.Bodies.GetBodyReference(new BodyHandle(currentHandle));
        return Execute(null, simulation, ref pref, PosePosition);
    }
}