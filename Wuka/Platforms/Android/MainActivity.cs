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
using Android;
using Android.Runtime;
using Android.Util;
using Android.Widget;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Xamarin.Essentials;
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
    public class MainActivity : Silk.NET.Windowing.Sdl.Android.SilkActivity, ActivityCompat.IOnRequestPermissionsResultCallback
    {
        internal static AssetManager AssetManager;
        
        private Silk.NET.Windowing.IView _iView;
        private engine.Engine _engine;

        public static int REQUEST_BLUETOOTH_CONNECT = 10023;
        private static string _permissionBluetoothConnect = "android.permission.BLUETOOTH_CONNECT";
        string[] reqestPermission={ _permissionBluetoothConnect };
        private void _requestBluetoothPermission()
        { 
            if (ActivityCompat.ShouldShowRequestPermissionRationale(this, _permissionBluetoothConnect))
            {
                // Provide an additional rationale to the user if the permission was not granted
                // and the user would benefit from additional context for the use of the permission.
                // For example if the user has previously denied the permission.
                ActivityCompat.RequestPermissions(this, reqestPermission, REQUEST_BLUETOOTH_CONNECT);
            }
            else
            {
                //P0STNOTIFICATIONS permission has not been granted yet. Request it directly.
                ActivityCompat.RequestPermissions(this, reqestPermission, REQUEST_BLUETOOTH_CONNECT);
            }
        }
        
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            //Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            if (requestCode == REQUEST_BLUETOOTH_CONNECT)
            {
                if (grantResults.Length <= 0)
                {
                    // If user interaction was interrupted, the permission request is cancelled and you 
                    // receive empty arrays.
                    Log.Info("error", "User interaction was cancelled.");
                }
                else if (grantResults[0] == PermissionChecker.PermissionGranted)
                {
                    // Permission was granted.
                }
                else
                {
                    // Permission denied.
                    Toast.MakeText(this, " REQUEST_P0STNOTIFICATIONS Permission Denied", ToastLength.Long).Show();
                }
            }
        }
        #if false
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
        #endif
    

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
            if (Android.OS.Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            {
                if (!(_checkPermissionGranted(Manifest.Permission.BluetoothConnect)))
                {
                    _requestBluetoothPermission();
                }
            }
            base.OnCreate(savedInstanceState);
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
            engine.GlobalSettings.Set("Engine.RWPath", System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData));

            _engine = Splash.Silk.Platform.EasyCreate(new string[] { }, _iView);
            _iView.Initialize();

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