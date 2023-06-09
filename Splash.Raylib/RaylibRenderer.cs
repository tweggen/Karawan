﻿using System;
using System.Numerics;
using Raylib_CsLo;

using System.Threading.Tasks;
using DefaultEcs;
using BepuUtilities;
using System.Collections.Generic;
using System.ComponentModel;
using static engine.Logger;

namespace Splash.Raylib
{
    class RaylibRenderer
    {
        private object _lo = new();

        private engine.Engine _engine;

        private IThreeD _threeD;
        private RaylibThreeD _raylibThreeD;
        private LightManager _lightManager;

        private Vector2 _vViewSize = new Vector2(1280, 720);

        /**
         * Render all camera objects.
         * This function is called from a dedicated rendering thread as executed
         * inside the platform API. It must not access ECS data.
         */
        private unsafe void _renderParts(in IList<RenderPart> RenderParts)
        {
            Raylib_CsLo.Raylib.BeginDrawing();

            Raylib_CsLo.Raylib.ClearBackground(Raylib_CsLo.Raylib.BLACK);
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

                _threeD.SetCameraPos(vCameraPosition);

                /*
                 * We need to reimplement BeginMode3d to freely set frustrums
                 * 
                 * The following code is the explicit wording of BeginMode3d
                 */
                {
                    // RlGl.rlDrawRenderBatchActive();      // Update and draw internal render batch

                    RlGl.rlMatrixMode(RlGl.RL_PROJECTION);    // Switch to projection matrix
                    RlGl.rlPushMatrix();                 // Save previous matrix, which contains the settings for the 2d ortho projection
                    RlGl.rlLoadIdentity();               // Reset current matrix (projection)

                    // Setup perspective projection
                    float right = cCameraParams.NearFrustum * (float)Math.Tan(cCameraParams.Angle * 0.5f * (float)Math.PI / 180f);
                    float invAspect = _vViewSize.Y / _vViewSize.X;
                    float top = right * invAspect;
                    // RlGl.rlFrustum(-right, right, -top, top, cCameraParams.NearFrustum, cCameraParams.FarFrustum);
                    {
                        float n = cCameraParams.NearFrustum;
                        float f = cCameraParams.FarFrustum;
                        float l = -right;
                        float r = right;
                        float t = top;
                        float b = -top;
                        Matrix4x4 m = new(
                            2f * n / (r - l), 0f, 0f, 0f,
                            0f, 2f * n / (t - b), 0f, 0f,
                            0f, 0f, -(f + n) / (f - n), -2f * f * n / (f - n),
                            0f, 0f, -1f, 0f
                        ); 
                        RlGl.rlMultMatrixf(Matrix4x4.Transpose(m));
                    }
                    Matrix4x4 matNativeProjection = RlGl.rlGetMatrixProjection();
                    
                    RlGl.rlMatrixMode(RlGl.RL_MODELVIEW);     // Switch back to modelview matrix
                    RlGl.rlLoadIdentity();               // Reset current matrix (modelview)

                    // Setup Camera view
                    Matrix4x4 matView;
                    matView = Matrix4x4.CreateLookAt(vCameraPosition, vCameraPosition+vZ , vUp);
                    // matView = Matrix4x4.Transpose(matView);
                    // Multiply modelview matrix by view matrix (camera)
                    /*
                     * Note to myself: rlMultMatrixf automatically transposes the matrix it takes as an argument.
                     */
                    RlGl.rlMultMatrixf(matView);

                    RlGl.rlEnableDepthTest();            // Enable DEPTH_TEST for 3D
                }
                    

                /*
                 * First draw player related stuff
                 */
                // TXWTODO: Nothing here

                /*
                 * Then draw standard world
                 */
                RenderPart.CameraOutput.RenderStandard(_threeD);
                

                /*
                 * Then render transparent
                 */
                RenderPart.CameraOutput.RenderTransparent(_threeD);

                Raylib_CsLo.Raylib.EndMode3D();

                Raylib_CsLo.Raylib.DrawText("Debug info:\n" + RenderPart.CameraOutput.GetDebugInfo(), 20, y0Stats, 10, Raylib_CsLo.Raylib.GREEN);
                y0Stats += 20;
            }

            Raylib_CsLo.Raylib.DrawFPS(20, 100);
            Raylib_CsLo.Raylib.DrawText("codename Karawan", 20, 20, 10, Raylib_CsLo.Raylib.GREEN);
            Raylib_CsLo.Raylib.EndDrawing();
        }


        public void RenderFrame(in RenderFrame renderFrame)
        {
            _lightManager.ApplyLights(renderFrame, _raylibThreeD.GetInstanceShaderEntry());
            _renderParts(renderFrame.RenderParts);
        }
        
        
        public void SetDimension(int x, int y)
        {
            _vViewSize = new Vector2((float)x, (float)y);
        }
        

        public RaylibRenderer(
            in engine.Engine engine,
            in LightManager lightManager,
            in RaylibThreeD raylibThreeD)
        {
            _engine = engine;
            _lightManager = lightManager;
            _raylibThreeD = raylibThreeD;
            _threeD = raylibThreeD;
        }
    }
}
