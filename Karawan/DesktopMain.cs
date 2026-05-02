using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using engine;
using Silk.NET.Core;
using Silk.NET.Windowing;
using Silk.NET.Maths;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Karawan;

public class DesktopMain
{
    private static void _applySettingsOverrides(string[] args)
    {
        // --settings-file <path>: load a JSON object, apply each property as a GlobalSetting
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--settings-file")
            {
                string filePath = args[i + 1];
                if (File.Exists(filePath))
                {
                    Console.WriteLine($"Loading settings overrides from: {filePath}");
                    string json = File.ReadAllText(filePath);
                    using var doc = JsonDocument.Parse(json);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        string value = prop.Value.ValueKind == JsonValueKind.String
                            ? prop.Value.GetString()
                            : prop.Value.ToString();
                        GlobalSettings.Set(prop.Name, value);
                        Console.WriteLine($"  {prop.Name} = {value}");
                    }
                }
                else
                {
                    Console.WriteLine($"Warning: settings file not found: {filePath}");
                }
            }
        }

        // --setting <key>=<value>: individual GlobalSettings override (repeatable)
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--setting")
            {
                string kv = args[i + 1];
                int eq = kv.IndexOf('=');
                if (eq > 0)
                {
                    string key = kv.Substring(0, eq);
                    string value = kv.Substring(eq + 1);
                    GlobalSettings.Set(key, value);
                    Console.WriteLine($"Setting override: {key} = {value}");
                }
            }
        }
    }

    private static string _resolveRWPathOverride(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--rwpath")
            {
                return args[i + 1];
            }
        }
        return Environment.GetEnvironmentVariable("JOYCE_RWPATH");
    }

    /// <summary>
    /// Determine the resource path based on the current environment.
    /// </summary>
    private static string _determineResourcePath()
    {
        if (Directory.Exists("assets"))
        {
            // Installed/shipped mode on Windows
            return "./assets/";
        }

        if (File.Exists("./models/game.launch.json") || File.Exists("./models/nogame.json"))
        {
            // Jetbrains Rider / direct run from project root
            return "./models/";
        }

        // Running from bin/Debug or similar
        return "../../../../../models/";
    }

    /// <summary>
    /// Determine the generated resource path (animations, scenarios, textures, etc.)
    /// based on the current environment. Mirrors _determineResourcePath logic.
    /// Returns an absolute path to avoid path concatenation issues.
    /// </summary>
    private static string _determineGeneratedResourcePath()
    {
        if (Directory.Exists("assets"))
        {
            // Installed/shipped mode: generated assets are in assets/ along with others
            return Path.GetFullPath("./assets/") + Path.DirectorySeparatorChar;
        }

        if (File.Exists("./models/game.launch.json") || File.Exists("./models/nogame.json"))
        {
            // Jetbrains Rider / direct run from project root
            return Path.GetFullPath("./nogame/generated/") + Path.DirectorySeparatorChar;
        }

        // Running from bin/Debug or similar
        return Path.GetFullPath("../../../../nogame/generated/") + Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// Force explicit Assimp library version on macOS with Silk.NET 2.23+.
    /// Silk.NET 2.23 ships both libassimp.5.dylib and libassimp.6.dylib on macOS.
    /// Without explicit loading, symbol interposition causes GetVersionMajor() to report 5
    /// while ImportFileEx uses 6.0.2's behavior. Force load 6.dylib globally to ensure consistency.
    /// </summary>
    private static void _forceAssimpLibraryVersion()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

        try
        {
            var assimpAssembly = typeof(Silk.NET.Assimp.Assimp).Assembly;
            var version = assimpAssembly.GetName().Version;

            // Only apply on Silk.NET 2.23+
            if (version.Major < 2 || (version.Major == 2 && version.Minor < 23))
                return;

            // Find the native library in the output directory
            // Structure: bin/Debug/net9.0/osx-arm64/libassimp.6.dylib
            string arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
            string libPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "osx-" + arch, "libassimp.6.dylib");
            libPath = Path.GetFullPath(libPath);

            if (!File.Exists(libPath))
            {
                Console.WriteLine($"[Assimp] Warning: libassimp.6.dylib not found at: {libPath}");
                return;
            }

            const int RTLD_GLOBAL = 0x00008;
            const int RTLD_FIRST = 0x00100;

            var handle = dlopen(libPath, RTLD_GLOBAL | RTLD_FIRST);
            if (handle != IntPtr.Zero)
            {
                Console.WriteLine($"[Assimp] Preloaded libassimp.6.dylib globally from: {libPath}");
            }
            else
            {
                Console.WriteLine($"[Assimp] Warning: dlopen failed for: {libPath}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Assimp] Warning: Exception preloading libassimp.6.dylib: {e.Message}");
        }
    }

    [DllImport("libSystem.dylib", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr dlopen(string filename, int flags);

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
            // Windows and Linux
            GlobalSettings.Set("platform.threeD.API", "OpenGL");
            GlobalSettings.Set("platform.threeD.API.version", "430");
        }

        GlobalSettings.Set("engine.NailLogicalFPS", "true");
    }

    public static void Main(string[] args)
    {
        var cwd = Directory.GetCurrentDirectory();
        Console.WriteLine($"CWD is {cwd}");

        // 0. Force Assimp library version (macOS symbol interposition fix)
        //_forceAssimpLibraryVersion();

        // 1. Setup platform graphics (platform-specific, not game-specific)
        _setupPlatformGraphics();

        // 2. Determine resource path
        string resourcePath = _determineResourcePath();
        GlobalSettings.Set("Engine.ResourcePath", resourcePath);

        // 3. Load launch configuration (game-agnostic mechanism)
        var launchConfig = LaunchConfig.LoadFromStandardLocations(resourcePath);

        // 4. Determine generated resource path (animations, scenarios, texture atlases, etc.)
        string generatedPath = _determineGeneratedResourcePath();
        GlobalSettings.Set("Engine.GeneratedResourcePath", generatedPath);
        Console.WriteLine($"Generated resource path: {Path.GetFullPath(generatedPath)}");

        // 5. Apply game-specific settings from launch config
        launchConfig.ApplyToGlobalSettings();

        // 6. Register engine services
        I.Register<engine.joyce.TextureCatalogue>(() => new engine.joyce.TextureCatalogue());

        // 7. Setup asset implementation and load game config
        // The game config path is relative to the resource path
        string gameConfigPath = Path.Combine(resourcePath, launchConfig.Game.ConfigPath);
        // Normalize the path
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

        // Override RWPath if specified via CLI or environment variable.
        // Applied after InterpretConfig so CLI args take precedence over game config.
        string rwPathOverride = _resolveRWPathOverride(args);
        if (!string.IsNullOrEmpty(rwPathOverride))
        {
            Directory.CreateDirectory(rwPathOverride);
            GlobalSettings.Set("Engine.RWPath", rwPathOverride);
            Console.WriteLine($"RWPath overridden to: {rwPathOverride}");
        }

        // Apply settings overrides from --settings-file and --setting args.
        // Applied after InterpretConfig so CLI args take precedence over game config.
        _applySettingsOverrides(args);

        // 8. Create window
        bool startFullscreen;
#if DEBUG
        startFullscreen = false;
#else
        startFullscreen = true;
#endif

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

        // 9. Create engine
        var e = Splash.Silk.Platform.EasyCreate(args, iWindow, out var _);
        e.SetFullscreen(startFullscreen);

        iWindow.Initialize();

        // 10. Set window icon
        try
        {
            string iconPath = launchConfig.Branding.AppIcon;
            using Stream streamImage = engine.Assets.Open(iconPath);
            using var img = Image.Load<Rgba32>(streamImage);
            byte[] pixelArray = new byte[img.Width * img.Height * 4];
            img.CopyPixelDataTo(pixelArray);
            RawImage rawImage = new RawImage(img.Width, img.Height, pixelArray.AsMemory());
            iWindow.SetWindowIcon(ref rawImage);
        }
        catch (Exception)
        {
            // Unable to set icon - not critical
        }

        // 11. Setup logging
        {
            engine.ConsoleLogger logger = new(e);
            engine.Logger.SetLogTarget(logger);
        }

        // 12. Register audio API
        I.Register<Boom.ISoundAPI>(() => new Boom.OpenAL.API(e));

        // 13. Start game
        I.Get<engine.casette.Loader>().StartGame();

        e.Execute();

        Environment.Exit(0);
    }
}
