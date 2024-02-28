using System.Numerics;
using BepuPhysics;

namespace engine.physics.actions;

public class SetBodyPoseOrientation : ABase
{
    public int IntHandle;
    public Quaternion PoseOrientation;
    
    static public int Execute(Log? plog, Simulation simulation, ref BodyReference pref, in Quaternion poseOrientation)
    {
        pref.Pose.Orientation = poseOrientation;

        if (plog != null)
        {
            plog.Append(new SetBodyPoseOrientation()
            {
                PoseOrientation = poseOrientation,
                IntHandle = pref.Handle.Value
            });
        }

        return 0;
    }
    
    
    public override int Execute(Log? plog, Simulation simulation)
    {
        BodyReference pref = simulation.Bodies.GetBodyReference(new BodyHandle(IntHandle));
        return Execute(plog, simulation, ref pref, PoseOrientation);
    }
}