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
                var e = Splash.Raylib.Platform.EasyCreate(new string[] {});
                engine.GlobalSettings.Set("Engine.ResourcePath", "..\\..\\..\\..\\");

                e.AddSceneFactory("root", () => new nogame.RootScene());
                e.AddSceneFactory("logos", () => new nogame.LogosScene());

                e.SetMainScene("logos");
                e.Execute();
            }
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}