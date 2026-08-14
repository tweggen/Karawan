using System;
using System.Collections.Generic;
using System.IO;
using builtin.baking;
using builtin.loader;
using engine;
using engine.joyce;
using Xunit;
using Xunit.Abstractions;

namespace JoyceCode.Tests.engine.joyce;

/**
 * AC-4.4, in the form that is actually runnable here.
 *
 * The plan specifies "move *.fbx aside; run TestRunner; grep the log". That does
 * not work: TestRunner is the TALE/DES harness and has no renderer, so it never
 * starts the main scene and never loads a character model at all - a run of
 * tests/startup-smoke.json times out on "Wait for the main scene to start" with
 * zero model activity in the log. The smoke scripts are documented as running
 * against the game (docs/SYSTEMS/NARRATION/EXPECT_IMPLEMENTATION.md), not
 * TestRunner. Same class of error as AC-3.4, which the ledger already records.
 *
 * So the property is asserted directly instead, and more strictly than a log grep
 * would: every declared model is loaded through the real runtime path with an
 * asset layer that THROWS on any attempt to open an fbx. Falling back to Assimp is
 * therefore impossible rather than merely unobserved - if the baked path failed,
 * the test fails with the name of the fbx that was wanted.
 *
 * What this cannot show is that the result looks right on screen. That is GATE-D
 * and stays a human gate.
 */
[Collection("assimp")]
public class ModelLoadsWithoutFbxTests
{
    private readonly ITestOutputHelper _output;

    public ModelLoadsWithoutFbxTests(ITestOutputHelper output)
    {
        _output = output;
    }


    /**
     * Serves the generated/ directory and nothing else. Any fbx request is a
     * failure, not a fallback.
     */
    private sealed class NoFbxAssetImplementation : IAssetImplementation
    {
        private readonly string _generated;
        public readonly List<string> RefusedFbx = new();

        public NoFbxAssetImplementation(string generated)
        {
            _generated = generated;
        }

        public Stream Open(in string filename)
        {
            if (filename.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                RefusedFbx.Add(filename);
                throw new FileNotFoundException(
                    $"fbx access is not allowed in this test: {filename}");
            }

            return File.OpenRead(Path.Combine(_generated, filename));
        }

        public bool Exists(in string filename)
            => !filename.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
               && File.Exists(Path.Combine(_generated, filename));

        public void AddAssociation(string tag, string uri)
        {
        }

        public IReadOnlyDictionary<string, string> GetAssets() => new Dictionary<string, string>();
    }


    [Fact]
    public void EveryDeclaredModelLoadsFromItsBakeWithNoFbxAccess()
    {
        var declared = new List<object[]>(BakedModelEquivalenceTests.DeclaredModels());
        if (declared.Count == 0)
        {
            _output.WriteLine("no declared models found; skipping");
            return;
        }

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName, "nogame", "generated")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);
        string generated = Path.Combine(root!.FullName, "nogame", "generated");

        if (Directory.GetFiles(generated, "mo-*").Length == 0)
        {
            _output.WriteLine("no bake output present; skipping");
            return;
        }

        /*
         * CompileMode off, or TryLoadBaked declines by design - it is the flag that
         * marks the run that PRODUCES these files.
         */
        GlobalSettings.Set("joyce.CompileMode", "false");
        GlobalSettings.Set("joyce.DisablePrebakedModels", "false");

        var noFbx = new NoFbxAssetImplementation(generated);
        Assets.SetAssetImplementation(noFbx);

        int loaded = 0;
        try
        {
            foreach (var row in declared)
            {
                string uri = (string)row[0];
                var properties = (SortedDictionary<string, string>)row[1];
                string modelFileName = Path.GetFileName(uri);

                var modelProperties = new ModelProperties();
                foreach (var kvp in properties)
                {
                    modelProperties.Properties[kvp.Key] = kvp.Value;
                }

                bool ok = Model.TryLoadBaked(modelFileName, modelProperties, out var model);
                Assert.True(ok, $"{modelFileName}: expected to load from "
                                + $"{ModelFileName.Of(modelFileName, properties)} without touching the fbx.");
                Assert.NotNull(model);

                // the same tail ModelCache runs
                model!.FinishBaked(modelProperties);

                Assert.Equal(modelFileName, model.ModelUrl);
                Assert.NotNull(model.ModelNodeTree?.RootNode);
                Assert.NotNull(model.FirstInstanceDescNode);
                Assert.NotNull(model.FirstInstanceDescNode!.InstanceDesc);
                Assert.NotEmpty(model.FirstInstanceDescNode.InstanceDesc!.Meshes);

                // a character model is skinned; a skeleton-less result would render a T-pose
                Assert.NotNull(model.Skeleton);
                Assert.True(model.Skeleton!.NBones > 0, $"{modelFileName}: no bones.");

                ++loaded;
            }
        }
        finally
        {
            Assets.SetAssetImplementation(null);
        }

        Assert.Equal(declared.Count, loaded);
        Assert.Empty(noFbx.RefusedFbx);
        _output.WriteLine($"{loaded} models loaded from bakes, 0 fbx opened.");
    }
}
