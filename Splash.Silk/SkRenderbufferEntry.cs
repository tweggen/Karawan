using Silk.NET.OpenGL;
using static engine.Logger;

namespace Splash.Silk;

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

    
    public int CheckError(string what)
    {
        int err = 0;
        while (true)
        {
            var error = _gl.GetError();
            if (error != GLEnum.NoError)
            {
                Error($"Found OpenGL {what} error {error}");
                err += (int)error;
            }
            else
            {
                // Console.WriteLine($"OK: {what}");
                return err;
            }
        }
    }


    public void Use(GL gl)
    {
        _gl = gl;
        gl.BindFramebuffer(GLEnum.Framebuffer, _handleFramebuffer);
        CheckError("SkRenderbuffer BindFramebuffer");
        gl.Viewport(0, 0, JRenderbuffer.Width, JRenderbuffer.Height);
        CheckError("SkRenderbuffer Viewport");
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

        fixed (uint* pHandle = &_handleFramebuffer)
        {
            gl.GenFramebuffers(1U, pHandle);
            CheckError("SkRenderbuffer GenFramebuffers");
        }

        _jTextureName = JRenderbuffer.TextureName;
        _jTexture = new engine.joyce.Texture(_jTextureName);
        _skTexture = new SkTexture(gl, false);
        _skTexture.SetFrom(JRenderbuffer.Width, JRenderbuffer.Height);
        _skTextureEntry = new SkTextureEntry(_jTexture);
        _skTextureEntry.SkTexture = _skTexture;
        
        /*
         * Use it, adding the depth buffer.
         */
        gl.BindFramebuffer(GLEnum.Framebuffer, _handleFramebuffer);
        CheckError("SkRenderbuffer BindFramebuffer");

        fixed (uint *pHandle = &_handleDepthbuffer) {
            gl.GenRenderbuffers(1, pHandle);
            CheckError("SkRenderbuffer GenRenderbuffers");
        }

        uint width = JRenderbuffer.Width;
        uint height = JRenderbuffer.Height;
        
        gl.BindRenderbuffer(GLEnum.Renderbuffer, _handleDepthbuffer);
        CheckError("SkRenderbuffer BindRenderbuffers");
        gl.RenderbufferStorage(GLEnum.Renderbuffer, GLEnum.DepthComponent, 
            width, height);
        CheckError("SkRenderbuffer RenderbufferStorage");
        gl.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.DepthAttachment,
            GLEnum.Renderbuffer, _handleDepthbuffer);
        CheckError("SkRenderbuffer FramebufferRenderbuffer");
        gl.FramebufferTexture(GLEnum.Framebuffer, GLEnum.ColorAttachment0, _skTexture.Handle, 0);
        CheckError("SkRenderbuffer FramebufferTexture");
        _modesDrawbuffer = new DrawBufferMode[1];
        fixed (DrawBufferMode* pModes = _modesDrawbuffer)
        {
            gl.DrawBuffers(1, pModes);
            CheckError("SkRenderbuffer DrawBuffers");
        }

        if (gl.CheckFramebufferStatus(GLEnum.Framebuffer) != GLEnum.FramebufferComplete)
        {
            Error("Unable to initialize frame buffer");
        }
        CheckError("SkRenderbuffer CheckFramebufferStatus");
        Trace( $"Uploaded texture {_jTexture.Source}.");

        textureManager.PushTexture(_jTexture.Source, _skTextureEntry);
    }
    
    
    public SkRenderbufferEntry(in engine.joyce.Renderbuffer jRenderbuffer) : base(jRenderbuffer)
    {
    }
}