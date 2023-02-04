using System;
using Raylib_CsLo;

namespace Karawan
{
    public class DesktopMain
    {
        public static void Main(string[] args)
        {

            var engine = Karawan.platform.cs1.Platform.EasyCreate(args);
            engine.SetConfigParam("Engine.ResourcePath", "..\\..\\..\\..\\");

            var scnNogameRoot = new nogame.RootScene();
            scnNogameRoot.SceneActivate(engine);

            engine.Execute();

            scnNogameRoot.SceneDeactivate();
        }
    }
}
