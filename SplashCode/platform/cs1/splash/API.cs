using System;
using System.Numerics;
using Raylib_CsLo;

using System.Threading.Tasks;
using DefaultEcs;

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


        private void _drawSkyboxes(in Vector3 vCameraPosition, in uint cameraMasks)
        {
            var skyboxes = _engine.GetEcsWorld().GetEntities()
                .With<engine.joyce.components.Skybox>().AsEnumerable();
            // TXWTODO: Sort it by distance.
            // TXWTODO: We certainly allow transparency in the skyboxes, so start with the one far away.
            foreach(var eSkybox in skyboxes)
            {
                // No transformation applied, just the camera.
                var cSkybox = eSkybox.Get<engine.joyce.components.Skybox>();
                if( 0 == (cameraMasks & cSkybox.CameraMask))
                {
                    continue;
                }
                var rlMeshEntry = eSkybox.Get<splash.components.RlMesh>().MeshEntry;
                var rlMaterialEntry = eSkybox.Get<splash.components.RlMaterial>().MaterialEntry;
                var matrixSkybox = Matrix4x4.Transpose(Matrix4x4.CreateTranslation(vCameraPosition));

                Raylib_CsLo.Raylib.DrawMesh(
                    rlMeshEntry.RlMesh,
                    rlMaterialEntry.RlMaterial,
                    matrixSkybox
                );

            }
        }

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
                var vUp = new Vector3(mToWorld.M21, mToWorld.M22, mToWorld.M23);
                var vFront = new Vector3(-mToWorld.M31, -mToWorld.M32, -mToWorld.M33);
                var vTarget = vPosition + vFront;
                // Console.WriteLine($"vFront = {vFront}");

                var rCamera = new Raylib_CsLo.Camera3D( vPosition, vTarget, vUp, 
                    cCameraParams.Angle, CameraProjection.CAMERA_PERSPECTIVE);

                // TXWTODO: Hack the camera position into the main shader.
                _materialManager.HackSetCameraPos(vPosition);

                Raylib.BeginMode3D(rCamera);

                /*
                 * First draw player related stuff
                 */
                // TXWTODO: Nothing here

                /*
                 * Then draw standard world
                 */
                // _drawRlMeshesSystem.Update(cCameraParams.CameraMask);

                /*
                 * Then draw terrain
                 */
                // TXWTODO: Remove terrain from standard mesh drawing

                /*
                 * Then draw skybox
                 */
                _drawSkyboxes(vPosition, cCameraParams.CameraMask);

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
