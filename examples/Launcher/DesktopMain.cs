using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using engine;
using Silk.NET.Windowing;
using Silk.NET.Maths;

namespace Karawan.GenericLauncher;

/// <summary>
/// Generic desktop launcher for any Karawan game.
/// </summary>
public class DesktopMain
{
    /// <summary>
    /// Regex to detect if a path is inside a .NET build output directory.
    /// </summary>
    private static readonly Regex BuildOutputPattern = new Regex(
        @"[/\\]bin[/\\](Debug|Release)[/\\]net\d+\.\d+([/\\][a-z]+-[a-z0-9]+)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// If the given path is a build output directory, return the project root.
    /// Otherwise return null.
    /// </summary>
    private static string _tryGetProjectRootFromBuildDir(string path)
    {
        var match = BuildOutputPattern.Match(path);
        if (match.Success)
        {
            string projectRoot = path.Substring(0, match.Index);
            Console.WriteLine($"  '{path}' is a build dir, project root: '{projectRoot}'");
            return projectRoot;
        }
        Console.WriteLine($"  '{path}' is NOT a build dir");
        return null;
    }

    /// <summary>
    /// Try to find game.launch.json starting from the given base directory.
    /// Returns the directory containing game.launch.json, or null if not found.
    /// </summary>
    private static string _tryFindLaunchConfig(string baseDir)
    {
        string[] subPaths = { "models", "." };
        
        foreach (var subPath in subPaths)
        {
            string candidatePath = Path.GetFullPath(Path.Combine(baseDir, subPath));
            string launchConfigPath = Path.Combine(candidatePath, "game.launch.json");
            
            Console.WriteLine($"  Checking: {launchConfigPath}");
            if (File.Exists(launchConfigPath))
            {
                Console.WriteLine($"  FOUND!");
                return candidatePath;
            }
        }
        return null;
    }

    /// <summary>
    /// Determine the resource path by checking standard locations.
    /// 
    /// Priority:
    /// 1. CWD (if not a build dir) -> models/ or ./
    /// 2. CWD project root (if CWD is a build dir) -> models/ or ./
    /// 3. Executable location project root (if exe is in build dir) -> models/ or ./
    /// </summary>
    private static string _determineResourcePath()
    {
        string cwd = Directory.GetCurrentDirectory();
        string exeDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        
        Console.WriteLine($"=== Resource Path Detection ===");
        Console.WriteLine($"CWD: {cwd}");
        Console.WriteLine($"Exe Dir: {exeDir}");

        // Build list of base directories to search
        var searchBases = new System.Collections.Generic.List<string>();

        // 1. Check CWD
        string cwdProjectRoot = _tryGetProjectRootFromBuildDir(cwd);
        if (cwdProjectRoot != null)
        {
            // CWD is a build dir - use its project root
            searchBases.Add(cwdProjectRoot);
        }
        else
        {
            // CWD is not a build dir - use it directly (this is the expected case)
            searchBases.Add(cwd);
        }

        // 2. Check executable location (as fallback)
        if (exeDir != cwd)
        {
            string exeProjectRoot = _tryGetProjectRootFromBuildDir(exeDir);
            if (exeProjectRoot != null)
            {
                // Don't add exe project root - that would be the launcher project, not the game
                // This is intentional - we want CWD to control which game we run
                Console.WriteLine($"  (Skipping exe project root - use CWD to specify game)");
            }
        }

        // Search for game.launch.json
        Console.WriteLine($"Searching for game.launch.json...");
        foreach (var baseDir in searchBases)
        {
            string found = _tryFindLaunchConfig(baseDir);
            if (found != null)
            {
                Console.WriteLine($"=== Resource path: {found} ===");
                return found;
            }
        }

        // Fallback
        string fallback = Path.GetFullPath(Path.Combine(cwd, "models"));
        Console.WriteLine($"WARNING: game.launch.json not found!");
        Console.WriteLine($"=== Fallback resource path: {fallback} ===");
        return fallback;
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

    private static void _preloadGameAssemblies(string resourcePath, LaunchConfig launchConfig)
    {
        string assemblyName = null;
        
        if (!string.IsNullOrEmpty(launchConfig.Game.Assembly))
        {
            assemblyName = launchConfig.Game.Assembly;
            Console.WriteLine($"Assembly from launch config: {assemblyName}");
        }
        else
        {
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

        Console.WriteLine($"Warning: Could not find assembly {assemblyName}");
    }

    public static void Main(string[] args)
    {
        Console.WriteLine($"=== Karawan Generic Launcher ===");

        // 1. Setup platform graphics
        _setupPlatformGraphics();

        // 2. Determine resource path
        string resourcePath = _determineResourcePath();
        GlobalSettings.Set("Engine.ResourcePath", resourcePath);
        GlobalSettings.Set("Engine.GeneratedResourcePath", resourcePath);

        // 3. Load launch configuration from the resource path
        string launchConfigPath = Path.Combine(resourcePath, "game.launch.json");
        Console.WriteLine($"Loading launch config from: {launchConfigPath}");
        var launchConfig = LaunchConfig.Load(launchConfigPath);
        Console.WriteLine($"  Window title: {launchConfig.Branding.WindowTitle}");
        Console.WriteLine($"  Game config: {launchConfig.Game.ConfigPath}");
        Console.WriteLine($"  Assembly: {launchConfig.Game.Assembly ?? "(from game config)"}");
        launchConfig.ApplyToGlobalSettings();

        // 4. Pre-load game assemblies
        _preloadGameAssemblies(resourcePath, launchConfig);

        // 5. Register engine services
        I.Register<engine.joyce.TextureCatalogue>(() => new engine.joyce.TextureCatalogue());

        // 6. Load game config
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

        // 11. Start game
        Console.WriteLine("Starting game...");
        I.Get<engine.casette.Loader>().StartGame();

        e.Execute();

        Environment.Exit(0);
    }
}
