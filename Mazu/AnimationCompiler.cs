using builtin.loader;
using engine;
using engine.joyce;
using static engine.Logger;

namespace Mazu;

public class AnimationCompiler : IDisposable
{
    public required string ModelUrl;
    public required string AnimationUrls;
    public void Dispose()
    {
        Trace($"Disposing {nameof(AnimationCompiler)}");
    }
    
    public async Task Compile()
    {
        Trace($"Compiling animation.");
        var model = await I.Get<ModelCache>().LoadModel(new ModelCacheParams()
        {
            Url = ModelUrl,
            Properties = new ModelProperties()
            {
                Properties = new()
                {
                    { "CPUNodes", "MiddleFinger2_R;MiddleFinger2_L" },
                    { "ModelBaseBone", "Root_M" },
                    { "AnimationUrls", AnimationUrls }
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