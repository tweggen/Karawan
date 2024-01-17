using Android.App;
using Android.Media;
using Android.Runtime;
using Wuka.Platforms.Android;

namespace Wuka
{
    [Application]
    public class MainApplication : MauiApplication
    {

        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
            {
                var runtime = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
                Console.WriteLine($"Starting on platform {runtime}. Waiting for permissions...");
 
                Console.WriteLine($"Continuing...");
            }
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}