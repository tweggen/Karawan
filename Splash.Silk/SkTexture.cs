using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using static engine.Logger;

namespace Splash.Silk;

public class SkTexture : IDisposable
{
    private uint _handle;

    private bool _doFilter = false;
    
    /*
     * Data generation that had been uploaded.
     */
    private uint _generation = 0xfffffffe;

    public uint Generation
    {
        get => _generation; 
    }
    
    private GL _gl;
    
    public uint Handle
    {
        get => _handle;
    }
    
    public void CheckError(string what)
    {
        var error = _gl.GetError();
        if (error != GLEnum.NoError)
        {
            Error( $"Found OpenGL {what} error {error}" );
            if (what == "ActiveTexture")
            {
                Console.WriteLine("ActiveTexture");
            }
        }
        else
        {
            // Console.WriteLine($"OK: {what}");
        }
    }
    

    private void _setParameters()
    {
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int) GLEnum.Repeat);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int) GLEnum.Repeat);

        if (_doFilter)
        {
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) GLEnum.LinearMipmapLinear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        }
        else
        {
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) GLEnum.Nearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        }

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 8);
    }


    private void _generateMipmap()
    {
        _gl.GenerateMipmap(TextureTarget.Texture2D);
        CheckError("GenerateMipMap");
    }


    public unsafe void SetFrom(string path)
    {
        // Trace("Creating new Texture from path {path}");

        Bind();
        try
        {
            System.IO.Stream streamImage = engine.Assets.Open(path);
            var img = Image.Load<Rgba32>(streamImage);
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)img.Width, (uint)img.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
                CheckError("TexImage2D");

                img.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        fixed (void* data = accessor.GetRowSpan(y))
                        {
                            _gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, y, (uint)accessor.Width, 1, PixelFormat.Rgba, PixelType.UnsignedByte, data);
                        }
                    }
                });
            }
        }
        catch (Exception e)
        {
            /*
             * As a fallback, generate a single-colored texture.
             */
            byte[] arrData = new byte[4] { 128, 128, 0, 255};
            Span<byte> spanData = arrData.AsSpan();
            fixed (void* d = &spanData[0])
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba, 1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, d);
                CheckError("TexImage2D");
            }
        }
        _generateMipmap();
    }

    
    public unsafe void SetFrom(uint generation, Span<byte> data, uint width, uint height)
    {
        // Trace($"Creating new Texture from Span {width}x{height}");
        if (_generation == generation)
        {
            Warning($"Superfluous call to SetFrom from Span, identical generation {generation}.");
            return;
        }
        _generation = generation;
        Bind();
        fixed (void* d = &data[0])
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, (int) InternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, d);
            CheckError("TexImage2D");
        }
        _generateMipmap();
    }


    public unsafe void SetFrom(uint width, uint height)
    {
        // Trace($"Creating new Texture {width}x{height}");
        Bind();
        _gl.TexImage2D(TextureTarget.Texture2D, 0, (int) InternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
        _generateMipmap();
    }


    public void BindAndActive(TextureUnit textureSlot)
    {
        _gl.ActiveTexture(textureSlot);
        CheckError("ActiveTexture");
        _gl.BindTexture(TextureTarget.Texture2D, _handle);
        CheckError("Bind Texture");
    }

    public void Bind()
    {
        _gl.BindTexture(TextureTarget.Texture2D, _handle);
        CheckError("Bind Texture");
    }
    
    
    public void Dispose()
    {
        _gl.DeleteTexture(_handle);
        CheckError("DeleteTexture");
    }

    
    public unsafe SkTexture(GL gl, bool doFilter)
    {
        _gl = gl;
        _doFilter = doFilter;
        _handle = _gl.GenTexture();
        Bind();
        _setParameters();
    }



}