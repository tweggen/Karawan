using Android.App;
using Android.App.Roles;
using Android.Content.PM;
using Android.OS;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;
using Silk.NET.Input.Sdl;

namespace Wuka
{
    [Activity(MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : Silk.NET.Windowing.Sdl.Android.SilkActivity
    {
        private Silk.NET.Windowing.IView _iView;
        protected override void OnRun()
        {
            SdlWindowing.RegisterPlatform();
            SdlInput.RegisterPlatform();
            // FileManager.AssetManager = Assets;
            var options = ViewOptions.Default;
            options.API = new GraphicsAPI(ContextAPI.OpenGLES, ContextProfile.Compatability, ContextFlags.Default, new APIVersion(3, 0));
            _iView = Silk.NET.Windowing.Window.GetView(options); // note also GetView, instead of Window.Create.

            engine.GlobalSettings.Set("platform.threeD.API", "OpenGLES");
            engine.GlobalSettings.Set("platform.threeD.API.version", "300");
            engine.GlobalSettings.Set("engine.NailLogicalFPS", "true");
            engine.GlobalSettings.Set("Engine.ResourcePath", "..\\..\\..\\..\\");

            var e = Splash.Silk.Platform.EasyCreate(new string[] { }, _iView);

            e.AddSceneFactory("root", () => new nogame.RootScene());
            e.AddSceneFactory("logos", () => new nogame.LogosScene());

            e.SetMainScene("logos");
            e.Execute();
        }
    }
}