using System;
using Android.App;
using Android.Runtime;

namespace Wuka
{
    [Application]
    /*
     * WP-6.1: was MauiApplication. MAUI contributed nothing here beyond the base class.
     *
     * WP-4.4 removed what the body used to do. It was a libassimp.so load probe,
     * reporting at startup whether that native resolved - useful while Assimp was
     * the model importer and shipped in the APK. Phase 4 bakes models at build
     * time, libassimp.so is no longer packaged, and the probe would now report a
     * missing library on every launch: an alarming log line describing the
     * intended state. The class stays because [Application] and the JNI
     * constructor are what Android instantiates.
     */
    public class MainApplication : Android.App.Application
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
            var runtime = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
            Console.WriteLine($"Starting on platform {runtime}. Waiting for permissions...");
        }
    }
}
