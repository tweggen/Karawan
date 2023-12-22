using System;
using System.Numerics;

using System.Threading.Tasks;
using DefaultEcs;
using BepuUtilities;
using System.Collections.Generic;
using System.ComponentModel;
using engine;
using static engine.Logger;

using Silk.NET.OpenGL;
using Silk.NET.SDL;
using Splash.components;
using Renderbuffer = engine.joyce.Renderbuffer;
using static Splash.Silk.GLCheck;

namespace Splash.Silk
{
    class SilkRenderer
    {
        private object _lo = new();

        private engine.Engine _engine;

        private readonly IThreeD _threeD;
        private readonly SilkThreeD _silkThreeD;
        private readonly TextureManager _textureManager;
        
        /**
         * This is the size of the physical view
         */
        private Vector2 _vViewSize;
        
        /**
         * This is the size of the logical view.
         */
        private Vector2 _v3dSize;
        private Vector2 _vLastGlSize = new(0f, 0f);

        private GL _gl = null;

        private uint _frameNumber = 0;

        /**
         * Render all camera objects.
         * This function is called from a dedicated rendering thread as executed
         * inside the platform API. It must not access ECS data.
         */
        private void _renderParts(in IList<RenderPart> renderParts)
        {
            bool isFirstPart = true;

            int y0Stats = 30;

            ++_frameNumber;

            // was sFactorAlpha Zero and One before. Why does this work?
            _gl.BlendFuncSeparate(
                BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha,
                BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            _gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
            
            foreach(var renderPart in renderParts)
            {
                /*
                 * We clear the screen only before the very first rendering pass.
                 * In all other passes, we clear the depth buffer only.
                 */
                bool clearDepthBuffer = false;
                bool clearAll = false;

                Renderbuffer renderbuffer = renderPart.PfRenderbuffer.Renderbuffer;
                SkRenderbufferEntry skRenderbufferEntry = renderPart.PfRenderbuffer.RenderbufferEntry as SkRenderbufferEntry;
                
                bool haveRenderbuffer = renderbuffer != null && skRenderbufferEntry != null;
                
                if (!haveRenderbuffer)
                {
                    if (isFirstPart)
                    {
                        clearAll = true;
                        isFirstPart = false;
                    }
                    else
                    {
                        clearDepthBuffer = true;
                    }
                }
                else
                {
                    if (renderbuffer.LastFrame != _frameNumber)
                    {
                        clearAll = true;
                    }
                    else
                    {
                        clearDepthBuffer = true;
                    }
                    
                    renderbuffer.LastFrame = _frameNumber;
                }

                /*
                 * Switch to the appropriate renderbuffer
                 */
                if (haveRenderbuffer)
                {
                    if (!skRenderbufferEntry.IsUploaded())
                    {
                        skRenderbufferEntry.Upload(_gl, _textureManager);
                    }
                    skRenderbufferEntry.Use(_gl);
                    // _gl.Viewport(0, 0, renderbuffer.Width, renderbuffer.Height);
                }
                else
                {
                    _gl.BindFramebuffer(GLEnum.Framebuffer, 0);
                    _nailViewport(true, true);
                }
                
                if (clearDepthBuffer)
                {
                    _gl.Clear(ClearBufferMask.DepthBufferBit);
                }

                if (clearAll)
                {            
                    /*
                     * Before any frame, clear colors and depth.
                     */
                    _gl.Clear((uint)ClearBufferMask.ColorBufferBit | (uint)ClearBufferMask.DepthBufferBit);
                }

                var cCameraParams = renderPart.CameraOutput.Camera3;

                if ((cCameraParams.CameraFlags & engine.joyce.components.Camera3.Flags.EnableFog) != 0)
                {
                    _silkThreeD.SetFogDistance(200f);
                }
                else
                {
                    _silkThreeD.SetFogDistance(0.5f);
                }
                /*
                 * We enable depth test only for the lower 16 bit camera masks.
                 */
                if ((cCameraParams.CameraFlags & engine.joyce.components.Camera3.Flags.DisableDepthTest) == 0)
                {
                    _gl.Enable(EnableCap.DepthTest);
                }
                else
                {
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
                Vector2 v3dSize;

                if (haveRenderbuffer)
                {
                    v3dSize = new(renderbuffer.Width, renderbuffer.Height);
                }
                else
                {
                    v3dSize = _v3dSize;
                }
                cCameraParams.GetProjectionMatrix(out Matrix4x4 matProjection, v3dSize);

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
                CheckError(_gl, $"glViewport {_v3dSize}");
                _vLastGlSize = _v3dSize;
            }
        }
        

        public void RenderFrame(in RenderFrame renderFrame)
        {
            _silkThreeD.LoadFrame(renderFrame);
            _gl = _silkThreeD.GetGL();
            
            /*
             * Switch to the main viewport.
             */
            _gl.BindFramebuffer(GLEnum.Framebuffer, 0);
            CheckError(_gl, "glBindFramebuffer");
            _nailViewport(true, true);
            _renderParts(renderFrame.RenderParts);
            _nailViewport(false, true);
            _silkThreeD.UnloadAfterFrame();
        }


        public void SetDimension(int x, int y)
        {
            if (x != 0 && y != 0)
            {
                _vViewSize = new Vector2((float)x, (float)y);
            }
        }
        

        public SilkRenderer()
        {
            _engine = I.Get<Engine>();
            _textureManager = I.Get<TextureManager>();
            _threeD = I.Get<IThreeD>();
            _silkThreeD = _threeD as SilkThreeD;
            _vViewSize = new Vector2(1280, 720);
        }
    }
}
