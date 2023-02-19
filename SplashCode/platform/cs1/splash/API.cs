using System;
using System.Numerics;
using Raylib_CsLo;

using System.Threading.Tasks;
using DefaultEcs;
using static IronPython.Modules._ast;
using BepuUtilities;

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
                var matrixSkybox = Matrix4x4.Transpose(
                    Matrix4x4.CreateTranslation(vCameraPosition));

                Matrix4x4[] arrMatrix = { matrixSkybox };
                Span<Matrix4x4> spanMatrix = arrMatrix;
                /*
                 * I must draw using the instanced call because I only use an instanced shader.
                 */
                Raylib_CsLo.Raylib.DrawMeshInstanced(
                        rlMeshEntry.RlMesh,
                        rlMaterialEntry.RlMaterial,
                        spanMatrix,
                        1
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
                Vector3 vY;
                Vector3 vUp = vY = new Vector3(mToWorld.M21, mToWorld.M22, mToWorld.M23);
                Vector3 vZ = new Vector3(-mToWorld.M31, -mToWorld.M32, -mToWorld.M33);
                Vector3 vFront = -vZ;
                Vector3 vTarget = vPosition + vFront;
                // Console.WriteLine($"vFront = {vFront}");

                var rCamera = new Raylib_CsLo.Camera3D( vPosition, vTarget, vUp, 
                    cCameraParams.Angle, CameraProjection.CAMERA_PERSPECTIVE);

                // TXWTODO: Hack the camera position into the main shader.
                _materialManager.HackSetCameraPos(vPosition);

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
                    matView = Matrix4x4.CreateLookAt(vPosition, vPosition+vZ , vUp);
                    // matView = Matrix4x4.Transpose(matView);
                    // Multiply modelview matrix by view matrix (camera)
                    RlGl.rlMultMatrixf((matView));

                    RlGl.rlEnableDepthTest();            // Enable DEPTH_TEST for 3D
                }


                /*
                 * First draw player related stuff
                 */
                // TXWTODO: Nothing here

                /*
                 * Then draw standard world
                 */
                _drawRlMeshesSystem.Update(cCameraParams.CameraMask);

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
