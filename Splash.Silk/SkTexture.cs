using System.Numerics;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
//using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using static engine.Logger;

namespace Splash.Silk;

public class SkTexture : IDisposable
{
    private uint _liveHandle;
    private uint _backHandle;

    private bool _doFilter = false;

    /*
     * Data generation that had been uploaded.
     */
    private uint _generation = 0xfffffffe;

    /*
     * Track if the live handle already has been consumed by
     * the renderer.
     */
    private bool _liveConsumed = false;

    public uint Generation
    {
        get => _generation;
    }

    private GL _gl;

    public uint Handle
    {
        /*
         * We do not use a mutex because we knoe that an 32bit assignment is reasonably atomic.
         */
        get => _liveHandle;
    }

    public void CheckError(string what)
    {
        var error = _gl.GetError();
        if (error != GLEnum.NoError)
        {
            Error($"Found OpenGL {what} error {error}");
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
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);

        if (_doFilter)
        {
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int)GLEnum.LinearMipmapLinear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        }
        else
        {
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
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


    private void _backToLive()
    {
        var local = _liveHandle;
        _liveHandle = _backHandle;
        _backHandle = local;
    }


    private void _allocateBack()
    {
        _backHandle = _gl.GenTexture();
        _bindBack();
        _setParameters();
    }


    private void _checkReloadTexture()
    {
        if (_backHandle == 0xffffffff)
        {
            Trace("(First) reload detected. Will allocate back buffer texture.");
            _allocateBack();
        }
    }


    public unsafe void SetFrom(string path)
    {
        // Trace("Creating new Texture from path {path}");

        _checkReloadTexture();
        _bindBack();
        try
        {
            System.IO.Stream streamImage = engine.Assets.Open(path);
            var img = Image.Load<Rgba32>(streamImage);
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)img.Width, (uint)img.Height, 0,
                    PixelFormat.Rgba, PixelType.UnsignedByte, null);
                CheckError("TexImage2D");

                img.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        fixed (void* data = accessor.GetRowSpan(y))
                        {
                            _gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, y, (uint)accessor.Width, 1,
                                PixelFormat.Rgba, PixelType.UnsignedByte, data);
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
            byte[] arrData = new byte[4] { 128, 128, 0, 255 };
            Span<byte> spanData = arrData.AsSpan();
            fixed (void* d = &spanData[0])
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba, 1, 1, 0, PixelFormat.Rgba,
                    PixelType.UnsignedByte, d);
                CheckError("TexImage2D");
            }
        }

        _generateMipmap();
        _backToLive();
    }


    public unsafe void SetFrom(
        uint generation, Span<byte> data, uint width, uint height)
    {
        // Trace($"Creating new Texture from Span {width}x{height}");
        if (_generation == generation)
        {
            Warning($"Superfluous call to SetFrom from Span, identical generation {generation}.");
            return;
        }

        _generation = generation;
        _checkReloadTexture();
        _bindBack();
        fixed (void* d = &data[0])
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba, width, height, 0, PixelFormat.Rgba,
                PixelType.UnsignedByte, d);
            CheckError("TexImage2D");
        }

        _generateMipmap();
        _backToLive();
    }


    public unsafe void SetFrom(uint width, uint height)
    {
        // Trace($"Creating new Texture {width}x{height}");
        _checkReloadTexture();
        _bindBack();
        _gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba, width, height, 0, PixelFormat.Rgba,
            PixelType.UnsignedByte, null);
        _generateMipmap();
    }


    public void BindAndActive(TextureUnit textureSlot)
    {
        _liveConsumed = true;
        _gl.ActiveTexture(textureSlot);
        CheckError("ActiveTexture");
        _gl.BindTexture(TextureTarget.Texture2D, _liveHandle);
        CheckError("Bind Texture");
    }

    private void _bindBack()
    {
        _gl.BindTexture(TextureTarget.Texture2D, _backHandle);
        CheckError("Bind Texture");
    }


    public void Dispose()
    {
        _gl.DeleteTexture(_liveHandle);
        CheckError("DeleteTexture live");
        _gl.DeleteTexture(_backHandle);
        CheckError("DeleteTexture back");
    }


    public unsafe SkTexture(GL gl, bool doFilter)
    {
        _gl = gl;
        _doFilter = doFilter;
        _backHandle = 0xffffffff;
        _liveHandle = 0xffffffff;
        _allocateBack();
    }
    
}