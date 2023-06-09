﻿using Android.App;
using Android.App.Roles;
using Android.Content.PM;
using Android.OS;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;
using Silk.NET.Input.Sdl;

using Android.Content.Res;
using engine;
using Silk.NET.GLFW;

namespace Wuka
{
    [Activity(
        MainLauncher = true, 
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
        ScreenOrientation = ScreenOrientation.Landscape,
        Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen"
    )]
    public class MainActivity : Silk.NET.Windowing.Sdl.Android.SilkActivity
    {
        internal static AssetManager AssetManager;
        
        private Silk.NET.Windowing.IView _iView;
        protected override void OnRun()
        {
            
            /*
             * setup framework dependencies.
             */
            SdlWindowing.RegisterPlatform();
            SdlInput.RegisterPlatform();
            
            /*
             * Setup singletons and statics
             */
            var assetManagerImplementation = new Wuka.AssetImplementation(Assets);
            engine.Assets.SetAssetImplementation(assetManagerImplementation);

            var options = ViewOptions.Default;
            options.API = new GraphicsAPI(ContextAPI.OpenGLES, ContextProfile.Compatability, ContextFlags.Default, new APIVersion(3, 0));
            options.FramesPerSecond = 60;
            options.VSync = false;
            options.ShouldSwapAutomatically = false;
            _iView = Silk.NET.Windowing.Window.GetView(options); // note also GetView, instead of Window.Create.

            engine.GlobalSettings.Set("nogame.CreateOSD", "true");
            engine.GlobalSettings.Set("platform.threeD.API", "OpenGLES");
            engine.GlobalSettings.Set("platform.threeD.API.version", "300");
            engine.GlobalSettings.Set("engine.NailLogicalFPS", "true");
            engine.GlobalSettings.Set("Engine.ResourcePath", "./");
            engine.GlobalSettings.Set("splash.touchControls", "true");
            engine.GlobalSettings.Set("Android", "true");

            engine.GlobalSettings.Set("nogame.LogosScene.PlayTitleMusic", "true");


            var e = Splash.Silk.Platform.EasyCreate(new string[] { }, _iView);
#if false
            {
                WireServer.API aWireServer = new(e, 9001);
            }
#endif

            Implementations.Register<Boom.ISoundAPI>(() =>
            {
                var api = new Boom.OpenAL.API(e);
                return api;
            });
            
            nogame.Main.Start(e);

            e.Execute();

            Implementations.Get<Boom.ISoundAPI>().Dispose();

        }
    }
}