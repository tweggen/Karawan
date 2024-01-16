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
using nogame;
using GameState = Android.App.GameState;

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

        public static int REQUEST_P0STNOTIFICATIONS = 10023;
        string[] reqestPermission={ "android.permission.POST_NOTIFICATIONS" };
        private void RequestPostNotificationsPermission()
        { 
            if (ActivityCompat.ShouldShowRequestPermissionRationale(this, "android.permission.POST_NOTIFICATIONS");)
            {
                // Provide an additional rationale to the user if the permission was not granted
                // and the user would benefit from additional context for the use of the permission.
                // For example if the user has previously denied the permission.
                ActivityCompat.RequestPermissions(this, reqestPermission, REQUEST_P0STNOTIFICATIONS);
            }
            else
            {
                //P0STNOTIFICATIONS permission has not been granted yet. Request it directly.
                ActivityCompat.RequestPermissions(this, reqestPermission, REQUEST_P0STNOTIFICATIONS);
            }
        }
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


        private bool _checkPermissionGranted(string Permissions)
        {
            // Check if the permission is already available.
            if (ActivityCompat.CheckSelfPermission(this, Permissions) != Permission.Granted)
            {
                return false;
            }
            else
            {
                return true;
            }
        }



        protected override void OnStop()
        {
            /*
             * Try to save a backup copy
             */
            I.Get<engine.DBStorage>().SaveGameState(I.Get<GameState>());

            //_engine.Suspend();
            base.OnStop();
            _engine.Exit();
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            if (Android.OS.Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            {
                if (!(CheckPermissionGranted(Manifest.Permission.BluetoothConnect)))
                {
                    _requestBluetoothPermission();
                }
            }
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
            _iView.Initialize();

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
            engine.GlobalSettings.Set("Engine.RWPath", System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData));

            _engine = Splash.Silk.Platform.EasyCreate(new string[] { }, _iView);
#if false
            {
                WireServer.API aWireServer = new(e, 9001);
            }
#endif

            I.Register<Boom.ISoundAPI>(() =>
            {
                var api = new Boom.OpenAL.API(_engine);
                return api;
            });
            
            nogame.Main.Start(_engine);

            _engine.Execute();

            I.Get<Boom.ISoundAPI>().Dispose();

        }
    }
}