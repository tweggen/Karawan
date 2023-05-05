using System;
using System.Numerics;

using System.Threading.Tasks;
using DefaultEcs;
using BepuUtilities;
using System.Collections.Generic;
using System.ComponentModel;
using static engine.Logger;

using Silk.NET.OpenGL;


namespace Splash.Silk
{
    class SilkRenderer
    {
        private object _lo = new();

        private engine.Engine _engine;

        private IThreeD _threeD;
        private SilkThreeD _silkThreeD;
        private LightManager _lightManager;
        private Vector2 _vViewSize;

        private GL _gl;

        /**
         * Render all camera objects.
         * This function is called from a dedicated rendering thread as executed
         * inside the platform API. It must not access ECS data.
         */
        private void _renderParts(in IList<RenderPart> RenderParts)
        {
            _silkThreeD.CheckError("Beginning renderParts");
            _gl.Enable(EnableCap.DepthTest);
            _silkThreeD.CheckError("Enable Depth");
            _gl.Clear((uint) (ClearBufferMask.ColorBufferBit  | ClearBufferMask.DepthBufferBit));
            _silkThreeD.CheckError("Clear");

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

                //var rCamera = new Raylib_CsLo.Camera3D( vCameraPosition, vTarget, vUp, 
                //    cCameraParams.Angle, CameraProjection.CAMERA_PERSPECTIVE);

                _threeD.SetCameraPos(vCameraPosition);

                /*
                 * Create the model/projection/view matrix for use in the scaler.
                 * This part of code only sets up the view matrix (how to move the world into
                 * the camera's point of view) and the projection matrix (how to move camera 3d
                 * coordinates into clip space, i.e. 2d screen with depth info).
                 */
                {
                    Matrix4x4 matView = Matrix4x4.CreateLookAt(vCameraPosition, vCameraPosition+vZ , vUp);
                    _silkThreeD.SetViewMatrix(matView);
                    float right = cCameraParams.NearFrustum * (float)Math.Tan(cCameraParams.Angle * 0.5f * (float)Math.PI / 180f);
                    float invAspect = _vViewSize.Y / _vViewSize.X;
                    float top = right * invAspect;
                    Matrix4x4 matProjection;
                    {
                        float n = cCameraParams.NearFrustum;
                        float f = cCameraParams.FarFrustum;
                        float l = -right;
                        float r = right;
                        float t = top;
                        float b = -top;
                        Matrix4x4 m = new(
                            2f * n / (r - l), 0f, (r+l)/(r-l), 0f,
                            0f, 2f * n / (t - b), (t+b)/(t-b), 0f,
                            0f, 0f, -(f + n) / (f - n), -2f * f * n / (f - n),
                            0f, 0f, -1f, 0f
                        );
                        // TXWTODO: We need a smarter way to fix that to the view.
                        Matrix4x4 mScaleToViewWindow =
                            Matrix4x4.Identity;
                        
                            /* new(
                            1f/_vViewSize.X, 0f, 0f, 0f,
                            0f, 1f/_vViewSize.Y, 0f, 0f,
                            0f, 0f, 1f, 0f,
                            0f, 0f, 0f, 1f
                        ); */
                        matProjection = Matrix4x4.Transpose(m*mScaleToViewWindow);
                    }
                    _silkThreeD.SetProjectionMatrix(matProjection);
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
                y0Stats += 20;
            }

        }


        public void RenderFrame(in RenderFrame renderFrame)
        {
            _gl = _silkThreeD.GetGL();
            var skShaderEntry = _silkThreeD.GetInstanceShaderEntry(); 
            _gl.UseProgram(skShaderEntry.SkShader.Handle);
            _lightManager.ApplyLights(renderFrame, _silkThreeD.GetInstanceShaderEntry());
            // _gl.UseProgram(0);
            _renderParts(renderFrame.RenderParts);
        }


        public void SetDimension(int x, int y)
        {
            if (x != 0 && y != 0)
            {
                _vViewSize = new Vector2((float)x, (float)y);
            }
        }
        

        public SilkRenderer(
            in engine.Engine engine,
            in LightManager lightManager,
            in SilkThreeD silkThreeD)
        {
            _engine = engine;
            _lightManager = lightManager;
            _silkThreeD = silkThreeD;
            _threeD = silkThreeD;
            _vViewSize = new Vector2(1280, 720);
        }
    }
}
