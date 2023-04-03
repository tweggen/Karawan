using Android.App;
using Android.App.Roles;
using Android.Content.PM;
using Android.OS;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl.Android;

namespace Wuka
{
    [Activity(MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : SilkActivity
    {
        private Silk.NET.Windowing.IView _iView;
        protected override void OnRun()
        {
            // FileManager.AssetManager = Assets;
            var options = ViewOptions.Default;
            options.API = new GraphicsAPI(ContextAPI.OpenGLES, ContextProfile.Compatability, ContextFlags.Default, new APIVersion(3, 0));
            _iView = Silk.NET.Windowing.Window.GetView(options); // note also GetView, instead of Window.Create.


            var e = Splash.Silk.Platform.EasyCreate(new string[] { }, iView);
            engine.GlobalSettings.Set("Engine.ResourcePath", "..\\..\\..\\..\\");

            e.AddSceneFactory("root", () => new nogame.RootScene());
            e.AddSceneFactory("logos", () => new nogame.LogosScene());

            e.SetMainScene("logos");
            e.Execute();
        }
    }
}