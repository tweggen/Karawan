using Raylib_CsLo;

namespace Karawan.platform.cs1
{
    class PlatformMain
    {
        public static void execute(string[] args)
        {
            Platform platform = new Platform( args );
            engine.Engine engine = new engine.Engine(platform);
            platform.execute();
        }
    }
}
