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

        string generatedPath = System.IO.Directory.Exists("assets") ? "./" : "../nogame/generated";
        GlobalSettings.Set("Engine.GeneratedResourcePath", generatedPath);
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

        // Manually create and activate TestDriverModule
        Console.WriteLine("Creating TestDriverModule...");
        try
        {
            var testModule = new engine.testing.TestDriverModule();
            testModule.SetEngine(e);
            testModule.ModuleActivate();
            Console.WriteLine("TestDriverModule created and activated");
            e.AddModule(testModule);
        }
        catch (System.Exception ex)
        {
            Console.Error.WriteLine($"Error with TestDriver: {ex}");
        }

        Console.WriteLine("Starting test execution...");

        // Start DES simulation in a background thread (non-blocking)
        // This generates DES events that tests will validate
        System.Threading.Thread simThread = new System.Threading.Thread(() =>
        {
            System.Threading.Thread.Sleep(500);  // Give test framework time to initialize

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
                schedules.Add(new engine.tale.NpcSchedule
                {
                    NpcId = i,
                    Seed = 1000 + i,
                    Role = new[]{"merchant", "worker", "drifter", "socialite", "authority"}[i % 5],
                    HomeLocationId = 0,
                    WorkplaceLocationId = 1,
                    SocialVenueIds = new System.Collections.Generic.List<int> { 2 },
                    Properties = new System.Collections.Generic.Dictionary<string, float>
                    {
                        ["desperation"] = 0.5f,
                        ["morality"] = 0.5f,
                        ["social"] = 0.5f
                    },
                    Trust = new System.Collections.Generic.Dictionary<int, float>()
                });
            }

            // Create and run simulation
            var sim = new engine.tale.DesSimulation();
            var simStart = new System.DateTime(2024, 1, 1, 0, 0, 0);
            var simEnd = simStart.AddDays(1);  // Run for 1 day

            sim.Initialize(spatial, schedules, library, new engine.tale.NullEventLogger(), simStart, seed: 42);
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
