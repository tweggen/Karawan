using System;
using System.Numerics;

using System.Threading.Tasks;
using DefaultEcs;
using BepuUtilities;
using System.Collections.Generic;
using System.ComponentModel;
using static engine.Logger;

using Silk.NET.OpenGL;
using Silk.NET.SDL;


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
        private Vector2 _vLastGlSize = new(0f, 0f);
        

        private GL _gl = null;

        /**
         * Render all camera objects.
         * This function is called from a dedicated rendering thread as executed
         * inside the platform API. It must not access ECS data.
         */
        private void _renderParts(in IList<RenderPart> RenderParts)
        {
            
            bool isFirstPart = true;

            int y0Stats = 30;

            foreach(var RenderPart in RenderParts)
            {
                /*
                 * We clear the screen only before the very first rendering pass.
                 */
                if (isFirstPart)
                {
                    _gl.Clear((uint)ClearBufferMask.ColorBufferBit | (uint)ClearBufferMask.DepthBufferBit);
                    isFirstPart = false;
                }
                else
                {
                    _gl.Clear(ClearBufferMask.DepthBufferBit);
                }
                

                var cCameraParams = RenderPart.Camera3;

                /*
                 * We enable depth test only for the lower 16 bit camera masks.
                 */
                if ((cCameraParams.CameraMask & 0xffff) != 0)
                {
                    _gl.Enable(EnableCap.DepthTest);
                }
                else
                {
                    _gl.Disable(EnableCap.DepthTest);
                }

                var mToWorld = RenderPart.Transform3ToWorld.Matrix;

                var vCameraPosition = mToWorld.Translation;
                Vector3 vY;
                Vector3 vUp = vY = new Vector3(mToWorld.M21, mToWorld.M22, mToWorld.M23);
                Vector3 vZ = new Vector3(-mToWorld.M31, -mToWorld.M32, -mToWorld.M33);
                Vector3 vFront = -vZ;
                Vector3 vTarget = vCameraPosition + vFront;

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
                _gl.Enable(EnableCap.Blend);
                _gl.Disable(EnableCap.CullFace);

                _gl.BlendFuncSeparate(
                    BlendingFactor.SrcAlpha,BlendingFactor.OneMinusSrcAlpha,
                    BlendingFactor.Zero,BlendingFactor.One);
                _gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
                
                RenderPart.CameraOutput.RenderTransparent(_threeD);
                
                _gl.Disable(EnableCap.Blend);
                _gl.Enable(EnableCap.CullFace);
                y0Stats += 20;
            }

        }


        private void _nailViewport(bool force = false)
        {
            if (null == _gl)
            {
                return;
            }

            if (force || _vLastGlSize != _vViewSize)
            {
                _gl.Viewport(0, 0, (uint)_vViewSize.X, (uint)_vViewSize.Y);
                _vLastGlSize = _vViewSize;
            }
        }
        

        public void RenderFrame(in RenderFrame renderFrame)
        {
            _gl = _silkThreeD.GetGL();
            /*
             * Switch to the main viewport.
             */
            _gl.BindFramebuffer(GLEnum.Framebuffer, 0);
            _nailViewport(true);
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
                _nailViewport();
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
