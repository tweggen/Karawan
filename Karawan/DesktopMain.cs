using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Reflection;
using engine;
using Silk.NET.Core;
using Silk.NET.Windowing;
using Silk.NET.Maths;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Karawan;

public class DesktopMain
{
    public static void LoadGame(engine.Engine engine0, string dllPath, string fullClassName, string methodName)
    {

        Assembly asm = Assembly.LoadFrom(dllPath);
        Type t = asm.GetType(fullClassName);

        //
        // 2. We will be invoking a method: 'public int MyMethod(int count, string text)'
        //
        var methodInfo = t.GetMethod(methodName, new Type[] { typeof(engine.Engine) });
        if (methodInfo == null)
        {
            // never throw generic Exception - replace this with some other exception type
            throw new Exception("Unable to find game startup method.");
        }

        //
        // 5. Specify parameters for the method we will be invoking: 'int MyMethod(int count, string text)'
        //
        object[] parameters = new object[1];
        parameters[0] = engine0; // 'count' parameter

        //
        // 6. Invoke method 'int MyMethod(int count, string text)'
        //
        var r = methodInfo.Invoke(null, parameters);
        Console.WriteLine(r);
    }


    public static void Main(string[] args)
    {
        // System.Environment.SetEnvironmentVariable("ALSOFT_LOGLEVEL", "3");

        string jsonPath;
        /*
         * Setup globals and statics
         */
        engine.GlobalSettings.Set("platform.threeD.API", "OpenGLES");
        engine.GlobalSettings.Set("platform.threeD.API.version", "330");
        engine.GlobalSettings.Set("engine.NailLogicalFPS", "true");
        if (Directory.Exists("assets"))
        {
            /*
             * This is when we start installed on windows.
             */
            engine.GlobalSettings.Set("Engine.ResourcePath", "./assets/");
            jsonPath = "./";
        }
        else
        {
            if (Path.Exists("./models/nogame.json"))
            {
                /*
                 * I don't know this case.
                 */
                engine.GlobalSettings.Set("Engine.ResourcePath", "./nogame/");
                jsonPath = "../models/";
            }
            else
            {
                /*
                 * This is when we start from the debugger on windows.
                 */
                jsonPath = "../models/";
                engine.GlobalSettings.Set("Engine.ResourcePath", "../../../../../nogame/");
            }
        }

        {
            string userRWPath = System.Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData);
            string vendorRWPath = Path.Combine(userRWPath, "nassau records");
            string appRWPath = Path.Combine(vendorRWPath, "silicondesert2");
            System.IO.Directory.CreateDirectory(appRWPath);
            engine.GlobalSettings.Set("Engine.RWPath", appRWPath);
        }
        
        engine.GlobalSettings.Set("splash.touchControls", "false");
        engine.GlobalSettings.Set("platform.suspendOnUnfocus", "false");
        engine.GlobalSettings.Set("platform.initialZoomState", "0");


        I.Register<engine.joyce.TextureCatalogue>(() => new engine.joyce.TextureCatalogue());

        /*
         * Bootstrap game by directly reading game config, setting up
         * asset implementation with the pathes.
         */
        var iassetDesktop = new Karawan.AssetImplementation();
        engine.casette.Loader cassetteLoader;
        var cwd = System.IO.Directory.GetCurrentDirectory();
        Console.WriteLine($"CWD is {cwd}");
        using (var streamJson =
               File.OpenRead(
                   Path.Combine(
                       engine.GlobalSettings.Get("Engine.ResourcePath"),
                       jsonPath+"nogame.json"))) 
        {
            cassetteLoader = new engine.casette.Loader(streamJson);
        }
        engine.Assets.SetAssetImplementation(iassetDesktop);
        cassetteLoader.SetAssetLoaderAssociations(iassetDesktop);

        
        IWindow iWindow = null;

        bool startFullscreen = true;
#if DEBUG
        startFullscreen = false;
#else
            startFullscreen = true;
#endif

        {
            var options = WindowOptions.Default;

            // options.API = GraphicsAPI.
            /*
             * Even if we don't start up fullscreen, we need to setup a size anyway.  
             */
            options.Size = new Vector2D<int>(1280, 720);
            // TXWTODO: This is game specific
            options.Title = "codename Karawan";
            options.FramesPerSecond = 60;
            options.VSync = false;
            options.ShouldSwapAutomatically = false;
            options.WindowState = WindowState.Normal;
            options.PreferredDepthBufferBits = 16;
            iWindow = Window.Create(options);

            iWindow.Size = new Vector2D<int>(1280, 720);


        }

        var e = Splash.Silk.Platform.EasyCreate(args, iWindow, out var _);
        e.SetFullscreen(startFullscreen);

        iWindow.Initialize();
        try
        {
            // TXWTODO: This also is game specific.
            System.IO.Stream streamImage = engine.Assets.Open("appiconpng.png");
            using (var img = Image.Load<Rgba32>(streamImage))
            {
                byte[] pixelArray = new byte[img.Width * img.Height * 4];
                img.CopyPixelDataTo(pixelArray);
                RawImage rawImage = new RawImage(img.Width, img.Height, pixelArray.AsMemory());
                iWindow.SetWindowIcon(ref rawImage);
            }
        }
        catch (Exception)
        {
            // Unable to set icon
        }

        {
            engine.ConsoleLogger logger = new(e);
            engine.Logger.SetLogTarget(logger);
        }

        I.Register<Boom.ISoundAPI>(() =>
        {
            var api = new Boom.OpenAL.API(e);
            return api;
        });

        cassetteLoader.InterpretConfig();
        cassetteLoader.StartGame();
        
        e.Execute();

        // Add Call to remove an implementations.
    }
}
