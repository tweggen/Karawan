using System.Security.Cryptography;
using System.Text;
using builtin.baking;
using builtin.loader;
using engine;
using engine.joyce;
using static engine.Logger;

namespace Mazu;

public class AnimationCompiler : IDisposable
{
    public required string ModelUrl;
    public required string AnimationUrls;
    public required string OutputDirectory;
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
        Trace($"Animation {model.Name} loaded.");
        string strModelAnims = $"{ModelUrl};{AnimationUrls}";
        string strHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(strModelAnims)));
        string strFileName = $"ac-{strHash}";
        using (var ostream = new FileStream(
                   Path.Combine(OutputDirectory, strFileName),
                   FileMode.Create, FileAccess.Write))
        {
            ModelAnimationCollectionWriter.Write(ostream, model.AnimationCollection);

        }
        Trace($"Animation {model.Name} serialized.");
    }

    public AnimationCompiler()
    {
        Trace($"Animation compiler initialized.");
    }
}