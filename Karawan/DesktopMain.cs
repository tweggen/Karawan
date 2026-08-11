using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using engine;
using Silk.NET.Core;
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

        /*
         * Search UPWARD for the content root instead of counting "..".
         *
         * This used to end in a hardcoded "../../../../../models/", tuned for a CWD of
         * <repo>/Karawan/bin/Debug/<tfm>/<rid>. `dotnet run` sets CWD to the PROJECT
         * directory instead, so the same five levels walked out of the checkout and landed
         * in the user's home - producing a DirectoryNotFoundException for a path nobody
         * configured, at a distance that varied with how deep the repo sits.
         *
         * See engine.GameRoot for the full account.
         */
        string? root = engine.GameRoot.PathTo("models");
        if (null != root) return root;

        throw new DirectoryNotFoundException(
            "Could not locate the game content root. Searched upward from "
            + $"'{Directory.GetCurrentDirectory()}' and from '{AppContext.BaseDirectory}' "
            + "for models/nogame.json, models/game.launch.json or Karawan.sln, and found "
            + "none. Run from inside the repository, or ship an 'assets' directory beside "
            + "the executable.");
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

        // Same upward search as _determineResourcePath. Note the old hardcoded chains here
        // and there disagreed - four levels vs five - because each was tuned separately
        // against a different observed layout. Neither was right for `dotnet run`.
        string? generated = engine.GameRoot.PathTo(Path.Combine("nogame", "generated"));
        if (null != generated) return generated;

        throw new DirectoryNotFoundException(
            "Could not locate the generated resource directory (nogame/generated). "
            + "See the message from the resource-path lookup for what was searched.");
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

        /*
         * WP-3.5: the Silk windowing fallback is GONE. SDL3 is the only desktop backend.
         *
         * No Initialize() step: SDL_CreateWindow and SDL_GL_CreateContext happen together in
         * the constructor, because GetProcAddress has to be usable before Platform's load
         * handler runs - that handler is what calls GL.GetApi.
         */
        var sdl3Backend = new Splash.Silk.Sdl3WindowBackend(
            launchConfig.Branding.WindowTitle, 1280, 720, isResizable: true);

        engine.Engine e = Splash.Silk.Platform.EasyCreate(args, sdl3Backend, out var _);
        e.SetFullscreen(startFullscreen);

        // 10. Set window icon
        try
        {
            string iconPath = launchConfig.Branding.AppIcon;
            using Stream streamImage = engine.Assets.Open(iconPath);
            using var img = Image.Load<Rgba32>(streamImage);
            byte[] pixelArray = new byte[img.Width * img.Height * 4];
            img.CopyPixelDataTo(pixelArray);
            sdl3Backend.SetWindowIcon(pixelArray, img.Width, img.Height);
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
