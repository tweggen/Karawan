using Raylib_CsLo;

namespace Karawan.platform.cs1
{
    class PlatformMain
    {
        public static void Execute(string[] args)
        {
            Platform platform = new Platform( args );

            engine.Engine engine = new engine.Engine(platform);
            engine.SetupDone();

            platform.SetEngine(engine);
            platform.SetupDone();
            engine.PlatformSetupDone();

            var scnNogameRoot = new nogame.RootScene();
            scnNogameRoot.SceneActivate(engine);

            platform.Execute();
        }
    }
}
