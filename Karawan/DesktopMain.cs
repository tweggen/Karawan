using System;
using System.IO;
using System.Runtime.InteropServices;
using engine;
using Silk.NET.Core;
using Silk.NET.Windowing;
using Silk.NET.Maths;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Karawan;

public class DesktopMain
{
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

        // 4. Determine generated resource path
        string generatedPath;
        if (Directory.Exists("assets"))
        {
            // Installed mode - generated resources are in assets folder
            generatedPath = "./";
        }
        else
        {
            // Development mode - generated resources are in nogame/generated
            generatedPath = "../nogame/generated";
        }
        GlobalSettings.Set("Engine.GeneratedResourcePath", generatedPath);

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
