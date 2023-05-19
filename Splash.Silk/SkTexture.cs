using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static engine.Logger;

namespace Splash.Silk;

public class SkTexture : IDisposable
{
    private uint _handle;
    private GL _gl;

    public unsafe SkTexture(GL gl, string path)
    {
        _gl = gl;
        CheckError("Before load path");
        Trace($"Loading {path}");
        _handle = _gl.GenTexture();
        CheckError("GenTexture");
        Bind();

        try
        {
            System.IO.Stream streamImage = engine.Assets.Open(path);
            var img = Image.Load<Rgba32>(streamImage);
            {
                gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)img.Width, (uint)img.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
                CheckError("TexImage2D");

                img.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        fixed (void* data = accessor.GetRowSpan(y))
                        {
                            gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, y, (uint)accessor.Width, 1, PixelFormat.Rgba, PixelType.UnsignedByte, data);
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
                SetParameters();
            }
        }

        SetParameters();
    }

    public unsafe SkTexture(GL gl, Span<byte> data, uint width, uint height)
    {
        _gl = gl;

        _handle = _gl.GenTexture();
        Bind();
            
        fixed (void* d = &data[0])
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, (int) InternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, d);
            CheckError("TexImage2D");
            SetParameters();
        }
    }

    private void SetParameters()
    {
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int) GLEnum.Repeat);
        CheckError("TextureWrapS");
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int) GLEnum.Repeat);
        CheckError("TextureWrapT");
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) GLEnum.LinearMipmapLinear);
        CheckError("TextureMinFilter");
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) GLEnum.Linear);
        CheckError("TextureMagFilter");
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
        CheckError("TextureBaseLevel");
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 8);
        CheckError("TextureMaxLevel");
        _gl.GenerateMipmap(TextureTarget.Texture2D);
        CheckError("GenerateMipMap");
    }

    public void Bind(TextureUnit textureSlot = TextureUnit.Texture0)
    {
        _gl.ActiveTexture(textureSlot);
        CheckError("ActiveTexture");
        _gl.BindTexture(TextureTarget.Texture2D, _handle);
        CheckError("Bind Texture");
    }

    public void Dispose()
    {
        _gl.DeleteTexture(_handle);
        CheckError("DeleteTexture");
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

}