using System;
using System.Numerics;
using Splash.API.OpenGL;
using static engine.Logger;

namespace Splash.OpenGL;

public class SkRenderbufferEntry : ARenderbufferEntry
{
    private GL _gl;
    private uint _handleFramebuffer = 0;
    private uint _handleDepthbuffer = 0;
    private engine.joyce.Texture _jTexture;
    private SkTexture _skTexture = null;
    private SkTextureEntry _skTextureEntry = null;
    private DrawBufferMode[] _modesDrawbuffer = null;
    private string _jTextureName = "";
    
    
    public override bool IsUploaded()
    {
        return _handleFramebuffer != 0;
    }

    



    public void Use(GL gl, Vector2 ul, Vector2 lr)
    {
        _gl = gl;
        gl.BindFramebuffer(GLEnum.Framebuffer, _handleFramebuffer);
        GlDbg.Check(_gl);

        Vector2 v2Ul = ul;
        Vector2 v2Lr = lr;
        v2Ul.X *= JRenderbuffer.Width;
        v2Ul.Y *= JRenderbuffer.Height;
        v2Lr.X *= JRenderbuffer.Width;
        v2Lr.Y *= JRenderbuffer.Height;
        Vector2 v2Size = v2Lr - v2Ul;
        gl.Viewport((int)v2Ul.X, (int)v2Ul.Y, (uint)v2Size.X, (uint)v2Size.Y);
        GlDbg.Check(_gl);
    }
    

    public void Release(GL gl)
    {
        _gl = gl;
        Error("Not yet implemented.");
    }
    
    public unsafe void Upload(GL gl, in TextureManager textureManager)
    {
        _gl = gl;

        if (_handleFramebuffer != 0)
        {
            ErrorThrow( "Framebuffer already uploaded.", (m)=>new ArgumentException());
        }

        /*
         * Create a framebuffer.
         *
         * - create a texture, add it as color attachment
         * - create a renderbuffer, add it as depth attachment.
         */

        /*
         * First, the framebuffer with our internal api.
         */
        _jTextureName = JRenderbuffer.TextureName;
        _jTexture = new engine.joyce.Texture(_jTextureName);
        _skTexture = new SkTexture(gl, _jTexture, engine.joyce.Texture.FilteringModes.Framebuffer);
        _skTexture.SetFrom(JRenderbuffer.Width, JRenderbuffer.Height);
        _skTextureEntry = new SkTextureEntry(_jTexture);
        _skTextureEntry.SkTexture = _skTexture;
        
        /*
         * Now, create the framebuffer and bind it as current.
         */
        fixed (uint* pHandle = &_handleFramebuffer)
        {
            gl.GenFramebuffers(1U, pHandle);
            GlDbg.Check(_gl);
        }
        gl.BindFramebuffer(GLEnum.Framebuffer, _handleFramebuffer);
        GlDbg.Check(_gl);

        /*
         * Assign the texture as color channel.
         */
        gl.FramebufferTexture(GLEnum.Framebuffer, GLEnum.ColorAttachment0, _skTexture.Handle, 0);
        GlDbg.Check(_gl);

        /*
         * Create a renderbuffer for depth.
         */
        fixed (uint *pHandle = &_handleDepthbuffer) {
            gl.GenRenderbuffers(1, pHandle);
            GlDbg.Check(_gl);
        }
        /*
         * Bind as current ...
         */
        gl.BindRenderbuffer(GLEnum.Renderbuffer, _handleDepthbuffer);
        GlDbg.Check(_gl);
        /*
         * ... allocate ram ...
         */
        gl.RenderbufferStorage(GLEnum.Renderbuffer, GLEnum.DepthComponent24, 
            JRenderbuffer.Width, JRenderbuffer.Height);
        GlDbg.Check(_gl);
        /*
         * ... bind it to the framebufffer.
         */
        gl.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.DepthAttachment,
            GLEnum.Renderbuffer, _handleDepthbuffer);
        GlDbg.Check(_gl);
        
        
        _modesDrawbuffer = new DrawBufferMode[1];
        _modesDrawbuffer[0] = DrawBufferMode.ColorAttachment0;
        fixed (DrawBufferMode* pModes = _modesDrawbuffer)
        {
            gl.DrawBuffers(1, pModes);
            GlDbg.Check(_gl);
        }

        if (gl.CheckFramebufferStatus(GLEnum.Framebuffer) != GLEnum.FramebufferComplete)
        {
            Error("Unable to initialize frame buffer");
        }
        GlDbg.Check(_gl);
        Trace( $"Uploaded texture {_jTexture.Key}.");

        textureManager.PushTexture(_jTexture.Key, _skTextureEntry);
    }
    
    
    public SkRenderbufferEntry(in engine.joyce.Renderbuffer jRenderbuffer) : base(jRenderbuffer)
    {
    }
}