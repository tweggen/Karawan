using System;
using System.IO;
using System.Runtime.InteropServices;
using engine;
using Silk.NET.Windowing;
using Silk.NET.Maths;

namespace Karawan.GenericLauncher;

/// <summary>
/// Generic desktop launcher for any Karawan game.
/// 
/// This launcher does NOT have compile-time dependencies on any game project.
/// Instead, it loads game assemblies dynamically at runtime based on configuration.
/// 
/// To use:
/// 1. Build your game project (e.g., grid.dll)
/// 2. Copy the game DLL to the launcher's output directory (or a known location)
/// 3. Ensure game.launch.json points to the correct game config
/// 4. Ensure the game config specifies the correct assembly in defaults.loader.assembly
/// 5. Run the launcher
/// </summary>
public class DesktopMain
{
    /// <summary>
    /// Search for the resource path by looking for key configuration files.
    /// </summary>
    private static string _determineResourcePath()
    {
        // Check various possible locations for the models folder
        string[] possiblePaths = {
            "./models/",
            "../models/",
            "../../models/",
            "../../../models/",
            "../../../../models/",
            "../../../../../models/",
            "../../../../../../models/",
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(Path.Combine(path, "game.launch.json")))
            {
                Console.WriteLine($"Found game.launch.json at {path}");
                return path;
            }
        }

        // Also check for any .json game config directly
        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path))
            {
                var jsonFiles = Directory.GetFiles(path, "*.json");
                if (jsonFiles.Length > 0)
                {
                    Console.WriteLine($"Found config files at {path}");
                    return path;
                }
            }
        }

        Console.WriteLine("Warning: Could not find models directory, using ./models/");
        return "./models/";
    }

    /// <summary>
    /// Setup platform-specific graphics API settings.
    /// </summary>
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

    /// <summary>
    /// Pre-load game assemblies before the loader needs them.
    /// This ensures the assemblies are available when the loader tries to instantiate game classes.
    /// </summary>
    private static void _preloadGameAssemblies(string resourcePath, LaunchConfig launchConfig)
    {
        string assemblyName = null;
        
        // First, check if assembly is specified directly in launch config
        if (!string.IsNullOrEmpty(launchConfig.Game.Assembly))
        {
            assemblyName = launchConfig.Game.Assembly;
            Console.WriteLine($"Assembly specified in launch config: {assemblyName}");
        }
        else
        {
            // Otherwise, read from the game config
            string gameConfigPath = Path.Combine(resourcePath, launchConfig.Game.ConfigPath);
            if (!File.Exists(gameConfigPath))
            {
                Console.WriteLine($"Warning: Game config not found at {gameConfigPath}");
                return;
            }

            try
            {
                using var stream = File.OpenRead(gameConfigPath);
                using var doc = System.Text.Json.JsonDocument.Parse(stream);
                
                if (doc.RootElement.TryGetProperty("defaults", out var defaults) &&
                    defaults.TryGetProperty("loader", out var loader) &&
                    loader.TryGetProperty("assembly", out var assemblyProp))
                {
                    assemblyName = assemblyProp.GetString();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Warning: Error reading game config: {e.Message}");
                return;
            }
        }
        
        if (string.IsNullOrEmpty(assemblyName))
        {
            Console.WriteLine("Warning: No assembly specified in configuration.");
            return;
        }
        
        Console.WriteLine($"Game assembly: {assemblyName}");

        // Try to pre-load the assembly from various locations
        string[] searchPaths = {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assemblyName),
            Path.Combine(resourcePath, assemblyName),
            Path.Combine(resourcePath, "..", assemblyName),
            Path.Combine(resourcePath, "..", "bin", "Debug", "net9.0", assemblyName),
            assemblyName
        };

        foreach (var searchPath in searchPaths)
        {
            string fullPath = Path.GetFullPath(searchPath);
            if (File.Exists(fullPath))
            {
                Console.WriteLine($"Pre-loading assembly from: {fullPath}");
                engine.rom.Loader.TryLoadDll(fullPath);
                return;
            }
        }

        Console.WriteLine($"Warning: Could not find assembly {assemblyName} in search paths.");
        Console.WriteLine("The loader will attempt to find it at runtime.");
    }

    public static void Main(string[] args)
    {
        var cwd = Directory.GetCurrentDirectory();
        Console.WriteLine($"Karawan Generic Launcher");
        Console.WriteLine($"CWD: {cwd}");
        Console.WriteLine($"Base Directory: {AppDomain.CurrentDomain.BaseDirectory}");

        // 1. Setup platform graphics
        _setupPlatformGraphics();

        // 2. Determine resource path
        string resourcePath = _determineResourcePath();
        resourcePath = Path.GetFullPath(resourcePath);
        Console.WriteLine($"Resource path: {resourcePath}");
        GlobalSettings.Set("Engine.ResourcePath", resourcePath);
        GlobalSettings.Set("Engine.GeneratedResourcePath", resourcePath);

        // 3. Load launch configuration
        var launchConfig = LaunchConfig.LoadFromStandardLocations(resourcePath);
        Console.WriteLine($"Window title: {launchConfig.Branding.WindowTitle}");
        Console.WriteLine($"Game config: {launchConfig.Game.ConfigPath}");
        launchConfig.ApplyToGlobalSettings();

        // 4. Pre-load game assemblies (critical for dynamic loading)
        _preloadGameAssemblies(resourcePath, launchConfig);

        // 5. Register engine services
        I.Register<engine.joyce.TextureCatalogue>(() => new engine.joyce.TextureCatalogue());

        // 6. Setup asset implementation and load game config
        string gameConfigPath = Path.Combine(resourcePath, launchConfig.Game.ConfigPath);
        gameConfigPath = Path.GetFullPath(gameConfigPath);
        Console.WriteLine($"Loading game config from: {gameConfigPath}");

        I.Register<engine.casette.Loader>(() =>
        {
            using var streamJson = File.OpenRead(gameConfigPath);
            return new engine.casette.Loader(streamJson);
        });

        var iassetDesktop = new AssetImplementation();
        iassetDesktop.WithLoader();
        I.Get<engine.casette.Loader>().InterpretConfig();

        // 7. Create window
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(1280, 720);
        options.Title = launchConfig.Branding.WindowTitle;
        options.FramesPerSecond = 60;
        options.VSync = false;
        options.ShouldSwapAutomatically = false;
        options.WindowState = WindowState.Normal;
        options.PreferredDepthBufferBits = 16;

        IWindow iWindow = Window.Create(options);
        iWindow.Size = new Vector2D<int>(1280, 720);

        // 8. Create engine
        var e = Splash.Silk.Platform.EasyCreate(args, iWindow, out var _);
        e.SetFullscreen(false);

        iWindow.Initialize();

        // 9. Setup logging
        {
            engine.ConsoleLogger logger = new(e);
            engine.Logger.SetLogTarget(logger);
        }

        // 10. Register audio API
        I.Register<Boom.ISoundAPI>(() => new Boom.OpenAL.API(e));

        // 11. Start game (this will dynamically load and instantiate the root module)
        Console.WriteLine("Starting game...");
        I.Get<engine.casette.Loader>().StartGame();

        e.Execute();

        Environment.Exit(0);
    }
}
