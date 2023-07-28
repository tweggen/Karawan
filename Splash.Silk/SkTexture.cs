using System.ComponentModel.Design;
using System.Numerics;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
//using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using static engine.Logger;
using Trace = System.Diagnostics.Trace;

namespace Splash.Silk;

public class SkTexture : IDisposable
{
    private uint _liveHandle = 0xffffffff;
    private bool _liveBound = false;
    private bool _liveGenerated = false;
    private bool _liveData = false;
    private uint _backHandle = 0xffffffff;
    private bool _backBound = false;
    private bool _backGenerated = false;
    private bool _backData = false;
    

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
        /*
         * We do not use a mutex because we knoe that an 32bit assignment is reasonably atomic.
         */
        get => _liveHandle;
    }

    public int CheckError(string what)
    {
        var error = _gl.GetError();
        if (error != GLEnum.NoError)
        {
            Error($"Found OpenGL {what} error {error}");
            return -(int)error;
        }
        else
        {
            // Console.WriteLine($"OK: {what}");
            return 0;
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
        var localBound = _liveBound;
        var localGenerated = _liveGenerated;
        var localData = _liveData;
        _liveHandle = _backHandle;
        _liveBound = _backBound;
        _liveGenerated = _backGenerated;
        _liveData = _backData;
        _backHandle = local;
        _backBound = localBound;
        _backGenerated = localGenerated;
        _backData = localData;
    }


    private void _allocateBack()
    {
        if (_backGenerated)
        {
            Trace("Already had been generated.");
        }
        _backHandle = _gl.GenTexture();
        _backGenerated = true;
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
                _backData = true;
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
            _backData = true;
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
        _backData = true;
        _gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba, width, height, 0, PixelFormat.Rgba,
            PixelType.UnsignedByte, null);
        _generateMipmap();
    }


    public void BindAndActive(TextureUnit textureSlot)
    {
        _gl.ActiveTexture(textureSlot);
        CheckError("ActiveTexture");
        _gl.BindTexture(TextureTarget.Texture2D, _liveHandle);
        if (0 == CheckError("BindAndActive Texture"))
        {
            _liveBound = true;
        }
    }

    private void _bindBack()
    {
        bool isUnbound = false;
        if (_backBound)
        {
            _gl.BindTexture(TextureTarget.Texture2D, 0);
            _backBound = false;
            isUnbound = true;
        }

        //if (!_backData)
        //{
        //    return;
        //}
        _gl.BindTexture(TextureTarget.Texture2D, _backHandle);
        int err = CheckError("_bindBack Texture");
        if (err<0)
        {
            Trace("Break here.");
        }
        else
        {
            _backBound = true;
        } 
    }


    public void Dispose()
    {
        _gl.DeleteTexture(_liveHandle);
        _liveHandle = 0xffffffff;
        _liveBound = false;
        CheckError("DeleteTexture live");
        _gl.DeleteTexture(_backHandle);
        _backHandle = 0xffffffff;
        _backBound = false;
        CheckError("DeleteTexture back");
    }


    public unsafe SkTexture(GL gl, bool doFilter)
    {
        _gl = gl;
        _doFilter = doFilter;
        _allocateBack();
    }
    
}