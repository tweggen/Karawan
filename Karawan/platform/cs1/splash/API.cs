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

                var cCameraParams = eCamera.Get<engine.joyce.components.Camera3>();
                var mToWorld = eCamera.Get<engine.transform.components.Object3ToWorldMatrix>().Matrix;

                var vPosition = mToWorld.Translation;
                var vUp = new Vector3(mToWorld.M12, mToWorld.M22, mToWorld.M32);
                var vFront = new Vector3(-mToWorld.M13, mToWorld.M23, mToWorld.M33);

                var rCamera = new Raylib_CsLo.Camera3D( vPosition, vFront, vUp, 
                    cCameraParams.Angle, CameraProjection.CAMERA_PERSPECTIVE);

            }

            Raylib.EndDrawing();
        }

        public API(engine.Engine engine)
        {
            _engine = engine;
        }
    }
}
