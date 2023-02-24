using System;
using System.Numerics;
using Raylib_CsLo;

using System.Threading.Tasks;
using DefaultEcs;
using BepuUtilities;

namespace Karawan.platform.cs1.splash
{
    class API
    {
        private engine.Engine _engine;

        private systems.CreateRlMeshesSystem _createRlMeshesSystem;
        private systems.DrawRlMeshesSystem _drawRlMeshesSystem;
        private systems.DrawSkyboxesSystem _drawSkyboxesSystem;

        private MaterialManager _materialManager;
        private TextureGenerator _textureGenerator;
        private TextureManager _textureManager;
        private MeshManager _meshManager;
        private LightManager _lightManager;


        /**
         * Render all camera objects.
         */
        public void Render()
        {
            /*
             * Create/upload all ressources that haven't been uploaded.
             */
            _createRlMeshesSystem.Update(_engine);
            _lightManager.CollectLights(_materialManager.GetInstanceShaderEntry());

            Raylib.ClearBackground(Raylib.BLACK);

            var listCameras = _engine.GetEcsWorld().GetEntities()
                .With<engine.joyce.components.Camera3>()
                .With<engine.transform.components.Transform3ToWorld>()
                .AsEnumerable();

            foreach(var eCamera in listCameras)
            {
                var cCameraParams = eCamera.Get<engine.joyce.components.Camera3>();
                var mToWorld = eCamera.Get<engine.transform.components.Transform3ToWorld>().Matrix;

                var vCameraPosition = mToWorld.Translation;
                Vector3 vY;
                Vector3 vUp = vY = new Vector3(mToWorld.M21, mToWorld.M22, mToWorld.M23);
                Vector3 vZ = new Vector3(-mToWorld.M31, -mToWorld.M32, -mToWorld.M33);
                Vector3 vFront = -vZ;
                Vector3 vTarget = vCameraPosition + vFront;
                // Console.WriteLine($"vFront = {vFront}");

                var rCamera = new Raylib_CsLo.Camera3D( vCameraPosition, vTarget, vUp, 
                    cCameraParams.Angle, CameraProjection.CAMERA_PERSPECTIVE);

                // TXWTODO: Hack the camera position into the main shader.
                _materialManager.HackSetCameraPos(vCameraPosition);

                // Raylib.BeginMode3D(rCamera);
                /*
                 * We need to reimplement BeginMode3d to freely set frustrums
                 */
                {
                    RlGl.rlDrawRenderBatchActive();      // Update and draw internal render batch

                    RlGl.rlMatrixMode(RlGl.RL_PROJECTION);    // Switch to projection matrix
                    RlGl.rlPushMatrix();                 // Save previous matrix, which contains the settings for the 2d ortho projection
                    RlGl.rlLoadIdentity();               // Reset current matrix (projection)

                    // Setup perspective projection
                    float top = cCameraParams.NearFrustum * (float)Math.Tan(cCameraParams.Angle * 0.5f * (float)Math.PI / 180f);
                    float aspect = 16f / 9f;
                    float right = top * aspect;
                    RlGl.rlFrustum(-right, right, -top, top, cCameraParams.NearFrustum, cCameraParams.FarFrustum);

                    RlGl.rlMatrixMode(RlGl.RL_MODELVIEW);     // Switch back to modelview matrix
                    RlGl.rlLoadIdentity();               // Reset current matrix (modelview)

                    // Setup Camera view
                    Matrix4x4 matView;
                    matView = Matrix4x4.CreateLookAt(vCameraPosition, vCameraPosition+vZ , vUp);
                    // matView = Matrix4x4.Transpose(matView);
                    // Multiply modelview matrix by view matrix (camera)
                    RlGl.rlMultMatrixf((matView));

                    RlGl.rlEnableDepthTest();            // Enable DEPTH_TEST for 3D
                }


                /*
                 * Collect all standard meshes.
                 */
                _drawRlMeshesSystem.Update(cCameraParams.CameraMask);

                /*
                 * First draw player related stuff
                 */
                // TXWTODO: Nothing here

                /*
                 * Then draw standard world
                 */
                _drawRlMeshesSystem.RenderStandard();

                /*
                 * Then draw terrain
                 */
                // TXWTODO: Remove terrain from standard mesh drawing

                /*
                 * Then draw skybox
                 */
                _drawSkyboxesSystem.CameraPosition = vCameraPosition;
                _drawSkyboxesSystem.Update(cCameraParams.CameraMask);

                /*
                 * Then render transparent
                 */
                _drawRlMeshesSystem.RenderTransparent();

                Raylib.EndMode3D();
            }

            Raylib.DrawFPS(20, 100);
            Raylib.DrawText("codename Karawan", 20, 20, 10, Raylib.GREEN);
            Raylib.DrawText("Debug info:\n" + _drawRlMeshesSystem.GetDebugInfo(), 20, 30, 10, Raylib.GREEN);
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
            _drawSkyboxesSystem = new(_engine, _materialManager);
            _lightManager = new(_engine);
        }
    }
}
