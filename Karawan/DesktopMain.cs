
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

#if false
            splash.TextureManager textureManager = new();

            for (int n = 0; n < 20; ++n)
            {
                double now = Raylib.GetTime();
                int nIterations = 10;
                for (int i = 0; i < nIterations; ++i)
                {
                    splash.RlTextureEntry rlTextureEntry;
                    rlTextureEntry = textureManager.FindRlTexture(new engine.joyce.Texture("joyce://64MB"));
                    textureManager.LoadBackTexture(rlTextureEntry);
                }
                double then = Raylib.GetTime();
                Console.WriteLine("Upload of {0} * 64MB took {1}s.", nIterations, then - now);
            }
#endif

            var scnNogameRoot = new nogame.RootScene();
            scnNogameRoot.SceneActivate(engine);

            platform.Execute();

            scnNogameRoot.SceneDeactivate();
        }
    }
}
