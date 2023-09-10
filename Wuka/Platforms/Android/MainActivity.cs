using Android.App;
using Android.App.Roles;
using Android.Content.PM;
using Android.OS;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;
using Silk.NET.Input.Sdl;

using Android.Content.Res;
using engine;
using Silk.NET.GLFW;
using Android.Media;
using Wuka.Platforms.Android;
using Java.Util;
using System.Numerics;

namespace Wuka
{
    [Activity(
        MainLauncher = true, 
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
        ScreenOrientation = ScreenOrientation.Landscape,
        Theme = "@style/Maui.SplashTheme" //"@android:style/Theme.Black.NoTitleBar.Fullscreen"
    )]
    public class MainActivity : Silk.NET.Windowing.Sdl.Android.SilkActivity
    {
        internal static AssetManager AssetManager;
        
        private Silk.NET.Windowing.IView _iView;
        private engine.Engine _engine;

        private async void _requestBluetoothPermission()
        {
            try
            {
                var permissionStatus = await Permissions.RequestAsync<MyBluetoothPermission>();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }



        protected override void OnStop()
        {
            //_engine.Suspend();
            base.OnStop();
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            _requestBluetoothPermission();
        }

        protected override void OnRestart()
        {
            //_engine.Resume();
            base.OnRestart();
        }

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
            engine.GlobalSettings.Set("platform.initialZoomState", "-16");
            engine.GlobalSettings.Set("nogame.CreateUI", "false");
            engine.GlobalSettings.Set("nogame.LogosScene.PlayTitleMusic", "true");
            engine.GlobalSettings.Set("Engine.RWPath", "./data/");

            _engine = Splash.Silk.Platform.EasyCreate(new string[] { }, _iView);
#if false
            {
                WireServer.API aWireServer = new(e, 9001);
            }
#endif

            Implementations.Register<Boom.ISoundAPI>(() =>
            {
                var api = new Boom.OpenAL.API(_engine);
                return api;
            });
            
            nogame.Main.Start(_engine);

            _engine.Execute();

            Implementations.Get<Boom.ISoundAPI>().Dispose();

        }
    }
}