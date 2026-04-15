using System;
using System.Runtime.InteropServices;
using engine;

namespace TestRunner;

/// <summary>
/// Test runner for TALE test scripts.
/// Initializes the engine with full module support and runs ExpectEngine test scripts.
/// Environment variable: JOYCE_TEST_SCRIPT (path relative to models/ directory)
/// </summary>
public class TestRunnerMain
{
    private static string _determineResourcePath()
    {
        string dir = System.IO.Directory.GetCurrentDirectory();
        for (int i = 0; i < 8; i++)
        {
            string candidate = System.IO.Path.Combine(dir, "models", "nogame.json");
            if (System.IO.File.Exists(candidate))
                return System.IO.Path.Combine(dir, "models") + System.IO.Path.DirectorySeparatorChar;
            string parent = System.IO.Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        return "./models/";
    }

    private static string _determineGeneratedResourcePath()
    {
        string dir = System.IO.Directory.GetCurrentDirectory();
        for (int i = 0; i < 8; i++)
        {
            string candidate = System.IO.Path.Combine(dir, "nogame", "generated");
            if (System.IO.Directory.Exists(candidate))
                return System.IO.Path.Combine(dir, "nogame", "generated") + System.IO.Path.DirectorySeparatorChar;
            string parent = System.IO.Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        // Fallback
        if (System.IO.Directory.Exists("assets"))
            return "./";
        return "../nogame/generated/";
    }

    private static engine.tale.RoleRegistry _CreateDefaultRoleRegistry()
    {
        var registry = new engine.tale.RoleRegistry();
        // Populate with hardcoded defaults (game authors can override via config)
        registry.Add("worker", new engine.tale.RoleDefinition { Id = "worker", DisplayName = "Worker", DefaultWeight = 0.30f });
        registry.Add("merchant", new engine.tale.RoleDefinition { Id = "merchant", DisplayName = "Merchant", DefaultWeight = 0.13f });
        registry.Add("socialite", new engine.tale.RoleDefinition { Id = "socialite", DisplayName = "Socialite", DefaultWeight = 0.15f });
        registry.Add("drifter", new engine.tale.RoleDefinition { Id = "drifter", DisplayName = "Drifter", DefaultWeight = 0.12f });
        registry.Add("authority", new engine.tale.RoleDefinition { Id = "authority", DisplayName = "Authority", DefaultWeight = 0.1f });
        registry.Add("nightworker", new engine.tale.RoleDefinition { Id = "nightworker", DisplayName = "Night Shift Worker", DefaultWeight = 0.08f });
        registry.Add("hustler", new engine.tale.RoleDefinition { Id = "hustler", DisplayName = "Street Hustler", DefaultWeight = 0.07f });
        registry.Add("reveler", new engine.tale.RoleDefinition { Id = "reveler", DisplayName = "Night Owl", DefaultWeight = 0.05f });
        return registry;
    }

    private static engine.tale.InteractionTypeRegistry _CreateDefaultInteractionTypeRegistry()
    {
        var registry = new engine.tale.InteractionTypeRegistry();
        // Populate with hardcoded defaults
        registry.Add("greet", new engine.tale.InteractionTypeDefinition { Id = "greet", DisplayName = "Greeting", TrustDelta = 0.04f });
        registry.Add("chat", new engine.tale.InteractionTypeDefinition { Id = "chat", DisplayName = "Chat", TrustDelta = 0.06f });
        registry.Add("trade", new engine.tale.InteractionTypeDefinition { Id = "trade", DisplayName = "Trade", TrustDelta = 0.05f });
        registry.Add("help", new engine.tale.InteractionTypeDefinition { Id = "help", DisplayName = "Help", TrustDelta = 0.10f });
        registry.Add("argue", new engine.tale.InteractionTypeDefinition { Id = "argue", DisplayName = "Argue", TrustDelta = -0.08f });
        registry.Add("intimidate", new engine.tale.InteractionTypeDefinition { Id = "intimidate", DisplayName = "Intimidate", TrustDelta = -0.20f });
        registry.Add("rob", new engine.tale.InteractionTypeDefinition { Id = "rob", DisplayName = "Rob", TrustDelta = -0.30f });
        registry.Add("recruit", new engine.tale.InteractionTypeDefinition { Id = "recruit", DisplayName = "Recruit", TrustDelta = 0.16f });
        registry.FinalizeOrder();
        return registry;
    }

    private static engine.tale.RelationshipTierRegistry _CreateDefaultRelationshipTierRegistry()
    {
        var registry = new engine.tale.RelationshipTierRegistry();
        registry.Add("stranger", new engine.tale.RelationshipTierDefinition { Id = "stranger", DisplayName = "Stranger", MinTrust = 0.0f, MaxTrust = 0.15f });
        registry.Add("acquaintance", new engine.tale.RelationshipTierDefinition { Id = "acquaintance", DisplayName = "Acquaintance", MinTrust = 0.15f, MaxTrust = 0.4f });
        registry.Add("friend", new engine.tale.RelationshipTierDefinition { Id = "friend", DisplayName = "Friend", MinTrust = 0.4f, MaxTrust = 0.7f });
        registry.Add("ally", new engine.tale.RelationshipTierDefinition { Id = "ally", DisplayName = "Ally", MinTrust = 0.7f, MaxTrust = 1.0f });
        return registry;
    }

    private static engine.tale.GroupTypeRegistry _CreateDefaultGroupTypeRegistry()
    {
        // For now, return an empty registry - group classification will use fallback logic
        return new engine.tale.GroupTypeRegistry();
    }

    private static void _setupPlatformGraphics()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            GlobalSettings.Set("platform.threeD.API", "OpenGL");
            GlobalSettings.Set("platform.threeD.API.version", "410");
        }
        else
        {
            GlobalSettings.Set("platform.threeD.API", "OpenGL");
            GlobalSettings.Set("platform.threeD.API.version", "430");
        }
        GlobalSettings.Set("engine.NailLogicalFPS", "true");
    }

    public static void Main(string[] args)
    {
        Console.WriteLine("=== TALE Test Runner ===");
        Console.WriteLine($"Timestamp: {System.DateTime.UtcNow:u}");

        string testScript = Environment.GetEnvironmentVariable("JOYCE_TEST_SCRIPT");
        if (string.IsNullOrEmpty(testScript))
        {
            Console.Error.WriteLine("Error: JOYCE_TEST_SCRIPT environment variable not set");
            Environment.Exit(1);
        }
        Console.WriteLine($"Test script: {testScript}");

        _setupPlatformGraphics();

        string resourcePath = _determineResourcePath();
        GlobalSettings.Set("Engine.ResourcePath", resourcePath);
        Console.WriteLine($"Resource path: {System.IO.Path.GetFullPath(resourcePath)}");

        var launchConfig = LaunchConfig.LoadFromStandardLocations(resourcePath);

        string generatedPath = _determineGeneratedResourcePath();
        GlobalSettings.Set("Engine.GeneratedResourcePath", generatedPath);
        Console.WriteLine($"Generated resource path: {System.IO.Path.GetFullPath(generatedPath)}");
        launchConfig.ApplyToGlobalSettings();

        I.Register<engine.joyce.TextureCatalogue>(() => new engine.joyce.TextureCatalogue());

        // Asset implementation MUST be set up before loading game config
        // Creating an instance automatically registers it via the constructor
        // Note: This is a minimal implementation for test running
        // We don't need full asset management, just test script loading
        var assetImpl = new MinimalAssetImplementation();

        string gameConfigPath = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(resourcePath, launchConfig.Game.ConfigPath));
        Console.WriteLine($"Loading game config from: {gameConfigPath}");

        I.Register<engine.casette.Loader>(() =>
        {
            using var streamJson = System.IO.File.OpenRead(gameConfigPath);
            return new engine.casette.Loader(streamJson);
        });

        // Register TALE registries with default configurations
        // (Game authors can override these via config in nogame.json)
        I.Register<engine.tale.RoleRegistry>(() => _CreateDefaultRoleRegistry());
        I.Register<engine.tale.InteractionTypeRegistry>(() => _CreateDefaultInteractionTypeRegistry());
        I.Register<engine.tale.RelationshipTierRegistry>(() => _CreateDefaultRelationshipTierRegistry());
        I.Register<engine.tale.GroupTypeRegistry>(() => _CreateDefaultGroupTypeRegistry());

        // Interpret config BEFORE engine creation (so modules are registered)
        I.Get<engine.casette.Loader>().InterpretConfig();
        Console.WriteLine("Config interpreted. Modules registered.");

        // Create headless engine
        Console.WriteLine("Creating headless engine...");
        var e = Splash.Silk.Platform.EasyCreateHeadless(args, out var _);

        {
            engine.ConsoleLogger logger = new(e);
            engine.Logger.SetLogTarget(logger);
        }

        // Verify that modules were created
        Console.WriteLine($"Engine has {e.GetModules().Count} modules registered");

        // Get and activate TestDriverModule from DI container
        Console.WriteLine("Activating TestDriverModule...");
        try
        {
            var testModule = I.Get<engine.testing.TestDriverModule>();
            testModule.ModuleActivate();
            Console.WriteLine("TestDriverModule activated");
        }
        catch (System.Exception ex)
        {
            Console.Error.WriteLine($"Error activating TestDriverModule: {ex.Message}");
        }

        // Entity-level behavior tests (not DES simulation)
        if (testScript == "entity-behavior-tests")
        {
            Console.WriteLine("Running entity-level behavior bugfix tests...");
            int failures = BehaviorBugfixTests.RunAll(e);
            Console.WriteLine(failures == 0 ? "[TEST] PASS" : "[TEST] FAIL");
            Environment.Exit(failures == 0 ? 0 : 1);
        }

        Console.WriteLine("Starting test execution...");

        bool isPhase6 = testScript.Contains("phase6");

        // Start DES simulation in a background thread (non-blocking)
        // This generates DES events that tests will validate
        System.Threading.Thread simThread = new System.Threading.Thread(() =>
        {
            System.Threading.Thread.Sleep(500);  // Give test framework time to initialize

            if (isPhase6)
            {
                RunPhase6Tests(resourcePath);
                return;
            }

            Console.WriteLine("Setting up DES simulation...");

            // Load storylet library
            string talePath = System.IO.Path.Combine(resourcePath, "tale");
            var library = new engine.tale.StoryletLibrary();
            if (System.IO.Directory.Exists(talePath))
            {
                library.LoadFromDirectory(talePath);
                Console.WriteLine($"Loaded {library.All.Count} storylets");
            }

            // Create minimal spatial model (3 locations)
            var spatial = new engine.tale.SpatialModel();
            spatial.Locations.Add(new engine.tale.Location
            {
                Id = 0,
                Type = "residential",
                Position = new System.Numerics.Vector3(0, 0, 0),
                Capacity = 50,
                QuarterIndex = 0,
                EstateIndex = 0
            });
            spatial.Locations.Add(new engine.tale.Location
            {
                Id = 1,
                Type = "commercial",
                Position = new System.Numerics.Vector3(10, 0, 0),
                Capacity = 50,
                QuarterIndex = 0,
                EstateIndex = 0
            });
            spatial.Locations.Add(new engine.tale.Location
            {
                Id = 2,
                Type = "social_venue",
                Position = new System.Numerics.Vector3(0, 10, 0),
                Capacity = 50,
                QuarterIndex = 0,
                EstateIndex = 0
            });
            spatial.BuildIndex();

            // Create 10 NPC schedules for testing
            var schedules = new System.Collections.Generic.List<engine.tale.NpcSchedule>();
            for (int i = 0; i < 10; i++)
            {
                // Seed escalation-prone NPCs (drifters with low morality)
                bool isEscalationProne = (i == 2 || i == 7) && (i % 5) == 3; // NPCs 2 and 7 are drifters

                schedules.Add(new engine.tale.NpcSchedule
                {
                    NpcId = i,
                    Seed = 1000 + i,
                    Role = new[]{"merchant", "worker", "drifter", "socialite", "authority", "nightworker", "hustler", "reveler"}[i % 8],
                    HomeLocationId = 0,
                    WorkplaceLocationId = 1,
                    SocialVenueIds = new System.Collections.Generic.List<int> { 2 },
                    Properties = new System.Collections.Generic.Dictionary<string, float>
                    {
                        ["fear"] = 0.1f,
                        ["reputation"] = 0.5f,
                        ["anger"] = isEscalationProne ? 0.5f : 0.1f,
                        ["wealth"] = isEscalationProne ? 0.2f : 0.5f,
                        ["hunger"] = 0.3f,
                        ["fatigue"] = 0.3f,
                        ["health"] = 0.8f,
                        ["happiness"] = 0.5f,
                        ["morality"] = isEscalationProne ? 0.25f : 0.7f,
                        ["desperation"] = 0.5f,
                        ["social"] = 0.5f
                    },
                    Trust = new System.Collections.Generic.Dictionary<int, float>()
                });
            }

            // Add player proxy NPC (NPC #10)
            schedules.Add(new engine.tale.NpcSchedule
            {
                NpcId = 10,
                Seed = 1010,
                Role = "worker",
                HomeLocationId = 0,
                WorkplaceLocationId = 1,
                SocialVenueIds = new System.Collections.Generic.List<int> { 2 },
                Properties = new System.Collections.Generic.Dictionary<string, float>
                {
                    ["fear"] = 0.1f,
                    ["reputation"] = 0.5f,
                    ["anger"] = 0.1f,
                    ["wealth"] = 0.5f,
                    ["hunger"] = 0.3f,
                    ["fatigue"] = 0.3f,
                    ["health"] = 0.8f,
                    ["happiness"] = 0.5f,
                    ["morality"] = 0.7f,
                    ["desperation"] = 0.5f,
                    ["social"] = 0.5f
                },
                Trust = new System.Collections.Generic.Dictionary<int, float>()
            });

            // Create and run simulation
            var sim = new engine.tale.DesSimulation();
            var simStart = new System.DateTime(2024, 1, 1, 0, 0, 0);

            // Allow configurable simulation duration via environment variable
            // TALE_SIM_DAYS: number of days to simulate (default: 60 for regression tests)
            string simDaysStr = System.Environment.GetEnvironmentVariable("TALE_SIM_DAYS") ?? "60";
            int simDays = int.TryParse(simDaysStr, out int days) ? days : 60;
            var simEnd = simStart.AddDays(simDays);  // Configurable duration for regression/recalibration

            // Use test event logger that bridges to engine's SubscriptionManager for test framework
            sim.Initialize(spatial, schedules, library, new TestEventLogger(), simStart, seed: 42);
            Console.WriteLine("DES simulation initialized. Running...");

            sim.RunUntil(simEnd);
            Console.WriteLine($"DES simulation completed. {sim.EventsProcessed} events processed.");
        })
        {
            IsBackground = true,
            Name = "SimulationThread"
        };
        simThread.Start();

        // Run engine logical thread (event processing loop)
        // TestDriverModule will process the test and call e.Exit()
        e.ExecuteLogicalThreadOnly();
        e.CallOnPlatformAvailable();

        // Wait for test completion (60 second timeout)
        // TestDriverModule._reportResult() calls e.Exit() and Environment.Exit()
        // The logical thread processes the exit action via its scheduler loop
        System.Threading.Thread.Sleep(60000);

        Console.WriteLine("Test execution timeout or completed after 60 seconds.");
        Environment.Exit(1); // If we reach here, test timed out
    }


    private static void RunPhase6Tests(string resourcePath)
    {
        Console.WriteLine("Setting up Phase 6 tests (population/cluster lifecycle)...");
        var eventQueue = engine.I.Get<engine.news.EventQueue>();

        // Load storylet library
        string talePath = System.IO.Path.Combine(resourcePath, "tale");
        var library = new engine.tale.StoryletLibrary();
        if (System.IO.Directory.Exists(talePath))
        {
            library.LoadFromDirectory(talePath);
            Console.WriteLine($"Loaded {library.All.Count} storylets");
        }

        // Create and initialize TaleManager
        var taleManager = new engine.tale.TaleManager();
        taleManager.Initialize(library, 42);

        // Create a mock ClusterDesc-like spatial model for testing
        // We'll use the SpatialModel to create NPC schedules, then test TaleManager
        var generator = new engine.tale.TalePopulationGenerator();

        // --- Test: Create schedules directly (simulating what PopulateCluster does) ---
        // Use a deterministic seed to create test NPCs
        var rnd = new builtin.tools.RandomSource("test-cluster-0");

        // Create test street points (simulating what a cluster would provide)
        var streetPositions = new System.Collections.Generic.List<System.Numerics.Vector3>();
        for (int i = 0; i < 20; i++)
        {
            streetPositions.Add(new System.Numerics.Vector3(
                rnd.GetFloat() * 400f - 200f,
                2.15f,  // ground height
                rnd.GetFloat() * 400f - 200f));
        }

        // Generate NPC schedules manually (since we don't have a real ClusterDesc)
        int clusterIndex = 0;
        string clusterSeed = "cluster-clusters-testworld-0";
        var schedules = new System.Collections.Generic.List<engine.tale.NpcSchedule>();
        string[] roles = { "worker", "merchant", "socialite", "drifter", "authority", "nightworker", "hustler", "reveler" };

        for (int i = 0; i < 10; i++)
        {
            var npcRnd = new builtin.tools.RandomSource(clusterSeed + "-npc-" + i);
            int npcId = engine.tale.NpcSchedule.MakeNpcId(clusterIndex, i);
            int homeIdx = npcRnd.GetInt(streetPositions.Count - 1);
            int workIdx = npcRnd.GetInt(streetPositions.Count - 1);

            var schedule = new engine.tale.NpcSchedule
            {
                NpcId = npcId,
                Seed = npcId,
                Role = roles[npcRnd.GetInt(4)],
                ClusterIndex = clusterIndex,
                NpcIndex = i,
                HomeLocationId = homeIdx,
                WorkplaceLocationId = workIdx,
                SocialVenueIds = new System.Collections.Generic.List<int> { npcRnd.GetInt(streetPositions.Count - 1) },
                HomePosition = streetPositions[homeIdx],
                WorkplacePosition = streetPositions[workIdx],
                CurrentLocationId = homeIdx,
                Properties = new System.Collections.Generic.Dictionary<string, float>
                {
                    ["hunger"] = 0f,
                    ["health"] = 0.9f + npcRnd.GetFloat() * 0.1f,
                    ["fatigue"] = npcRnd.GetFloat() * 0.2f,
                    ["anger"] = npcRnd.GetFloat() * 0.1f,
                    ["fear"] = 0f,
                    ["trust"] = 0.4f + npcRnd.GetFloat() * 0.2f,
                    ["happiness"] = 0.4f + npcRnd.GetFloat() * 0.3f,
                    ["reputation"] = 0.4f + npcRnd.GetFloat() * 0.2f,
                    ["morality"] = 0.6f + npcRnd.GetFloat() * 0.2f,
                    ["wealth"] = 0.3f + npcRnd.GetFloat() * 0.4f,
                },
                Trust = new System.Collections.Generic.Dictionary<int, float>(),
                HasPlayerDeviation = false,
            };
            schedules.Add(schedule);
        }

        // Register all schedules with TaleManager
        foreach (var s in schedules)
        {
            taleManager.RegisterNpc(s);
        }

        Console.WriteLine($"Phase 6: Created {schedules.Count} NPC schedules for cluster {clusterIndex}");

        // Emit world.cluster.completed (simulating cluster activation)
        eventQueue.Push(new engine.news.Event("world.cluster.completed", "TestCluster0"));
        Console.WriteLine("Phase 6: Emitted world.cluster.completed");

        // Emit tale.cluster.populated
        eventQueue.Push(new engine.news.Event("tale.cluster.populated", clusterIndex.ToString()));
        Console.WriteLine("Phase 6: Emitted tale.cluster.populated");

        // --- Test deviation tracking ---
        // Mark NPC at index 2 as deviated
        var deviatedNpc = schedules[2];
        deviatedNpc.HasPlayerDeviation = true;
        deviatedNpc.Properties["anger"] = 0.9f;
        deviatedNpc.Properties["wealth"] = 0.1f;
        deviatedNpc.Trust[42] = 0.8f;

        // Re-register to update skip mask
        taleManager.RegisterNpc(deviatedNpc);
        Console.WriteLine($"Phase 6: Marked NPC {deviatedNpc.NpcId} (index {deviatedNpc.NpcIndex}) as deviated");

        // --- Test second cluster ---
        int clusterIndex2 = 1;
        string clusterSeed2 = "cluster-clusters-testworld-1";
        var schedules2 = new System.Collections.Generic.List<engine.tale.NpcSchedule>();

        for (int i = 0; i < 8; i++)
        {
            var npcRnd = new builtin.tools.RandomSource(clusterSeed2 + "-npc-" + i);
            int npcId = engine.tale.NpcSchedule.MakeNpcId(clusterIndex2, i);
            int homeIdx = npcRnd.GetInt(streetPositions.Count - 1);
            int workIdx = npcRnd.GetInt(streetPositions.Count - 1);

            var schedule = new engine.tale.NpcSchedule
            {
                NpcId = npcId,
                Seed = npcId,
                Role = roles[npcRnd.GetInt(4)],
                ClusterIndex = clusterIndex2,
                NpcIndex = i,
                HomeLocationId = homeIdx,
                WorkplaceLocationId = workIdx,
                SocialVenueIds = new System.Collections.Generic.List<int> { npcRnd.GetInt(streetPositions.Count - 1) },
                HomePosition = streetPositions[homeIdx],
                WorkplacePosition = streetPositions[workIdx],
                CurrentLocationId = homeIdx,
                Properties = new System.Collections.Generic.Dictionary<string, float>
                {
                    ["hunger"] = 0f, ["health"] = 0.95f, ["fatigue"] = 0.1f,
                    ["anger"] = 0.05f, ["fear"] = 0f, ["trust"] = 0.5f,
                    ["happiness"] = 0.5f, ["reputation"] = 0.5f,
                    ["morality"] = 0.7f, ["wealth"] = 0.4f,
                },
                Trust = new System.Collections.Generic.Dictionary<int, float>(),
                HasPlayerDeviation = false,
            };
            schedules2.Add(schedule);
        }

        foreach (var s in schedules2)
        {
            taleManager.RegisterNpc(s);
        }

        // Emit second cluster events
        eventQueue.Push(new engine.news.Event("world.cluster.completed", "TestCluster1"));
        eventQueue.Push(new engine.news.Event("tale.cluster.populated", clusterIndex2.ToString()));
        Console.WriteLine($"Phase 6: Created {schedules2.Count} NPC schedules for cluster {clusterIndex2}");

        // --- Test AdvanceNpc ---
        var firstNpc = schedules[0];
        var storylet = taleManager.AdvanceNpc(firstNpc.NpcId, new System.DateTime(2024, 1, 1, 8, 0, 0));
        if (storylet != null)
        {
            Console.WriteLine($"Phase 6: AdvanceNpc returned storylet '{storylet.Id}' for NPC {firstNpc.NpcId}");
        }
        else
        {
            Console.WriteLine($"Phase 6: AdvanceNpc returned null (fallback) for NPC {firstNpc.NpcId}");
        }

        // --- Validate NPC ID encoding ---
        foreach (var s in schedules)
        {
            int decoded_cluster = engine.tale.NpcSchedule.GetClusterIndex(s.NpcId);
            int decoded_npc = engine.tale.NpcSchedule.GetNpcIndex(s.NpcId);
            if (decoded_cluster != s.ClusterIndex || decoded_npc != s.NpcIndex)
            {
                Console.Error.WriteLine($"Phase 6 ERROR: NPC ID encoding mismatch for NPC {s.NpcId}");
            }
        }
        Console.WriteLine("Phase 6: NPC ID encoding round-trip validated");

        // --- Validate deviation queries ---
        var deviated = taleManager.GetAllDeviatedNpcs();
        Console.WriteLine($"Phase 6: GetAllDeviatedNpcs returned {deviated.Count} NPCs");
        var skipMask = taleManager.GetDeviationSkipMask(clusterIndex);
        Console.WriteLine($"Phase 6: Skip mask for cluster {clusterIndex}: {(skipMask != null ? skipMask.Count + " entries" : "null")}");

        // --- Validate GetNpcsInFragment ---
        var homeFragment = engine.world.Fragment.PosToIndex3(firstNpc.HomePosition);
        var npcsInFrag = taleManager.GetNpcsInFragment(homeFragment);
        Console.WriteLine($"Phase 6: GetNpcsInFragment({homeFragment.I},{homeFragment.K}) returned {npcsInFrag.Count} NPCs");

        Console.WriteLine("Phase 6 test setup complete.");
    }
}


/// <summary>
/// Minimal asset implementation for test running.
/// Inherits from AAssetImplementation to get automatic registration.
/// Handles both models/ and generated assets paths.
/// </summary>
internal class MinimalAssetImplementation : engine.AAssetImplementation
{
    public override void AddAssociation(string key, string value) { }

    public override System.IO.Stream Open(in string tag)
    {
        string resourcePath = GlobalSettings.Get("Engine.ResourcePath") ?? "./models/";
        string generatedPath = GlobalSettings.Get("Engine.GeneratedResourcePath") ?? "../nogame/generated/";

        // Try multiple paths
        string[] pathsToTry = new[]
        {
            System.IO.Path.Combine(resourcePath, tag),
            System.IO.Path.Combine(resourcePath, "shaders", tag),
            System.IO.Path.Combine(resourcePath, "textures", tag),
            System.IO.Path.Combine(generatedPath, tag),
            tag  // Try as absolute or relative path
        };

        foreach (string fullPath in pathsToTry)
        {
            try
            {
                if (System.IO.File.Exists(fullPath))
                {
                    return System.IO.File.OpenRead(fullPath);
                }
            }
            catch { }
        }

        // If not found anywhere, log warning and return empty stream
        Console.Error.WriteLine($"Warning: Asset not found: {tag}");
        foreach (var path in pathsToTry)
        {
            Console.Error.WriteLine($"  Tried: {System.IO.Path.GetFullPath(path)}");
        }

        // Return empty memory stream to allow engine to continue
        return new System.IO.MemoryStream();
    }

    public override bool Exists(in string tag)
    {
        string resourcePath = GlobalSettings.Get("Engine.ResourcePath") ?? "./models/";
        string generatedPath = GlobalSettings.Get("Engine.GeneratedResourcePath") ?? "../nogame/generated/";

        string[] pathsToTry = new[]
        {
            System.IO.Path.Combine(resourcePath, tag),
            System.IO.Path.Combine(resourcePath, "shaders", tag),
            System.IO.Path.Combine(resourcePath, "textures", tag),
            System.IO.Path.Combine(generatedPath, tag),
            tag
        };

        foreach (string fullPath in pathsToTry)
        {
            if (System.IO.File.Exists(fullPath))
                return true;
        }
        return false;
    }

    public override System.Collections.Generic.IReadOnlyDictionary<string, string> GetAssets()
    {
        return new System.Collections.Generic.Dictionary<string, string>();
    }
}

/// <summary>
/// Event logger that bridges DES simulation events to the engine's EventQueue
/// so that the test framework (JoyceTestEventSource) can receive and validate them.
/// </summary>
internal class TestEventLogger : engine.tale.IEventLogger
{
    private readonly engine.news.EventQueue _eventQueue;

    public bool WantsDaySummary => true;

    public TestEventLogger()
    {
        _eventQueue = engine.I.Get<engine.news.EventQueue>();
    }

    public void LogNpcCreated(int npcId, int seed, string role, int homeLocationId,
        int workplaceLocationId, System.Collections.Generic.List<int> socialVenues,
        System.Collections.Generic.Dictionary<string, float> props, System.DateTime gameTime)
    {
        _eventQueue.Push(new engine.news.Event("npc_created", npcId.ToString()));
    }

    public void LogNodeArrival(int npcId, string storylet, int locationId, string locationType,
        System.DateTime gameTime, int day, System.Collections.Generic.Dictionary<string, float> props,
        System.Collections.Generic.Dictionary<string, float> deltas)
    {
        _eventQueue.Push(new engine.news.Event("node_arrival", storylet));
    }

    public void LogEncounter(int npcA, int npcB, string interactionType, int locationId,
        string locationType, System.DateTime gameTime, int day,
        float trustBefore, float trustAfter)
    {
        _eventQueue.Push(new engine.news.Event("encounter", $"{npcA}:{npcB}"));
    }

    public void LogRelationshipChanged(int npcA, int npcB, string oldTier, string newTier,
        float trust, int interactionCount, System.DateTime gameTime, int day)
    {
        _eventQueue.Push(new engine.news.Event("relationship_changed", $"{npcA}:{npcB}"));
    }

    public void LogDaySummary(int npcId, int day, int storyletsCompleted, int encounters,
        System.Collections.Generic.Dictionary<string, float> props, System.Collections.Generic.Dictionary<int, float> topRelationships)
    {
        _eventQueue.Push(new engine.news.Event("day_summary", day.ToString()));
    }

    public void LogRequestEmitted(int requestId, int requesterId, string requestType, int locationId,
        float urgency, int timeoutMinutes, string storyletContext, System.DateTime gameTime, int day)
    {
        _eventQueue.Push(new engine.news.Event("request_emitted", requestId.ToString()));
    }

    public void LogRequestClaimed(int requestId, int claimerId, System.DateTime gameTime, int day)
    {
        _eventQueue.Push(new engine.news.Event("request_claimed", requestId.ToString()));
    }

    public void LogSignalEmitted(int signalId, int requestId, string signalType, int sourceNpcId,
        System.DateTime gameTime, int day)
    {
        _eventQueue.Push(new engine.news.Event("signal_emitted", signalId.ToString()));
    }

    public void LogInterruptFired(int npcId, string interruptStorylet, string scope,
        string pausedStorylet, int day, System.DateTime gameTime)
    {
        _eventQueue.Push(new engine.news.Event("interrupt_fired", npcId.ToString()));
    }

    public void LogStoryletResumed(int npcId, string storylet, int day, System.DateTime gameTime)
    {
        _eventQueue.Push(new engine.news.Event("storylet_resumed", npcId.ToString()));
    }

    public void LogEscalationTriggered(int npcId, string escalationId, int targetNpcId,
        int day, System.DateTime gameTime)
    {
        _eventQueue.Push(new engine.news.Event("escalation_triggered", $"{npcId}:{escalationId}"));
    }

    public void LogGangFormed(int groupId, System.Collections.Generic.List<int> members, string groupType,
        int day, System.DateTime gameTime)
    {
        _eventQueue.Push(new engine.news.Event("gang_formed", groupId.ToString()));
    }

    public void Flush() { }
}
