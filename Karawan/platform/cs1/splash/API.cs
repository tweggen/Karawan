using System;
using System.Numerics;
using Raylib_CsLo;

using System.Threading.Tasks;

namespace Karawan.platform.cs1.splash
{
    class API
    {
        private engine.Engine _engine;


        /**
         * Render all camera objects.
         */
        public void Render()
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Raylib.SKYBLUE);
            Raylib.DrawFPS(10, 10);
            Raylib.DrawText("Raylib is easy!!!", 640, 360, 50, Raylib.RED);

            var listCameras = _engine.GetEcsWorld().GetEntities()
                .With<engine.joyce.components.Camera3>()
                .With<engine.transform.components.Object3ToWorldMatrix>()
                .AsEnumerable();
            foreach(var eCamera in listCameras)
            {
                Console.WriteLine("Found Camera.");

            }

            Raylib.EndDrawing();
        }

        public API(engine.Engine engine)
        {
            _engine = engine;
        }
    }
}
