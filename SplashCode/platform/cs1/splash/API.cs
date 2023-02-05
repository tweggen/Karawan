using System;
using System.Numerics;
using Raylib_CsLo;

using System.Threading.Tasks;

namespace Karawan.platform.cs1.splash
{
    class API
    {
        private engine.Engine _engine;

        private systems.CreateRlMeshesSystem _createRlMeshesSystem;
        private systems.DrawRlMeshesSystem _drawRlMeshesSystem;

        private MaterialManager _materialManager;
        private TextureGenerator _textureGenerator;
        private TextureManager _textureManager;
        private MeshManager _meshManager;

        /**
         * Render all camera objects.
         */
        public void Render()
        {
            /*
             * Create/upload all ressources that haven't been uploaded.
             */
            _createRlMeshesSystem.Update(_engine); 

            Raylib.ClearBackground(Raylib.BLUE);

            var listCameras = _engine.GetEcsWorld().GetEntities()
                .With<engine.joyce.components.Camera3>()
                .With<engine.transform.components.Transform3ToWorld>()
                .AsEnumerable();
            foreach(var eCamera in listCameras)
            {
                var cCameraParams = eCamera.Get<engine.joyce.components.Camera3>();
                var mToWorld = eCamera.Get<engine.transform.components.Transform3ToWorld>().Matrix;

                var vPosition = mToWorld.Translation;
                var vUp = new Vector3(mToWorld.M12, mToWorld.M22, mToWorld.M32);
                var vFront = new Vector3(-mToWorld.M13, -mToWorld.M23, -mToWorld.M33);
                var vTarget = vPosition + vFront;
                // Console.WriteLine($"vFront = {vFront}");

                var rCamera = new Raylib_CsLo.Camera3D( vPosition, vTarget, vUp, 
                    cCameraParams.Angle, CameraProjection.CAMERA_PERSPECTIVE);

                // TXWTODO: Hack the camera position into the main shader.
                _materialManager.HackSetCameraPos(vPosition);

                Raylib.BeginMode3D(rCamera);

                _drawRlMeshesSystem.Update(cCameraParams.CameraMask);

                Raylib.EndMode3D();
            }

            Raylib.DrawFPS(20, 40);
            Raylib.DrawText("codename Karawan", 20, 20, 10, Raylib.GREEN);

        }

        public API(engine.Engine engine)
        {
            _engine = engine;
            _textureGenerator = new TextureGenerator(engine);
            _textureManager = new(_textureGenerator);
            _materialManager = new(_textureManager);
            _materialManager.Manage(engine.GetEcsWorld());
            _meshManager = new();
            _meshManager.Manage(engine.GetEcsWorld());
            _createRlMeshesSystem = new(_engine, _meshManager, _materialManager);
            _drawRlMeshesSystem = new(_engine, _materialManager);
        }
    }
}
