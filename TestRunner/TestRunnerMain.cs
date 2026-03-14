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

        I.Get<engine.casette.Loader>().InterpretConfig();

        // Create headless engine
        Console.WriteLine("Creating headless engine...");
        var e = Splash.Silk.Platform.EasyCreateHeadless(args, out var _);

        {
            engine.ConsoleLogger logger = new(e);
            engine.Logger.SetLogTarget(logger);
        }

        Console.WriteLine("Starting test execution...");
        e.Execute();

        Console.WriteLine("Test execution completed.");
        Environment.Exit(0);
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
