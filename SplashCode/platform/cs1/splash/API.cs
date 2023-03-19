using System;
using System.Numerics;
using Raylib_CsLo;

using System.Threading.Tasks;
using DefaultEcs;
using BepuUtilities;
using System.Collections.Generic;
using System.ComponentModel;
using static engine.Logger;

namespace Karawan.platform.cs1.splash
{
    class API
    {
        private object _lo = new();

        private engine.Engine _engine;

        private systems.CreateRlMeshesSystem _createRlMeshesSystem;
        private systems.DrawRlMeshesSystem _drawRlMeshesSystem;
        private systems.DrawSkyboxesSystem _drawSkyboxesSystem;

        private MaterialManager _materialManager;
        private TextureGenerator _textureGenerator;
        private TextureManager _textureManager;
        private RaylibThreeD _raylibThreeD;
        private MeshManager _meshManager;
        private LightManager _lightManager;

        private Queue<RenderFrame> _renderQueue = new();

        /**
         * Collect the output of the cameras for later rendering.
         */
        private void _logicalRenderFrame(RenderFrame renderFrame)
        {
            var listCameras = _engine.GetEcsWorld().GetEntities()
                .With<engine.joyce.components.Camera3>()
                .With<engine.transform.components.Transform3ToWorld>()
                .AsEnumerable();

            foreach (var eCamera in listCameras)
            {
                RenderPart renderPart = new();
                renderPart.Camera3 = eCamera.Get<engine.joyce.components.Camera3>();
                renderPart.Transform3ToWorld = eCamera.Get<engine.transform.components.Transform3ToWorld>();
                CameraOutput cameraOutput = new(_raylibThreeD, _materialManager, _meshManager, renderPart.Camera3.CameraMask);
                renderPart.CameraOutput = cameraOutput;

                _drawRlMeshesSystem.Update(cameraOutput);

                var vCameraPosition = renderPart.Transform3ToWorld.Matrix.Translation;
                _drawSkyboxesSystem.CameraPosition = vCameraPosition;
                _drawSkyboxesSystem.Update(cameraOutput);

                renderFrame.RenderParts.Add(renderPart);
            }
        }


        /**
         * Called from the logical thread context every logical frame.
         * If behavior doesn't mess up.
         */
        public void CollectRenderData()
        {
            _createRlMeshesSystem.Update(_engine);

            RenderFrame renderFrame = null;
            /*
             * If we currently are not rendering, collect the data for the next 
             * rendering job. The entity system can only be read from this thread.
             */
            lock(_lo)
            {
                /*
                 * Append a new render job if there is nothing to render.
                 */
                if (_renderQueue.Count == 0)
                {
                    renderFrame = new();
                }
            }

            if(null != renderFrame)
            {
                /*
                 * Create/upload all ressources that haven't been uploaded.
                 */
                lock (_lightManager)
                {
                    _lightManager.CollectLights(renderFrame);
                }
                _logicalRenderFrame(renderFrame);
                lock(_lo)
                {
                    _renderQueue.Enqueue(renderFrame);
                }
            }
        }


        /**
         * Render all camera objects.
         * This function is called from a dedicated rendering thread as executed
         * inside the platform API. It must not access ECS data.
         */
        private void _renderParts(in IList<RenderPart> RenderParts)
        {
            Raylib.BeginDrawing();

            Raylib.ClearBackground(Raylib.BLACK);
            int y0Stats = 30;

            foreach(var RenderPart in RenderParts)
            {
                var cCameraParams = RenderPart.Camera3;
                var mToWorld = RenderPart.Transform3ToWorld.Matrix;

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
                RenderPart.CameraOutput.RenderStandard();


                /*
                 * Then render transparent
                 */
                RenderPart.CameraOutput.RenderTransparent();

                Raylib.EndMode3D();

                Raylib.DrawText("Debug info:\n" + RenderPart.CameraOutput.GetDebugInfo(), 20, y0Stats, 10, Raylib.GREEN);
                y0Stats += 20;
            }

            Raylib.DrawFPS(20, 100);
            Raylib.DrawText("codename Karawan", 20, 20, 10, Raylib.GREEN);
            Raylib.EndDrawing();
        }


        private void _renderFrame(in RenderFrame renderFrame)
        {
            _lightManager.ApplyLights(renderFrame, _raylibThreeD.GetInstanceShaderEntry());
            _renderParts(renderFrame.RenderParts);
        }



        /**
         * Called from Platform
         */
        public void RenderFrame()
        {
            RenderFrame renderFrame = null;
            lock(_lo)
            {
                if (_renderQueue.Count > 0)
                {
                    renderFrame = _renderQueue.Dequeue();
                }
            }
            if (renderFrame != null)
            {
                _renderFrame(renderFrame);
            } else
            {
                Warning("No new frame found.");
                System.Threading.Thread.Sleep(15);
            }
        }

        public API(engine.Engine engine)
        {
            _engine = engine;
            _textureGenerator = new TextureGenerator(engine);
            _textureManager = new(_textureGenerator);
            _raylibThreeD = new RaylibThreeD(_engine, _textureManager);
            _materialManager = new(_raylibThreeD, _textureManager);
            _materialManager.Manage(engine.GetEcsWorld());
            _meshManager = new(engine, _raylibThreeD);
            _meshManager.Manage(engine.GetEcsWorld());
            _createRlMeshesSystem = new(_engine, _meshManager, _materialManager);
            _drawRlMeshesSystem = new(_engine);
            _drawSkyboxesSystem = new(_engine);
            _lightManager = new(_engine);
        }
    }
}
