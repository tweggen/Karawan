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
    public class MainActivity : Activity, ActivityCompat.IOnRequestPermissionsResultCallback
    {
        private object _lo = new();
        private bool _havePermissions = false;
        private bool _isRequestingPermissions = false;
        private bool _triggeredGame = false;
        

        public static int REQUEST_BLUETOOTH_CONNECT = 10023;
        private static string _permissionBluetoothConnect = "android.permission.BLUETOOTH_CONNECT";
        string[] reqestPermission={ _permissionBluetoothConnect };
        private void _requestBluetoothPermission()
        { 
            if (ActivityCompat.ShouldShowRequestPermissionRationale(this, _permissionBluetoothConnect))
            {
                _isRequestingPermissions = true;
                // Provide an additional rationale to the user if the permission was not granted
                // and the user would benefit from additional context for the use of the permission.
                // For example if the user has previously denied the permission.
                ActivityCompat.RequestPermissions(this, reqestPermission, REQUEST_BLUETOOTH_CONNECT);
            }
            else
            {
                _isRequestingPermissions = true;
                //REQUEST_BLUETOOTH_CONNECT permission has not been granted yet. Request it directly.
                ActivityCompat.RequestPermissions(this, reqestPermission, REQUEST_BLUETOOTH_CONNECT);
            }
        }
        
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            //Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            if (requestCode == REQUEST_BLUETOOTH_CONNECT)
            {
                _isRequestingPermissions = false;
                if (grantResults.Length <= 0)
                {
                    // If user interaction was interrupted, the permission request is cancelled and you 
                    // receive empty arrays.
                    Log.Info("error", "User interaction was cancelled.");
                    _requestBluetoothPermission();
                }
                else if (grantResults[0] == PermissionChecker.PermissionGranted)
                {
                    lock (_lo)
                    {
                        _havePermissions = true;
                    }
                    _triggerGame();
                }
                else
                {
                    // Permission denied.
                    Toast.MakeText(this, " REQUEST_P0STNOTIFICATIONS Permission Denied", ToastLength.Long).Show();
                }
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
             * It might just set permissions
             */
            base.OnStop();
        }


        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            bool doTrigger = false;
            if (Android.OS.Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            {
                if (!(_checkPermissionGranted(Manifest.Permission.BluetoothConnect)))
                {
                    _requestBluetoothPermission();
                }
                else
                {
                    lock (_lo)
                    {
                        _havePermissions = true;
                        doTrigger = true;
                    }
                }
            }

            if (doTrigger)
            {
                _triggerGame();
            }
        }


        protected override void OnRestart()
        {
            base.OnRestart();

            if (_isRequestingPermissions)
            {
                return;
            }

            if (_havePermissions)
            {
                /*
                 * If we did not yet trigger the game activity, trigger it now.
                 */
                _triggerGame();
            }
        }


        void _triggerGame()
        {
            // Switch to GameActivity
            StartActivity(typeof(GameActivity));
        }
    }
}