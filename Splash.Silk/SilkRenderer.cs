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

        private readonly IThreeD _threeD;
        private readonly SilkThreeD _silkThreeD;
        private readonly LightManager _lightManager;
        /**
         * This is the size of the physical view
         */
        private Vector2 _vViewSize;
        
        /**
         * This is the size of the logical view.
         */
        private Vector2 _v3dSize;
        private Vector2 _vLastGlSize = new(0f, 0f);

        private SkShaderEntry _skShaderEntry;

        private GL _gl = null;

        /**
         * Render all camera objects.
         * This function is called from a dedicated rendering thread as executed
         * inside the platform API. It must not access ECS data.
         */
        private void _renderParts(in IList<RenderPart> renderParts)
        {
            int nSkipped = 0;
            int nTotal = 0;

            bool isFirstPart = true;

            int y0Stats = 30;

            // was sFactorAlpha Zero and One before. Why does this work?
            _gl.BlendFuncSeparate(
                BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha,
                BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            _gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
            
            /*
             * Before any frame, clear colors and depth.
             */
            _gl.Clear((uint)ClearBufferMask.ColorBufferBit | (uint)ClearBufferMask.DepthBufferBit);
            
            foreach(var renderPart in renderParts)
            {
                /*
                 * We clear the screen only before the very first rendering pass.
                 * In all other passes, we clear the depth buffer only.
                 */
                if (isFirstPart)
                {
                    isFirstPart = false;
                }
                else
                {
                    _gl.Clear(ClearBufferMask.DepthBufferBit);
                }
                

                var cCameraParams = renderPart.CameraOutput.Camera3;

                /*
                 * We enable depth test only for the lower 16 bit camera masks.
                 */
                if ((cCameraParams.CameraMask & 0xffff) != 0)
                {
                    _skShaderEntry.SkShader.SetUniform("fogDistance", 200f);
                    _gl.Enable(EnableCap.DepthTest);
                }
                else
                {
                    _skShaderEntry.SkShader.SetUniform("fogDistance", 0.5f);
                    _gl.Disable(EnableCap.DepthTest);
                }

                var mCameraToWorld = renderPart.CameraOutput.TransformToWorld;
                {
                    var vCameraPosition = mCameraToWorld.Translation;
                    // TXWTODO: Bad API. Use OnCameraCHanged API?
                    _threeD.SetCameraPos(vCameraPosition);
                }
                
                /*
                 * Create the model/projection/view matrix for use in the scaler.
                 * This part of code only sets up the view matrix (how to move the world into
                 * the camera's point of view) and the projection matrix (how to move camera 3d
                 * coordinates into clip space, i.e. 2d screen with depth info).
                 */
                cCameraParams.GetViewMatrix(out Matrix4x4 matView, mCameraToWorld);
                _silkThreeD.SetViewMatrix(matView);
                cCameraParams.GetProjectionMatrix(out Matrix4x4 matProjection, _v3dSize);
                _silkThreeD.SetProjectionMatrix(matProjection);


                /*
                 * First draw player related stuff
                 */

                /*
                 * Then draw standard world
                 */
                renderPart.CameraOutput.RenderStandard(_threeD);


                /*
                 * Then render transparent
                 */
                _gl.Enable(EnableCap.Blend);
                _gl.Disable(EnableCap.CullFace);

                renderPart.CameraOutput.RenderTransparent(_threeD);
                
                _gl.Disable(EnableCap.Blend);
                _gl.Enable(EnableCap.CullFace);
                y0Stats += 20;

                {
                    renderPart.CameraOutput.GetRenderStats(out int skippedNow, out int totalNow);
                    nSkipped += skippedNow;
                    nTotal += totalNow;
                }
            }

        }


        private void _nailViewport(bool useViewRectangle, bool force = false)
        {
            Vector2 vDesiredSize;
            Vector2 ul;
            if (useViewRectangle)
            {
                _engine.GetViewRectangle(out ul, out var lr);
                if (Vector2.Zero == lr)
                {
                    lr = _vViewSize - Vector2.One;
                    vDesiredSize = _vViewSize - ul;
                }
                else
                {
                    vDesiredSize = lr - ul + Vector2.One;
                }
            }
            else
            {
                ul = Vector2.Zero;
                vDesiredSize =  _vViewSize;
            }

            if (null != _gl && (force || _vLastGlSize != vDesiredSize))
            {
                _v3dSize = vDesiredSize;
                _gl.Viewport((int)ul.X, (int)(_vViewSize.Y-vDesiredSize.Y-ul.Y),
                    (uint)(vDesiredSize.X), (uint)(vDesiredSize.Y));
                _silkThreeD.CheckError($"glViewport {_v3dSize}");
                _vLastGlSize = _v3dSize;
            }
        }
        

        public void RenderFrame(in RenderFrame renderFrame)
        {
            _gl = _silkThreeD.GetGL();
            
            /*
             * Switch to the main viewport.
             */
            _gl.BindFramebuffer(GLEnum.Framebuffer, 0);
            _silkThreeD.CheckError("glBindFramebuffer");
            _nailViewport(true, true);
            _skShaderEntry = _silkThreeD.GetInstanceShaderEntry();
            _skShaderEntry.SkShader.Use();
            _lightManager.ApplyLights(renderFrame, _skShaderEntry);
            // _gl.UseProgram(0);
            _renderParts(renderFrame.RenderParts);
            _nailViewport(false, true);
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
