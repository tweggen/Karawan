using System.ComponentModel.Design;
using System.Numerics;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
//using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using static engine.Logger;
using Texture = engine.joyce.Texture;
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

    private bool _haveMipmap = false;

    private const int NMipmaps = 5;

    private engine.joyce.Texture.FilteringModes _filteringMode = engine.joyce.Texture.FilteringModes.Pixels;

    private bool _traceTexture = false;
    
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


    public uint UploadHandle
    {
        /*
         * We do not use a mutex because we knoe that an 32bit assignment is reasonably atomic.
         */
        get => _backHandle;
    }
    
    

    private void _trace(string msg)
    {
        if (_traceTexture)
        {
            Trace(msg);
        }
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


    private void _setParameters()
    {
        _trace("_setParameters");
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,
            (int)GLEnum.ClampToEdge);
        CheckError("TexParam WrapS");
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,
            (int)GLEnum.ClampToEdge);
        CheckError("TexParam WrapT");

        switch (_filteringMode)
        {
            case engine.joyce.Texture.FilteringModes.Framebuffer:
                _trace("_setParameters Framebuffer");
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, 
                    (int)GLEnum.Nearest);
                CheckError("TexParam MinFilter");
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                    (int)GLEnum.Nearest);
                CheckError("TexParam MagFilter");
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 
                    0);
                CheckError("TexParam BaseLevel");
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 
                    0);
                CheckError("TexParam MaxLevel");
                break;
            case engine.joyce.Texture.FilteringModes.Pixels:
                _trace("_setParameters Pixels");
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, 
                    (int)GLEnum.NearestMipmapNearest);
                CheckError("TexParam MinFilter");
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                    (int)GLEnum.Nearest);
                CheckError("TexParam MagFilter");
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 
                    0);
                CheckError("TexParam BaseLevel");
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 
                    NMipmaps-1);
                CheckError("TexParam MaxLevel");
                break;
            case engine.joyce.Texture.FilteringModes.Smooth:
                _trace("_setParameters Smooth");
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, 
                    (int)GLEnum.LinearMipmapLinear);
                CheckError("TexParam MinFilter");
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                    (int)GLEnum.Linear);
                CheckError("TexParam MagFilter");
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 
                    0);
                CheckError("TexParam BaseLevel");
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 
                    NMipmaps-1);
                CheckError("TexParam MaxLevel");
                break;
        }

    }


    private void _generateMipmap()
    {
        switch (_filteringMode)
        {
            case Texture.FilteringModes.Framebuffer:
                _trace($"Skipping mipmap for {_backHandle}");
                break;
            default:
                /*
                 * If we did not bring in a mipmap, create a default one.
                 */
                if (false == _haveMipmap)
                {
                    _trace($"generate mipmap for {_backHandle}");
                    _gl.GenerateMipmap(TextureTarget.Texture2D);
                }
                else
                {
                    _trace($"Using uploaded mipmap for {_backHandle}");
                }

                CheckError("GenerateMipMap");
                break;
        }
    }


    private void _backToLive()
    {
        _trace("backToLive");
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
        _trace("_allocateBack");
        if (_backGenerated)
        {
            Trace("Already had been generated.");
        }
        _backHandle = _gl.GenTexture();
        _trace($"_allocateBack handle {_backHandle}");
        _backGenerated = true;
        _bindBack();
        _setParameters();
    }


    private void _checkReloadTexture()
    {
        _trace("_checkReload");
        if (_backHandle == 0xffffffff)
        {
            Trace("(First) reload detected. Will allocate back buffer texture.");
            _allocateBack();
        }
    }


    public unsafe void SetFrom(string path, bool isAtlas)
    {
        _trace($"Creating new Texture from path {path}");

        _checkReloadTexture();
        _bindBack();
        try
        {
            System.IO.Stream streamImage = engine.Assets.Open(path);
            var img = Image.Load<Rgba32>(streamImage);
            {
                int width, height;
                _backData = true;

                // TXWTODO: Read this from a property.
                int nLevel = _filteringMode!=Texture.FilteringModes.Framebuffer?NMipmaps:1;
                
                if (!isAtlas)
                {
                    _trace($"texture {path} is no atlas, uploading straight.");
                    
                    width = img.Width;
                    height = img.Height;
                    _haveMipmap = false;
                    _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height,
                        0,
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
                                CheckError($"TexParam w/o mipmap SubImage2D {y}");

                            }
                        }
                    });
                }
                else
                {
                    _trace($"texture {path} has an atlas, uploading...");

                    width = img.Width / 2;
                    height = img.Height;
                    _haveMipmap = true;


                    if (_backHandle == 9)
                    {
                        int a = 1;
                    }
                    
                    /*
                     * Now, first step, allocate the textures.
                     */
                    for (int mm = 0; mm < nLevel; ++mm)
                    {
                        _gl.TexImage2D(TextureTarget.Texture2D, mm, InternalFormat.Rgba8, 
                            (uint)(width>>mm), (uint)(height>>mm),
                            0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
                        CheckError($"TexImage2D {mm}");
                    }
                    

                    img.ProcessPixelRows(accessor =>
                    {
                        // TXWTODO: We need to condfigure the number of mipmaps before
                        // ZXWTODO :We should write the number of mipmaps to the atals.
                        /*
                         * Line by line, fill all of the mipmap images.
                         */
                        for (int y = 0; y < accessor.Height; y++)
                        {
                            fixed (void* data = accessor.GetRowSpan(y))
                            {
                                /*
                                 * Where does the mipmap start?
                                 */
                                int xOffset = 0;

                                for (int mm = 0; mm < nLevel; ++mm)
                                {
                                    int mmHeight = height >> mm;
                                    int mmWidth = width >> mm;

                                    if (y<mmHeight)
                                    {
                                        _gl.TexSubImage2D(TextureTarget.Texture2D, mm, 0, y, (uint)mmWidth, 1,
                                            PixelFormat.Rgba, PixelType.UnsignedByte, ((byte *)data) + 4*xOffset);
                                        if (CheckError($"TexParam with mipmap SubImage2D {y}") > 0)
                                        {
                                            int a = 1;
                                        }
                                    }

                                    xOffset += mmWidth;
                                }
                            }
                        }
                    });
                }
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
        _unbindBack();
        _backToLive();
    }


    public unsafe void SetFrom(
        uint generation, Span<byte> data, uint width, uint height)
    {
        _trace($"Creating new Texture from Span {width}x{height}");
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
        _unbindBack();
        _backToLive();
    }


    public unsafe void SetFrom(uint width, uint height)
    {
        _trace($"Creating new Texture {width}x{height}");
        _checkReloadTexture();
        _bindBack();
        _backData = true;
        _gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba, width, height, 0, PixelFormat.Rgba,
            PixelType.UnsignedByte, null);
        CheckError("TexImage2D black");

        _generateMipmap();
        _unbindBack();
        _backToLive();
    }


    public void ActiveAndBind(TextureUnit textureSlot)
    {
        _trace($"Bind: Active slot {textureSlot}");
        _gl.ActiveTexture(textureSlot);
        CheckError("ActiveTexture {texureSlot}");
        if (!_liveData)
        {
            Error($"Live handle for texture {_liveHandle} does not have data.");
            return;
        }
        _trace($"Bind texture {_liveHandle}");
        _gl.BindTexture(TextureTarget.Texture2D, _liveHandle);
        if (0 == CheckError("BindAndActive Texture"))
        {
            _liveBound = true;
        }
    }


    public void ActiveAndUnbind(TextureUnit textureSlot)
    {
        _trace($"Unbind: Active slot {textureSlot}");
        _gl.ActiveTexture(textureSlot);
        CheckError("ActiveTexture {textureSlot}");
        if (!_liveData)
        {
            Error($"Live handle for texture {_liveHandle} does not have data.");
            return;
        }
        _trace($"Unbind texture 0");
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        if (0 == CheckError("BindAndActive Texture"))
        {
            _liveBound = false;
        }
    }
    
    private void _bindBack()
    {
        _trace("_bindBack");
        _trace($"bind {_backHandle}");
        _gl.BindTexture(TextureTarget.Texture2D, _backHandle);
        int err = CheckError("_bindBack Texture");
        if (err < 0)
        {
            Trace("Break here.");
        }
        else
        {
            _backBound = true;
        }
    }

    private void _unbindBack()
    {
        _trace($"Unbind back {_backHandle}");
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        _backBound = false;
    }


    public void Dispose()
    {
        _trace($"Dispose {_liveHandle} and {_backHandle}");
        _gl.DeleteTexture(_liveHandle);
        _liveHandle = 0xffffffff;
        _liveBound = false;
        CheckError("DeleteTexture live");
        _gl.DeleteTexture(_backHandle);
        _backHandle = 0xffffffff;
        _backBound = false;
        CheckError("DeleteTexture back");
    }


    public unsafe SkTexture(GL gl, engine.joyce.Texture.FilteringModes filteringMode)
    {
        _trace($"new SkTexture filteringMode={filteringMode}");

        _gl = gl;
        _filteringMode = filteringMode;
        _allocateBack();
    }
    
}