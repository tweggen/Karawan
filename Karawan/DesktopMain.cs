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

        /*
         * Setup globals and statics
         */
        engine.GlobalSettings.Set("nogame.CreateOSD", "true");
        engine.GlobalSettings.Set("platform.threeD.API", "OpenGL");
        engine.GlobalSettings.Set("platform.threeD.API.version", "330");
        engine.GlobalSettings.Set("engine.NailLogicalFPS", "true");
        if (Directory.Exists("assets"))
        {
            engine.GlobalSettings.Set("Engine.ResourcePath", "./assets/");
        }
        else
        {
            engine.GlobalSettings.Set("Engine.ResourcePath", "../../../../Wuka/Platforms/Android/");
        }
        engine.GlobalSettings.Set("Engine.RWPath", "./");
        
        engine.GlobalSettings.Set("nogame.LogosScene.PlayTitleMusic", "true");
        engine.GlobalSettings.Set("splash.touchControls", "false");
        engine.Props.Set("nogame.CreateHouses", "true");
        engine.Props.Set("nogame.CreateTrees", "true");
        engine.GlobalSettings.Set("platform.suspendOnUnfocus", "false");
        engine.GlobalSettings.Set("platform.initialZoomState", "0");


        engine.Assets.SetAssetImplementation(new Karawan.AssetImplementation());

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
            options.Title = "codename Karawan";
            options.FramesPerSecond = 60;
            options.VSync = false;
            options.ShouldSwapAutomatically = false;
            options.WindowState = WindowState.Normal;
            iWindow = Window.Create(options);

            iWindow.Size = new Vector2D<int>(1280, 720);


        }

        var e = Splash.Silk.Platform.EasyCreate(args, iWindow);
        e.SetFullscreen(startFullscreen);

        iWindow.Initialize();
        try
        {
            System.IO.Stream streamImage = engine.Assets.Open("appicon.png");
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

        // Add the engine web service to the host.
        // app.MapGrpcService<GreeterService>();
        // app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client.
        // To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

        // nogame.Main.Start(e);
        LoadGame(e, "nogame.dll", "nogame.Main", "Start");

        e.Execute();

        // Add Call to remove an implementations.
    }
}
