using System;
using System.Runtime.InteropServices;
using Android.App;
using Android.Media;
using Android.Runtime;

namespace Wuka
{
    [Application]
    /*
     * WP-6.1: was MauiApplication. MAUI contributed nothing here beyond the base class -
     * the body below is the real work, a libassimp load probe that reports at startup
     * whether the native library resolved.
     */
    public class MainApplication : Android.App.Application
    {

        [DllImport("libassimp.so", EntryPoint = "aiGetExportFormatCount", SetLastError = true)]
        static extern int twgAiGetExportFormatCount();

        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
            {
                var runtime = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
                Console.WriteLine($"Starting on platform {runtime}. Waiting for permissions...");
                try
                {
                    var size = twgAiGetExportFormatCount();
                    Console.WriteLine("Library loaded successfully!");
                }
                catch (DllNotFoundException e)
                {
                    Console.WriteLine($"Library not found: {e.Message}");
                }
                catch (EntryPointNotFoundException e)
                {
                    Console.WriteLine($"Function not found in library: {e.Message}");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error loading library: {e.Message}");
                }

                Console.WriteLine($"Continuing...");
            }
        }
    }
}