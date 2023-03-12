//using Raylib_CsLo;
using System.Runtime.InteropServices;


namespace Raylib_CsLo
{
    public static unsafe partial class Raylib
    {
        [DllImport("raylib", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void CloseWindow();

    }
}

namespace SanAi
{
    [Activity(Label = "@string/app_name", MainLauncher = true)]
    public class MainActivity : Activity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            IntPtr libHandle;
            var res = NativeLibrary.TryLoad("raylib", out libHandle);
            Console.WriteLine($"res == {res}.");
            Raylib_CsLo.Raylib.CloseWindow();

            base.OnCreate(savedInstanceState);
#if false
            Raylib.SetTraceLogLevel(4 /* LOG_WARNING */);
            Raylib.InitWindow(0, 0, "codename Karawan"); //Make app window 1:1 to screen size https://github.com/raysan5/raylib/issues/1731
#endif

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);
        }
    }
}