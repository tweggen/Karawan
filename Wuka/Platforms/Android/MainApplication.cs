using Android.App;
using Android.Runtime;


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
                Console.WriteLine($"Starting on platform {runtime}");

            }
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}