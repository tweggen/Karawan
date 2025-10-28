using builtin.loader;
using engine;
using engine.joyce;
using static engine.Logger;

namespace Mazu;

public class AnimationCompiler : IDisposable
{
    public void Dispose()
    {
        Trace($"Disposing {nameof(AnimationCompiler)}");
    }
    
    public async Task Compile()
    {
        Trace($"Compiling animation.");
        var model = await I.Get<ModelCache>().LoadModel(new ModelCacheParams()
        {
            Url = "man_casual_Rig.fbx",
            Properties = new ModelProperties()
            {
                Properties = new()
                {
                    { "CPUNodes", "MiddleFinger2_R;MiddleFinger2_L" },
                    { "ModelBaseBone", "Root_M" },
                    { "AnimationUrls", "Idle_Generic.fbx;Run_InPlace.fbx;Walk_Male.fbx;Running_Jump.fbx;Standing_Jump.fbx;Punch_LeftHand.fbx;Punch_RightHand.fbx;Death_FallForwards.fbx" }
                }
            }
        });
        Trace($"Animation {model.Name} compiled.");
    }

    public AnimationCompiler()
    {
        Trace($"Animation compiler initialized.");
    }
}