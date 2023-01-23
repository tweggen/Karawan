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

            splash.TextureManager textureManager = new();
            double now = Raylib.GetTime();
            for (int i = 0; i < 10; ++i)
            {
                splash.RlTextureEntry rlTextureEntry;
                rlTextureEntry = textureManager.FindRlTexture(new engine.joyce.Texture("joyce://64MB"));
                textureManager.LoadBackTexture(rlTextureEntry);
            }
            double then = Raylib.GetTime();

            var scnNogameRoot = new nogame.RootScene();
            scnNogameRoot.SceneActivate(engine);

            platform.Execute();
        }
    }
}
