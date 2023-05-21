using Silk.NET.OpenGL;
using static engine.Logger;

namespace Splash.Silk;

public class SkFramebuffer : AFramebuffer
{
    private uint _handleFramebuffer = 0;
    private uint _handleDepthbuffer = 0;
    private engine.joyce.Texture _jTexture;
    private SkTexture _skTexture = null;
    private SkTextureEntry _skTextureEntry = null;
    private DrawBufferMode[] _modesDrawbuffer = null;
    
    public override bool IsUploaded()
    {
        return false;
    }


    public void Use(GL gl)
    {
        gl.BindFramebuffer(GLEnum.Framebuffer, _handleFramebuffer);
        gl.Viewport(0, 0, JFramebuffer.Width, JFramebuffer.Height);
    }
    

    public void Release(GL gl)
    {
        
    }
    

    public unsafe void Upload(GL gl, in TextureManager textureManager)
    {
        if (_handleFramebuffer != 0)
        {
            ErrorThrow( "Framebuffer already uploaded.", (m)=>new ArgumentException());
        }

        fixed (uint* pHandle = &_handleFramebuffer)
        {
            gl.GenFramebuffers(1U, pHandle);
        }

        _jTexture = new engine.joyce.Texture($"framebuffer://{JFramebuffer.Name}");
        _skTexture = new SkTexture(gl, JFramebuffer.Width, JFramebuffer.Height);
        _skTextureEntry = new SkTextureEntry(_jTexture);
        _skTextureEntry.SkTexture = _skTexture;
        
        /*
         * Use it, adding the depth buffer.
         */
        gl.BindFramebuffer(GLEnum.Framebuffer, _handleFramebuffer);

        fixed (uint *pHandle = &_handleDepthbuffer) {
            gl.GenRenderbuffers(1, pHandle);
        }

        uint width = JFramebuffer.Width;
        uint height = JFramebuffer.Height;
        
        gl.BindRenderbuffer(GLEnum.Renderbuffer, _handleDepthbuffer);
        gl.RenderbufferStorage(GLEnum.Renderbuffer, GLEnum.DepthComponent, 
            width, height);
        gl.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.DepthAttachment,
            GLEnum.Renderbuffer, _handleDepthbuffer);
        gl.FramebufferTexture(GLEnum.Framebuffer, GLEnum.ColorAttachment0, _skTexture.Handle, 0);
        _modesDrawbuffer = new DrawBufferMode[1];
        fixed (DrawBufferMode* pModes = _modesDrawbuffer)
        {
            gl.DrawBuffers(1, pModes);
        }

        if (gl.CheckFramebufferStatus(GLEnum.Framebuffer) != GLEnum.FramebufferComplete)
        {
            Error("Unable to initialize frame buffer");
        }
        Trace( $"Uploaded texture {_jTexture.Source}.");

        textureManager.PushTexture(_jTexture.Source, _skTextureEntry);
    }
    
    
    public SkFramebuffer(in engine.joyce.Framebuffer jFramebuffer) : base(jFramebuffer)
    {
    }
}