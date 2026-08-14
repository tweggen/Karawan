using builtin.baking;
using builtin.loader;
using engine;
using engine.joyce;
using static engine.Logger;

namespace Mazu;

/**
 * Bakes one model into a mo-{hash} file (WP-4.2).
 *
 * Deliberately shaped like AnimationCompiler: load through the ordinary
 * ModelCache path, then serialise what came out. Loading through the real path
 * rather than a bake-specific one is the point - it is what makes the baked file
 * equal to what the fbx loader produces, which is what AC-4.2 asserts.
 *
 * Note the properties passed here must be the ones the GAME will use, because
 * they are part of the file name; see builtin.baking.ModelFileName.
 */
public class ModelCompiler : IDisposable
{
    public required string ModelUrl;
    public required SortedDictionary<string, string> Properties;
    public required string OutputDirectory;

    public void Dispose()
    {
        Trace($"Disposing {nameof(ModelCompiler)}");
    }


    public async Task Compile()
    {
        Trace($"Compiling model {ModelUrl}.");

        var model = await I.Get<ModelCache>().LoadModel(new ModelCacheParams()
        {
            Url = ModelUrl,
            Properties = new ModelProperties()
            {
                Properties = new(Properties)
            }
        });
        Trace($"Model {model.Name} loaded.");

        string strModelFileOnly = Path.GetFileName(ModelUrl);
        string strFileName = ModelFileName.Of(strModelFileOnly, Properties);

        using (var ostream = new FileStream(
                   Path.Combine(OutputDirectory, strFileName),
                   FileMode.Create, FileAccess.Write))
        {
            ModelWriter.Write(ostream, model);
        }

        Trace($"Model {model.Name} serialized to {strFileName}.");
    }


    public ModelCompiler()
    {
        Trace($"Model compiler initialized.");
    }
}
