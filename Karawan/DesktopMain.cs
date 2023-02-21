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

            engine.AddSceneFactory("root", () => new nogame.RootScene());
            engine.AddSceneFactory("logos", () => new nogame.LogosScene());

            engine.SetMainScene("logos");
            engine.Execute();
        }
    }
}
