
namespace Karawan
{
    public class DesktopMain
    {
        public static void Main(string[] args)
        {
            Karawan.platform.cs1.Platform platform = new Karawan.platform.cs1.Platform( args );

            engine.Engine engine = new engine.Engine(platform);
            engine.SetupDone();

            platform.SetEngine(engine);
            platform.SetupDone();
            engine.PlatformSetupDone();

            var scnNogameRoot = new nogame.RootScene();
            scnNogameRoot.SceneActivate(engine);

            platform.Execute();

            scnNogameRoot.SceneDeactivate();
        }
    }
}
