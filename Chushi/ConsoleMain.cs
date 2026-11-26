using System.Diagnostics;
using engine;
using engine.joyce;
using Mazu;
using static engine.Logger;

namespace Chushi;

public class ConsoleMain
{
    public static void Main(string[] args)
    {
        Trace($"Called with args: { string.Join(",", args) }");
        // System.Environment.SetEnvironmentVariable("ALSOFT_LOGLEVEL", "3");

        var cwd = System.IO.Directory.GetCurrentDirectory();
        string jsonPath;
        /*
         * Setup globals and statics
         */
        engine.GlobalSettings.Set("platform.threeD.API", "OpenGL");
        engine.GlobalSettings.Set("platform.threeD.API.version", "430");
        engine.GlobalSettings.Set("engine.NailLogicalFPS", "true");
        string strResourcePath;
        if (Directory.Exists("assets"))
        {
            /*
             * This is when we start installed on windows.
             */
            strResourcePath = "./assets/";
            jsonPath = "./";
        }
        else
        {
            /*
             * Search UPWARD for the content root rather than guessing how far down we are.
             *
             * What stood here was three escalating fallbacks - four, then five, then six
             * levels of ".." - each with a comment naming the one launch method it had been
             * observed to fix ("from the debugger on windows", "when called from jetbrains
             * compiler", "in Chushi on windows"). That list is the shape of the bug: the
             * depth is not a property of this code, it is a property of where the process
             * was started, so every new launch path needed another rung and any tree at an
             * unexpected depth fell off the end into "Running in unknown environment".
             *
             * engine.GameRoot looks for models/nogame.json upward from the current
             * directory and from the assembly location, which answers all of those cases
             * and the ones nobody has hit yet.
             */
            string? chushiRoot = engine.GameRoot.Find();
            if (null == chushiRoot)
            {
                Console.Error.WriteLine(
                    $"Could not locate the content root. Searched upward from '{cwd}' and from "
                    + $"'{AppContext.BaseDirectory}' for models/nogame.json, "
                    + "models/game.launch.json or Karawan.sln.");
                System.Environment.Exit(-1);
                return;
            }

            strResourcePath = Path.Combine(chushiRoot, "models") + Path.DirectorySeparatorChar;
            jsonPath = strResourcePath;
        }
        engine.GlobalSettings.Set("Engine.ResourcePath", strResourcePath);
        Trace($"Using resource path {strResourcePath}, json path {jsonPath}");

        {
            string userRWPath = System.Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData);
            string vendorRWPath = Path.Combine(userRWPath, "nassau records");
            string appRWPath = Path.Combine(vendorRWPath, "silicondesert2");
            System.IO.Directory.CreateDirectory(appRWPath);
            engine.GlobalSettings.Set("Engine.RWPath", appRWPath);
        }

        // TXWTODO: How to know the underlying platform does have touch events?
        engine.GlobalSettings.Set("splash.touchControls", "false");
        engine.GlobalSettings.Set("platform.suspendOnUnfocus", "false");
        engine.GlobalSettings.Set("platform.initialZoomState", "0");

        engine.GlobalSettings.Set("joyce.CompileMode", "true");


        /*
         * WP-4.4: fbx import is build-time only, so the importer no longer ships
         * inside Joyce and ModelCache cannot call it directly. Chushi is one of
         * the two processes that still has it, and installs it here. Without this
         * line the model bake would find no importer and say so.
         */
        engine.joyce.ModelCache.FbxLoader = builtin.loader.Fbx.LoadModelInstance;

        I.Register<engine.joyce.TextureCatalogue>(() => new engine.joyce.TextureCatalogue());
        I.Register<engine.joyce.ModelCache>(() => new engine.joyce.ModelCache());
        I.Register<engine.joyce.AnimationPackRegistry>(() => new engine.joyce.AnimationPackRegistry());

        /*
         * Bootstrap game by directly reading game config, setting up
         * asset implementation with the pathes.
         */
        Console.WriteLine($"CWD is {cwd}");

        I.Register<engine.casette.Loader>(() => {
            using (var streamJson =
                   File.OpenRead(
                       Path.Combine(
                           engine.GlobalSettings.Get("Engine.ResourcePath"),
                           jsonPath + "nogame.json")))
            {
                return new engine.casette.Loader(streamJson);
            }
        });
        var iassetDesktop = new Chushi.AssetImplementation();
        iassetDesktop.WithLoader();

        /*
         * Register TALE registries with default content. ScenarioCompiler runs
         * the DES inside Chushi and DesSimulation.SeedNpc fetches RoleRegistry
         * via I.Get<>(), so the singletons must exist before any scenario task
         * fires. Mirrors TestRunner/TestRunnerMain.cs.
         */
        I.Register<engine.tale.RoleRegistry>(_CreateDefaultRoleRegistry);
        I.Register<engine.tale.InteractionTypeRegistry>(_CreateDefaultInteractionTypeRegistry);
        I.Register<engine.tale.RelationshipTierRegistry>(_CreateDefaultRelationshipTierRegistry);
        I.Register<engine.tale.GroupTypeRegistry>(_CreateDefaultGroupTypeRegistry);

        I.Get<engine.casette.Loader>().InterpretConfig();


        I.Register<engine.Engine>(() => new engine.Engine(null));
        engine.Engine e = I.Get<engine.Engine>();
        e.SetupDone();
        e.PlatformSetupDone();

        Trace($"Starting engine...");
        e.Execute();
        Trace($"Running compilation tasks...");
        List<Task> listTasks = new();

        /*
         * KAR-501: the identity of THIS baker. A file baked by another one - older code, a
         * different importer, another runtime - is re-baked, whatever the timestamps say.
         * The stamps live in one bake-manifest.json per output directory.
         */
        string bakerHash = builtin.baking.BakerIdentity.HashDirectory(AppContext.BaseDirectory);
        string modelStamp = builtin.baking.BakerIdentity.ModelStamp(bakerHash);
        string animationStamp = builtin.baking.BakerIdentity.AnimationStamp(bakerHash);
        string scenarioStamp = builtin.baking.BakerIdentity.ScenarioStamp();
        Trace($"Baker identity: {bakerHash} (from {AppContext.BaseDirectory}); stamps {modelStamp}, {animationStamp}, {scenarioStamp}.");
        Dictionary<string, builtin.baking.BakeManifest> manifests = new();
        builtin.baking.BakeManifest manifestFor(string directory)
        {
            string key = Path.GetFullPath(directory);
            if (!manifests.TryGetValue(key, out var m))
            {
                m = builtin.baking.BakeManifest.Load(key);
                manifests[key] = m;
            }
            return m;
        }
        void saveManifests()
        {
            foreach (var (dir, m) in manifests) m.Save(dir);
        }

        /*
         * Decide one file. A file to be baked loses its stamp now (saved before any bake
         * starts), so a bake that dies half-way leaves no stamp vouching for half a file.
         */
        bool needsBake(string kind, string directory, string fileName, string? staleReason, string stamp,
            bool adoptUnstamped = false)
        {
            var manifest = manifestFor(directory);
            var recorded = manifest.StampOf(fileName);
            var decision = builtin.baking.BakeDecision.Of(staleReason, recorded, stamp, adoptUnstamped);
            Trace($"{kind}: {(decision.Bake ? "baking" : "skipping")} {fileName} - {decision.Reason}.");
            if (decision.Bake) manifest.Forget(fileName);
            else if (recorded == null) manifest.Record(fileName, stamp);
            return decision.Bake;
        }

        var availableAnims = iassetDesktop.AvailableAnimations;
        foreach (var strAnimKey in availableAnims)
        {
            string uriModel;
            string uriAnimations;
            int idxSemi = strAnimKey.IndexOf(';');
            if (idxSemi >= 0)
            {
                uriModel = strAnimKey.Substring(0, idxSemi);
                uriAnimations = strAnimKey.Substring(idxSemi + 1);
            }
            else
            {
                uriModel = strAnimKey;
                uriAnimations = "";
            }

            string outputDirectory = Path.Combine(
                cwd,
                GlobalSettings.Get("Engine.ResourcePath"),
                "generated");
            if (args.Length >= 4)
            {
                outputDirectory = Path.Combine(args[3],args[2]);
            }

            // Check if animation is already up-to-date (makefile-style incremental build).
            string fileName = builtin.baking.ModelAnimationCollectionReader.ModelAnimationCollectionFileName(
                Path.GetFileName(uriModel), uriAnimations);
            string filePath = Path.GetFullPath(Path.Combine(outputDirectory, fileName));
            if (!needsBake("AnimationCompiler", outputDirectory, fileName,
                    _IsAnimationStale(filePath, uriModel, uriAnimations, Path.GetFullPath(strResourcePath)),
                    animationStamp))
            {
                continue;
            }

            AnimationCompiler comp = new()
            {
                ModelUrl = uriModel,
                AnimationUrls = uriAnimations,
                OutputDirectory = outputDirectory
            };

            var animManifest = manifestFor(outputDirectory);
            listTasks.Add(Task.Run(async () =>
            {
                await comp.Compile();
                animManifest.Record(fileName, animationStamp);
            }));
        }

        /*
         * Model compilation loop (WP-4.2). Mirrors the animation loop above: one
         * task per declared model, same Task.Run pool, same generated/ directory.
         * Files are named mo-{hash} so they sit alongside ac-{hash} and sc-{hash}.
         *
         * The set comes from /resources/list entries typed "model", so a model
         * that ships is a model that bakes, by construction.
         */
        {
            string modelOutputDirectory = Path.Combine(
                cwd,
                GlobalSettings.Get("Engine.ResourcePath"),
                "generated");
            if (args.Length >= 4)
            {
                modelOutputDirectory = Path.Combine(args[3], args[2]);
            }

            var availableModels = iassetDesktop.AvailableModels;
            Trace($"Model compilation: Found {availableModels.Count} models to bake.");
            foreach (var req in availableModels)
            {
                string fileName = builtin.baking.ModelFileName.Of(req.ModelFileName, req.Properties);
                string filePath = Path.GetFullPath(Path.Combine(modelOutputDirectory, fileName));
                if (!needsBake("ModelCompiler", modelOutputDirectory, fileName,
                        _IsModelStale(filePath, req.Uri, Path.GetFullPath(strResourcePath)),
                        modelStamp))
                {
                    continue;
                }

                var captured = req;
                var modelManifest = manifestFor(modelOutputDirectory);
                var modelComp = new ModelCompiler
                {
                    ModelUrl = captured.ModelFileName,
                    Properties = new SortedDictionary<string, string>(captured.Properties),
                    OutputDirectory = modelOutputDirectory
                };
                /*
                 * async/await, not a void lambda. A block-bodied lambda returning
                 * void binds to Task.Run(Action), which starts Compile() and then
                 * reports completion the moment it hits its first await - so
                 * Task.WaitAll below returns while models are still being written
                 * and the process exits mid-bake. Observed: 10 of 13 files.
                 */
                listTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await modelComp.Compile();
                        modelManifest.Record(fileName, modelStamp);
                    }
                    catch (Exception ex)
                    {
                        Trace($"ModelCompiler ERROR: {captured.Uri} failed: {ex.GetType().Name}: {ex.Message}");
                        throw;
                    }
                }));
            }
        }

        /*
         * Scenario compilation loop. Mirrors the animation loop above: one task
         * per (category, index) pair, dispatched to the same Task.Run pool, all
         * writing into the same nogame/generated/ directory. Files are named
         * sc-{hash} so they sit alongside ac-{hash} animation collections.
         */
        string scenarioOutputDirectory = Path.Combine(
            cwd,
            GlobalSettings.Get("Engine.ResourcePath"),
            "generated");
        if (args.Length >= 4)
        {
            scenarioOutputDirectory = Path.Combine(args[3], args[2]);
        }
        var availableScenarios = iassetDesktop.AvailableScenarios;
        Trace($"Scenario compilation: Found {availableScenarios.Count} scenarios to bake.");
        foreach (var scenario in availableScenarios)
        {
            Trace($"  - Scenario: {scenario.CategoryName}/{scenario.Index} (seed={scenario.Seed}, npcCount={scenario.NpcCount}, simulationDays={scenario.SimulationDays})");
        }
        foreach (var req in availableScenarios)
        {
            // Check if scenario is already up-to-date (makefile-style incremental build).
            // Skip compilation if output exists and all sources are older.
            string fileName = engine.tale.bake.ScenarioFileName.Of(req.CategoryName, req.Index, req.Seed);
            string filePath = Path.GetFullPath(Path.Combine(scenarioOutputDirectory, fileName));
            if (!needsBake("ScenarioCompiler", scenarioOutputDirectory, fileName,
                    _IsScenarioStale(filePath, Path.GetFullPath(strResourcePath)),
                    scenarioStamp, adoptUnstamped: BakeVersionsScenarioAdoptsUnstamped))
            {
                continue;
            }

            // Capture-by-value for the closure.
            var captured = req;
            var scenarioManifest = manifestFor(scenarioOutputDirectory);
            var scenarioComp = new engine.tale.bake.ScenarioCompiler
            {
                CategoryName = captured.CategoryName,
                Index = captured.Index,
                NpcCount = captured.NpcCount,
                Seed = captured.Seed,
                SimulationDays = captured.SimulationDays,
                OutputDirectory = scenarioOutputDirectory
            };
            listTasks.Add(Task.Run(() =>
            {
                try
                {
                    scenarioComp.Compile();
                    scenarioManifest.Record(fileName, scenarioStamp);
                }
                catch (Exception ex)
                {
                    Trace($"ScenarioCompiler ERROR: {captured.CategoryName}/{captured.Index} failed: {ex.GetType().Name}: {ex.Message}");
                    throw;  // Re-throw so Task.WaitAll catches it
                }
            }));
        }

        // The stamps of the files about to be baked are gone before any of them is written.
        saveManifests();

        try
        {
            // Wait with dynamic timeout based on number of scenarios:
            // - Small (40-80 NPCs): ~10 seconds each
            // - Medium (150-250 NPCs): ~30-60 seconds each
            // - Large (400-600 NPCs, 365 days): 3-8 minutes each
            // Total: ~60 minutes for first build, then incremental builds skip already-compiled scenarios
            int timeoutMinutesPerScenario = listTasks.Count <= 10 ? 5 : 8;  // More time for large batches
            bool completed = Task.WaitAll(listTasks.ToArray(), TimeSpan.FromMinutes(timeoutMinutesPerScenario * listTasks.Count));
            if (!completed)
            {
                throw new TimeoutException($"Scenario compilation timeout: not all {listTasks.Count} tasks completed within {5 * listTasks.Count} minutes");
            }
            Trace($"Done running compilation tasks.");
        }
        catch (AggregateException ex)
        {
            Trace($"Error: {listTasks.Count} tasks started, but some failed:");
            foreach (var innerEx in ex.InnerExceptions)
            {
                Trace($"  - {innerEx.GetType().Name}: {innerEx.Message}");
                if (innerEx.InnerException != null)
                {
                    Trace($"    Inner: {innerEx.InnerException.Message}");
                }
            }
            throw;
        }
        finally
        {
            // Whatever finished gets its stamp, even if another bake failed: a failed scenario
            // must not cost the other scenarios their stamps (an hour of re-baking).
            saveManifests();
        }

        /*
         * TALE-SOCIAL Phase D5: walk the just-written sc-{hash} files, compute
         * per-scenario statistics, aggregate by category, and emit
         * scenario-statistics.json next to the bake artifacts. Cheap (each
         * read is a small JSON parse) and runs serially after Task.WaitAll
         * so it sees a complete bake. Failures here log a warning instead of
         * breaking the build — the statistics are observability, not part of
         * the runtime contract.
         */
        try
        {
            var perScenario = new List<engine.tale.bake.PerScenarioStats>();
            foreach (var req in availableScenarios)
            {
                string fileName = engine.tale.bake.ScenarioFileName.Of(req.CategoryName, req.Index, req.Seed);
                string filePath = System.IO.Path.Combine(scenarioOutputDirectory, fileName);
                if (!System.IO.File.Exists(filePath))
                {
                    Trace($"Statistics: skipping {req.CategoryName}/{req.Index} — bake file missing at {filePath}.");
                    continue;
                }
                try
                {
                    using var fs = System.IO.File.OpenRead(filePath);
                    var scenario = engine.tale.bake.ScenarioExporter.ReadFromStream(fs);
                    if (scenario != null)
                    {
                        perScenario.Add(engine.tale.bake.ScenarioStatisticsBuilder.From(scenario));
                    }
                }
                catch (System.Exception ex)
                {
                    Trace($"Statistics: failed to read {filePath}: {ex.Message}");
                }
            }
            if (perScenario.Count > 0)
            {
                var report = engine.tale.bake.ScenarioStatisticsBuilder.BuildReport(perScenario);
                string statsPath = System.IO.Path.Combine(scenarioOutputDirectory, "scenario-statistics.json");
                engine.tale.bake.ScenarioStatisticsBuilder.WriteReport(report, statsPath);
                Trace($"Statistics: wrote {statsPath} ({perScenario.Count} scenarios across {report.Categories.Count} categories).");
            }
            else
            {
                Trace("Statistics: no scenarios to summarize; skipping scenario-statistics.json.");
            }
        }
        catch (System.Exception ex)
        {
            Trace($"Statistics: report generation failed (non-fatal): {ex}");
        }

        Trace($"Stopping engine...");
        e.Exit();
        Trace($"Engine stopped.");

        System.Environment.Exit(0);
    }


    /**
     * Scenario bakes made before stamps existed are adopted at BakeVersions.Scenario 1
     * instead of costing every machine an hour; from version 2 on they must carry a stamp.
     */
    private static bool BakeVersionsScenarioAdoptsUnstamped => builtin.baking.BakeVersions.Scenario == 1;


    /// <summary>
    /// Check if a pre-baked animation file is up-to-date (makefile-style incremental build).
    /// Returns why the output is stale (missing, or a source is newer), null if its inputs are older.
    /// Whether the BAKER changed is decided separately by the stamp (KAR-501).
    /// </summary>
    private static string? _IsAnimationStale(string outputPath, string modelUrl, string animationUrls, string resourcePath)
    {
        if (!File.Exists(outputPath))
        {
            return "Output missing, needs compilation";
        }

        var outputTime = File.GetLastWriteTimeUtc(outputPath);

        // Check model file modification time
        string modelPath = Path.Combine(resourcePath, modelUrl);
        if (File.Exists(modelPath))
        {
            var modelTime = File.GetLastWriteTimeUtc(modelPath);
            if (modelTime > outputTime)
            {
                return "Model is newer, needs recompilation";
            }
        }

        // Check animation files modification time (semicolon-separated list)
        if (!string.IsNullOrWhiteSpace(animationUrls))
        {
            var animFiles = animationUrls.Split(';');
            foreach (var animFile in animFiles)
            {
                if (string.IsNullOrWhiteSpace(animFile))
                    continue;
                string animPath = Path.Combine(resourcePath, animFile.Trim());
                if (File.Exists(animPath))
                {
                    var animTime = File.GetLastWriteTimeUtc(animPath);
                    if (animTime > outputTime)
                    {
                        return "Animation file is newer, needs recompilation";
                    }
                }
            }
        }

        // Check animation config file
        string animConfigPath = Path.Combine(resourcePath, "nogame.animations.json");
        if (File.Exists(animConfigPath))
        {
            var configTime = File.GetLastWriteTimeUtc(animConfigPath);
            if (configTime > outputTime)
            {
                return "Config is newer, needs recompilation";
            }
        }

        return null;  // All sources are older or missing, output is up-to-date
    }

    /// <summary>
    /// Check if a baked model file is up-to-date (makefile-style incremental build).
    /// Returns why the output is stale (missing, or older than the model source or the
    /// resource declaration that decides how it is baked), null otherwise. The baker itself
    /// is checked separately by the stamp (KAR-501).
    /// </summary>
    private static string? _IsModelStale(string outputPath, string modelUri, string resourcePath)
    {
        if (!File.Exists(outputPath))
        {
            return "Output missing, needs compilation";
        }

        var outputTime = File.GetLastWriteTimeUtc(outputPath);

        string modelPath = Path.Combine(resourcePath, modelUri);
        if (File.Exists(modelPath))
        {
            if (File.GetLastWriteTimeUtc(modelPath) > outputTime)
            {
                return "Model is newer, needs recompilation";
            }
        }

        /*
         * The resource declaration carries the type and the load properties, and
         * the properties are part of the bake identity. Editing them normally
         * changes the file NAME, which the File.Exists check above already catches
         * - but removing a property can equally well restore an older name, so the
         * declaration's timestamp is checked too.
         */
        string resourceConfigPath = Path.Combine(resourcePath, "nogame.resources.json");
        if (File.Exists(resourceConfigPath))
        {
            if (File.GetLastWriteTimeUtc(resourceConfigPath) > outputTime)
            {
                return "Declaration is newer, needs recompilation";
            }
        }

        return null;
    }

    /// <summary>
    /// Check if a pre-baked scenario file is up-to-date (makefile-style incremental build).
    /// Returns why the output is stale (missing, or older than a game config or TALE
    /// definition file), null otherwise. Code changes are covered only by
    /// BakeVersions.Scenario (KAR-501).
    /// </summary>
    private static string? _IsScenarioStale(string outputPath, string resourcePath)
    {
        // Conservative check: only skip if we're 100% sure output is up-to-date.
        // If anything is in doubt, recompile to ensure we don't miss files.

        if (!File.Exists(outputPath))
        {
            return "Output missing, needs compilation";
        }

        var outputTime = File.GetLastWriteTimeUtc(outputPath);

        // CRITICAL: Check that config files exist. If they don't, we can't determine
        // if the output is truly up-to-date, so we must recompile.
        var mainConfigFiles = new[]
        {
            Path.Combine(resourcePath, "nogame.json"),
            Path.Combine(resourcePath, "nogame.scenarios.json"),
            /*
             * The bake types its groups by these rules (mirrored into
             * _CreateDefaultGroupTypeRegistry); a change must re-bake, or the scenarios keep the
             * old types (KAR-477). Code changes to the selector or the classifier are not
             * seen here - bump builtin.baking.BakeVersions.Scenario after one (KAR-501).
             */
            Path.Combine(resourcePath, "nogame.group-types.json"),
        };

        foreach (var configFile in mainConfigFiles)
        {
            if (!File.Exists(configFile))
            {
                // Config file missing - we can't verify up-to-date status, must recompile
                return "config file missing, cannot judge";
            }

            var configTime = File.GetLastWriteTimeUtc(configFile);
            if (configTime > outputTime)
            {
                return "Config is newer, needs recompilation";
            }
        }

        // Check modification time of all TALE definition files
        string taleDir = Path.Combine(resourcePath, "tale");
        if (Directory.Exists(taleDir))
        {
            var taleFiles = Directory.GetFiles(taleDir, "*.json", SearchOption.AllDirectories);
            if (taleFiles.Length == 0)
            {
                // No TALE files found - uncertain state, must recompile
                return "no TALE files found, cannot judge";
            }

            foreach (var taleFile in taleFiles)
            {
                var taleTime = File.GetLastWriteTimeUtc(taleFile);
                if (taleTime > outputTime)
                {
                    return "TALE definition is newer, needs recompilation";
                }
            }
        }
        else
        {
            // Tale directory missing - uncertain state, must recompile
            return "tale directory missing, cannot judge";
        }

        // All checks passed - output appears to be up-to-date
        return null;
    }

    /*
     * Default TALE registry factories. These mirror TestRunner/TestRunnerMain.cs
     * (intentional duplication: Chushi must remain a self-contained build tool
     * and pulling in TestRunner as a dependency would invert the layering).
     * Game authors override these via the JSON config; the defaults exist so
     * Chushi can run before that override path is wired in.
     */

    private static engine.tale.RoleRegistry _CreateDefaultRoleRegistry()
    {
        var r = new engine.tale.RoleRegistry();
        r.Add("worker",      new engine.tale.RoleDefinition { Id = "worker",      DisplayName = "Worker",             DefaultWeight = 0.30f });
        r.Add("merchant",    new engine.tale.RoleDefinition { Id = "merchant",    DisplayName = "Merchant",           DefaultWeight = 0.13f });
        r.Add("socialite",   new engine.tale.RoleDefinition { Id = "socialite",   DisplayName = "Socialite",          DefaultWeight = 0.15f });
        r.Add("drifter",     new engine.tale.RoleDefinition { Id = "drifter",     DisplayName = "Drifter",            DefaultWeight = 0.12f });
        r.Add("authority",   new engine.tale.RoleDefinition { Id = "authority",   DisplayName = "Authority",          DefaultWeight = 0.10f });
        r.Add("nightworker", new engine.tale.RoleDefinition { Id = "nightworker", DisplayName = "Night Shift Worker", DefaultWeight = 0.08f });
        r.Add("hustler",     new engine.tale.RoleDefinition { Id = "hustler",     DisplayName = "Street Hustler",     DefaultWeight = 0.07f });
        r.Add("reveler",     new engine.tale.RoleDefinition { Id = "reveler",     DisplayName = "Night Owl",          DefaultWeight = 0.05f });
        return r;
    }

    private static engine.tale.InteractionTypeRegistry _CreateDefaultInteractionTypeRegistry()
    {
        var r = new engine.tale.InteractionTypeRegistry();
        r.Add("greet",      new engine.tale.InteractionTypeDefinition { Id = "greet",      DisplayName = "Greeting",   TrustDelta =  0.04f });
        r.Add("chat",       new engine.tale.InteractionTypeDefinition { Id = "chat",       DisplayName = "Chat",       TrustDelta =  0.06f });
        r.Add("trade",      new engine.tale.InteractionTypeDefinition { Id = "trade",      DisplayName = "Trade",      TrustDelta =  0.05f });
        r.Add("help",       new engine.tale.InteractionTypeDefinition { Id = "help",       DisplayName = "Help",       TrustDelta =  0.10f });
        r.Add("argue",      new engine.tale.InteractionTypeDefinition { Id = "argue",      DisplayName = "Argue",      TrustDelta = -0.08f });
        r.Add("intimidate", new engine.tale.InteractionTypeDefinition { Id = "intimidate", DisplayName = "Intimidate", TrustDelta = -0.20f });
        r.Add("rob",        new engine.tale.InteractionTypeDefinition { Id = "rob",        DisplayName = "Rob",        TrustDelta = -0.30f });
        r.Add("recruit",    new engine.tale.InteractionTypeDefinition { Id = "recruit",    DisplayName = "Recruit",    TrustDelta =  0.16f });
        r.FinalizeOrder();
        return r;
    }

    private static engine.tale.RelationshipTierRegistry _CreateDefaultRelationshipTierRegistry()
    {
        var r = new engine.tale.RelationshipTierRegistry();
        r.Add("stranger",     new engine.tale.RelationshipTierDefinition { Id = "stranger",     DisplayName = "Stranger",     MinTrust = 0.0f, MaxTrust = 0.15f });
        r.Add("acquaintance", new engine.tale.RelationshipTierDefinition { Id = "acquaintance", DisplayName = "Acquaintance", MinTrust = 0.15f, MaxTrust = 0.4f });
        r.Add("friend",       new engine.tale.RelationshipTierDefinition { Id = "friend",       DisplayName = "Friend",       MinTrust = 0.4f, MaxTrust = 0.7f });
        r.Add("ally",         new engine.tale.RelationshipTierDefinition { Id = "ally",         DisplayName = "Ally",         MinTrust = 0.7f, MaxTrust = 1.0f });
        return r;
    }

    private static engine.tale.GroupTypeRegistry _CreateDefaultGroupTypeRegistry()
    {
        // Mirrors models/nogame.group-types.json. An empty registry would make
        // GroupTypeRegistry.ClassifyGroup fall through to "social" for every
        // group, which is what all baked scenarios shipped with before this
        // factory existed.
        var r = new engine.tale.GroupTypeRegistry();
        r.Add("patrol_unit", new engine.tale.GroupTypeDefinition
        {
            Id = "patrol_unit", DisplayName = "Patrol Unit", Priority = 100,
            Rules = { new engine.tale.GroupClassificationRule
                { Type = "authority_threshold", Parameters = { ["minimum_ratio"] = 0.5f } } }
        });
        r.Add("criminal", new engine.tale.GroupTypeDefinition
        {
            Id = "criminal", DisplayName = "Criminal Syndicate", Priority = 90,
            Rules = { new engine.tale.GroupClassificationRule
                { Type = "combined_threshold", Parameters = { ["wealth_max"] = 0.3f, ["morality_max"] = 0.4f } } }
        });
        r.Add("trade", new engine.tale.GroupTypeDefinition
        {
            Id = "trade", DisplayName = "Trading Partnership", Priority = 80,
            Rules =
            {
                new engine.tale.GroupClassificationRule
                    { Type = "trader_threshold", Parameters = { ["minimum_ratio"] = 0.4f } },
                new engine.tale.GroupClassificationRule
                    { Type = "combined_threshold", Parameters = { ["wealth_min"] = 0.5f } }
            }
        });
        r.Add("social", new engine.tale.GroupTypeDefinition
        {
            Id = "social", DisplayName = "Social Circle", Priority = 0
        });
        return r;
    }
}