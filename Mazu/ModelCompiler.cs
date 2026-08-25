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

        var modelProperties = new ModelProperties()
        {
            Properties = new(Properties)
        };

        /*
         * Imported DIRECTLY rather than through ModelCache.LoadModel, and this is
         * load-bearing.
         *
         * ModelCache.LoadModel does more than load: _obtain also runs
         * _instantiateModelParams, which folds
         * Model.FirstInstanceDescTransformWithInstance into the instance desc's
         * ModelTransform, and FindLights.Process. Those are PER-INSTANCE steps, and
         * the runtime performs them itself on whatever _fromFile returns - baked
         * model included. Baking a model that had already been through them made
         * the runtime apply them a SECOND time.
         *
         * On the character rigs that matrix carries the fbx cm->m scaling (0.01),
         * so the mesh rendered at 1/10000 scale, i.e. invisibly, while bone-attached
         * objects kept moving correctly because they read the animation matrices
         * instead. Shipped in the first cut of WP-4.2 and found on Windows.
         *
         * The file must therefore hold the model exactly as the IMPORTER produced
         * it - which is what _fromFile returns, and nothing more.
         */
        var model = await Fbx.LoadModelInstance(ModelUrl, modelProperties);
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
