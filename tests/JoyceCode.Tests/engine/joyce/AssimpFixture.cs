using System.Collections.Generic;
using System.IO;
using builtin.loader;
using engine;
using engine.joyce;
using Xunit;

namespace JoyceCode.Tests.engine.joyce;

/**
 * Lets a test load an fbx through the real Assimp path.
 *
 * Two things have to exist for that: an engine.Assets implementation, because
 * AssimpFile.Open goes through it rather than touching the filesystem directly,
 * and Engine.ResourcePath, which is where it resolves names from.
 *
 * engine.Assets holds ONE global implementation, so everything using this shares
 * the [Collection("assimp")] fixture below and runs serially.
 */
public sealed class TestAssetImplementation : AAssetImplementation
{
    private readonly string _basePath;
    private readonly SortedDictionary<string, string> _mapAssociations = new();
    private readonly object _lo = new();

    public TestAssetImplementation(string basePath)
    {
        _basePath = basePath;
    }

    public override void AddAssociation(string tag, string uri)
    {
        lock (_lo)
        {
            _mapAssociations[tag] = uri;
        }
    }

    public override IReadOnlyDictionary<string, string> GetAssets()
    {
        lock (_lo)
        {
            return new SortedDictionary<string, string>(_mapAssociations);
        }
    }

    public override bool Exists(in string filename) => File.Exists(_resolve(filename));

    public override Stream Open(in string filename) => File.OpenRead(_resolve(filename));

    private string _resolve(string filename)
    {
        lock (_lo)
        {
            if (_mapAssociations.TryGetValue(filename, out var uri))
            {
                return Path.Combine(_basePath, uri);
            }
        }

        return Path.Combine(_basePath, filename);
    }
}


public static class AssimpFixture
{
    private static readonly object _lo = new();
    private static bool _isSetUp;

    /**
     * Load a model the way the game does today, straight through Assimp.
     *
     * Only the fbx names are registered, and they are registered flat by file
     * name, which is how the loader asks for them.
     */
    public static Model? LoadThroughAssimp(
        DirectoryInfo repoRoot, string modelFileName, IDictionary<string, string> properties)
    {
        lock (_lo)
        {
            if (!_isSetUp)
            {
                /*
                 * Set before the work, not after: the I container refuses to
                 * re-register a type, so a failure partway through here would make
                 * every subsequent test report "Already registered" instead of the
                 * error that actually happened.
                 */
                _isSetUp = true;

                string models = Path.Combine(repoRoot.FullName, "models") + Path.DirectorySeparatorChar;
                GlobalSettings.Set("Engine.ResourcePath", models);
                GlobalSettings.Set(
                    "Engine.GeneratedResourcePath",
                    Path.Combine(repoRoot.FullName, "nogame", "generated"));

                /*
                 * Same mode Chushi bakes under. Besides skipping the baked-asset
                 * associations that do not exist during a bake, it stops the fbx
                 * loader from adopting an ac-{hash} file - so what this loads is the
                 * fbx and nothing else, which is what the comparison needs.
                 */
                GlobalSettings.Set("joyce.CompileMode", "true");

                /*
                 * FbxModel._findMaterial resolves every material through the texture
                 * catalogue, and the catalogue is only populated once the /textures
                 * config has been interpreted. So the fbx path cannot be exercised
                 * without a config bootstrap; this mirrors Chushi/ConsoleMain.
                 */
                I.Register<TextureCatalogue>(() => new TextureCatalogue());
                I.Register<AnimationPackRegistry>(() => new AnimationPackRegistry());

                /*
                 * Engine.SetupDone registers these, and FbxModel's material path
                 * resolves them. Registered directly rather than by booting an
                 * Engine, which would start threads this test has no use for.
                 * ModelCache is deliberately NOT registered: it needs a live
                 * Engine, and this test calls Fbx.LoadModelInstanceSync directly.
                 */
                I.Register<ObjectRegistry<Material>>(() => new ObjectRegistry<Material>());
                I.Register<ObjectRegistry<Renderbuffer>>(() => new ObjectRegistry<Renderbuffer>());

                /*
                 * Order matters and mirrors Chushi: WithLoader() resolves the
                 * config loader in order to subscribe to it, so the loader has to
                 * be registered first.
                 */
                I.Register<global::engine.casette.Loader>(() =>
                {
                    using var streamJson = File.OpenRead(Path.Combine(models, "nogame.json"));
                    return new global::engine.casette.Loader(streamJson);
                });

                var impl = new TestAssetImplementation(models);
                impl.WithLoader();

                I.Get<global::engine.casette.Loader>().InterpretConfig();

                /*
                 * The loader asks for models by bare file name, so register them
                 * flat alongside whatever the config already associated.
                 */
                foreach (var path in Directory.GetFiles(
                             Path.Combine(models, "models", "people", "polyperfect"), "*.fbx"))
                {
                    impl.AddAssociation(
                        Path.GetFileName(path),
                        Path.GetRelativePath(models, path));
                }

                _isSetUp = true;
            }

            var modelProperties = new ModelProperties();
            foreach (var kvp in properties)
            {
                modelProperties.Properties[kvp.Key] = kvp.Value;
            }

            Fbx.LoadModelInstanceSync(modelFileName, modelProperties, out var model);
            return model;
        }
    }
}


/**
 * Serialises everything that touches the single global engine.Assets slot.
 */
[CollectionDefinition("assimp", DisableParallelization = true)]
public class AssimpCollection
{
}
