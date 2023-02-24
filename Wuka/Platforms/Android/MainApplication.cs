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

                var engine = Karawan.platform.cs1.Platform.EasyCreate(new string[] {});
                engine.SetConfigParam("Engine.ResourcePath", "..\\..\\..\\..\\");

                engine.AddSceneFactory("root", () => new nogame.RootScene());
                engine.AddSceneFactory("logos", () => new nogame.LogosScene());

                engine.SetMainScene("logos");
                engine.Execute();
            }
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}