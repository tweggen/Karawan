using System;
using System.Numerics;
using Raylib_CsLo;

using System.Threading.Tasks;
using DefaultEcs;
using BepuUtilities;
using System.Collections.Generic;

namespace Karawan.platform.cs1.splash
{
    class API
    {
        private object _lo = new();

        private engine.Engine _engine;

        private systems.CreateRlMeshesSystem _createRlMeshesSystem;
        private systems.DrawRlMeshesSystem _drawRlMeshesSystem;
        private systems.DrawSkyboxesSystem _drawSkyboxesSystem;
        private systems.CreateRlMusicSystem _createRlMusicSystem;

        private MaterialManager _materialManager;
        private TextureGenerator _textureGenerator;
        private TextureManager _textureManager;
        private MeshManager _meshManager;
        private MusicManager _musicManager;
        private LightManager _lightManager;

        /**
         * Called from the logical thread context every logical frame.
         * If behavior doesn't mess up.
         */
        public void OnLogicalFrame()
        {
            _createRlMeshesSystem.Update(_engine);
            _createRlMusicSystem.Update(_engine);

            /*
             * If we currently are not rendering, collect the data for the next 
             * rendering job. The entity system can only be read from this thread.
             */
            if(!_isRendering)
            {
                /*
                 * Create/upload all ressources that haven't been uploaded.
                 */
                _lightManager.CollectLights(_materialManager.GetInstanceShaderEntry());

                _logicalRender();

            }
        }

        public class RenderJob
        {
            // Camera parameters
            public engine.transform.components.Transform3ToWorld Transform3ToWorld;
            public engine.joyce.components.Camera3 Camera3;
            public CameraOutput CameraOutput;
        }


        /**
         * Collect the output of the cameras for later rendering.
         */
        private List<RenderJob> _logicalRenderFrame()
        {
            List<RenderJob> renderJobs = new();
            
            var listCameras = _engine.GetEcsWorld().GetEntities()
                .With<engine.joyce.components.Camera3>()
                .With<engine.transform.components.Transform3ToWorld>()
                .AsEnumerable();

            foreach (var eCamera in listCameras)
            {
                RenderJob renderJob = new();
                renderJob.Camera3 = eCamera.Get<engine.joyce.components.Camera3>();
                renderJob.Transform3ToWorld = eCamera.Get<engine.transform.components.Transform3ToWorld>();
                CameraOutput cameraOutput = new(renderJob.Camera3.CameraMask);


                _drawRlMeshesSystem.Update(cameraOutput);

                var vCameraPosition = renderJob.Transform3ToWorld.Matrix.Translation;
                _drawSkyboxesSystem.CameraPosition = vCameraPosition;
                _drawSkyboxesSystem.Update(cameraOutput);

                renderJobs.Add(renderJob);
            }
            return renderJobs;
        }


        /**
         * Render all camera objects.
         * This function is called from a dedicated rendering thread as executed
         * inside the platform API. It must not access ECS data.
         */
        public void Render(in IList<RenderJob> renderJobs)
        {

            Raylib.ClearBackground(Raylib.BLACK);
            int y0Stats = 30;

            foreach(var renderJob in renderJobs)
            {
                var cCameraParams = renderJob.Camera3;
                var mToWorld = renderJob.Transform3ToWorld.Matrix;

                var vCameraPosition = mToWorld.Translation;
                Vector3 vY;
                Vector3 vUp = vY = new Vector3(mToWorld.M21, mToWorld.M22, mToWorld.M23);
                Vector3 vZ = new Vector3(-mToWorld.M31, -mToWorld.M32, -mToWorld.M33);
                Vector3 vFront = -vZ;
                Vector3 vTarget = vCameraPosition + vFront;

                var rCamera = new Raylib_CsLo.Camera3D( vCameraPosition, vTarget, vUp, 
                    cCameraParams.Angle, CameraProjection.CAMERA_PERSPECTIVE);

                // TXWTODO: Hack the camera position into the main shader.
                _materialManager.HackSetCameraPos(vCameraPosition);

                /*
                 * We need to reimplement BeginMode3d to freely set frustrums
                 * 
                 * The following code is the explicit wording of BeginMode3d
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
                 * First draw player related stuff
                 */
                // TXWTODO: Nothing here

                /*
                 * Then draw standard world
                 */
                renderJob.CameraOutput.RenderStandard();


                /*
                 * Then render transparent
                 */
                renderJob.CameraOutput.RenderTransparent();

                Raylib.EndMode3D();

                Raylib.DrawText("Debug info:\n" + renderJob.CameraOutput.GetDebugInfo(), 20, y0Stats, 10, Raylib.GREEN);
                y0Stats += 20;
            }

            Raylib.DrawFPS(20, 100);
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
            _musicManager = new(engine);
            _musicManager.Manage(engine.GetEcsWorld());
            _createRlMeshesSystem = new(_engine, _meshManager, _materialManager);
            _createRlMusicSystem = new(_engine);
            _drawRlMeshesSystem = new(_engine);
            _drawSkyboxesSystem = new(_engine);
            _lightManager = new(_engine);
        }
    }
}
