using System;
using System.ComponentModel.Design;
using System.Numerics;
using engine;
using engine.draw;
using glTFLoader.Schema;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
//using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SkiaSharp;
using static engine.Logger;
using Image = SixLabors.ImageSharp.Image;
using Texture = engine.joyce.Texture;
using Trace = System.Diagnostics.Trace;

namespace Splash.Silk;

public class SkTexture : IDisposable
{
    private object _lo = new();
    
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

    private engine.joyce.Texture _jTexture;
    private engine.joyce.Texture.FilteringModes _filteringMode = engine.joyce.Texture.FilteringModes.Pixels;

    private bool _traceTexture = false;
    
    /*
     * As we currently ignore the loading the textures, we immediately mark them as using.
     */
    private ATextureEntry.ResourceState _resourceState = ATextureEntry.ResourceState.Created;
    
    /*
     * Data generation that had been uploaded.
     */
    private uint _generation = 0xfffffffe;

    public uint Generation
    {
        get => _generation;
    }
    
    #if false
    #error
    /*
     * We create two default textures which are bound to a texture slot while
     * the real image still is loaded, one transparent and one intransparent one.
     * They are used until the image completely has loaded.
     */
    #endif

    public ATextureEntry.ResourceState ResourceState
    {
        get {
            lock (_lo)
            {
                /*
                 * We are uploaded. Check, if we also are outdated.
                 */
                if (_resourceState >= ATextureEntry.ResourceState.Using)
                {
                    IFramebuffer framebuffer = _jTexture.Framebuffer;
                    if (framebuffer != null)
                    {
                        if (framebuffer.Generation != _generation)
                        {
                            return ATextureEntry.ResourceState.Outdated;
                        }
                    }
                }
                
                return _resourceState;
            }
        }
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

    
    private bool _checkGLErrors = false;

    public bool CheckGLErrors
    {
        get => _checkGLErrors;
        set { _checkGLErrors = value; }
    }
    
    private int _checkError(string what)
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
        if (_checkGLErrors) _checkError("TexParam WrapS");
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,
            (int)GLEnum.ClampToEdge);
        if (_checkGLErrors) _checkError("TexParam WrapT");

        switch (_filteringMode)
        {
            case engine.joyce.Texture.FilteringModes.Framebuffer:
                _trace("_setParameters Framebuffer");
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, 
                    (int)GLEnum.Nearest);
                if (_checkGLErrors) _checkError("TexParam MinFilter");
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                    (int)GLEnum.Nearest);
                if (_checkGLErrors) _checkError("TexParam MagFilter");
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 
                    0);
                if (_checkGLErrors) _checkError("TexParam BaseLevel");
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 
                    0);
                if (_checkGLErrors) _checkError("TexParam MaxLevel");
                break;
            case engine.joyce.Texture.FilteringModes.Pixels:
                _trace("_setParameters Pixels");
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, 
                    (int)GLEnum.NearestMipmapNearest);
                if (_checkGLErrors) _checkError("TexParam MinFilter");
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                    (int)GLEnum.Nearest);
                if (_checkGLErrors) _checkError("TexParam MagFilter");
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 
                    0);
                if (_checkGLErrors) _checkError("TexParam BaseLevel");
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 
                    NMipmaps-1);
                if (_checkGLErrors) _checkError("TexParam MaxLevel");
                break;
            case engine.joyce.Texture.FilteringModes.Smooth:
                _trace("_setParameters Smooth");
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, 
                    (int)GLEnum.LinearMipmapLinear);
                if (_checkGLErrors) _checkError("TexParam MinFilter");
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                    (int)GLEnum.Linear);
                if (_checkGLErrors) _checkError("TexParam MagFilter");
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 
                    0);
                if (_checkGLErrors) _checkError("TexParam BaseLevel");
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 
                    NMipmaps-1);
                if (_checkGLErrors) _checkError("TexParam MaxLevel");
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

                if (_checkGLErrors) _checkError("GenerateMipMap");
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


    private unsafe void _processPixelChunks(
        Image<Rgba32> img,
        Action<IntPtr, int, int, uint, uint, long> action
    )
    {
#if true
        img.ProcessPixelRows(accessor =>
        {
            void* pFirstRow = null;
            void* pPreviousRow = null;
            void* pCurrentRow = null;
            long previousStride = 0l;
            long currentStride = 0l;
            uint nRows = 0;
            int y0 = 0;

            uint width = (uint)accessor.Width;
            uint height = (uint)accessor.Height;

            if (0 == height || 0 == width)
            {
                return;
            }

            for (int y = 0; y < height; y++)
            {
                fixed (void* p = accessor.GetRowSpan(y))
                {
                    pCurrentRow = p;
                    if (pFirstRow == null)
                    {
                        pFirstRow = p;
                    }

                    if (pPreviousRow != null)
                    {
                        currentStride = (byte*)pCurrentRow - (byte*)pPreviousRow;
                        if (previousStride != 0l)
                        {
                            if (currentStride != previousStride)
                            {
                                /*
                                 * This is a third (or later) line in a chunk. It does have a different stride
                                 * than the fist to the second.
                                 * 
                                 * Different size of the line, take what we have up to this point.
                                 * That is call the action from the first up until the previous line
                                 * with the previousStride set up.
                                 */

                                /*
                                 * emit nRows from y0, but not this.
                                 */
                                {
                                    action(new IntPtr(pFirstRow), 0, y0, width, nRows, previousStride);
                                }
                                
                                /*
                                 * Start a new chunk with this row.
                                 */
                                pFirstRow = pCurrentRow;
                                currentStride = 0l;                                
                                y0 = y;
                                nRows = 0; // which will increment to one very soon.
                            }
                            else
                            {
                                /*
                                 * This is a third (or later) line in a chunk. It did not have a different stride
                                 * than the fist to the second.
                                 * Continue to accumulate lines.
                                 */
                            }
                        }
                    }
                    else
                    {
                        /*
                         * This is the first row in a chunk. Only after the second we can push out a segment.
                         */
                    }
                }

                pPreviousRow = pCurrentRow;
                previousStride = currentStride;
                ++nRows;
            }
            
            /*
             * If height has not been zero, nRows is > 0 here.
             * Note that previousStride very well may be 0, precisely if nRows==1
             */
            action(new IntPtr(pFirstRow), 0, y0, width, nRows, previousStride);
        });
#else
        img.ProcessPixelRows(accessor =>
        {
            uint width = (uint)accessor.Width;
            uint height = (uint)accessor.Height;

            for (int y = 0; y < height; y++)
            {
                action(new IntPtr(p), 0, y, width, height, width * sizeof(Rgba32));
            }
        });
#endif
    }



    private unsafe void _uploadImage(Image<Rgba32> img, string path, bool isAtlas)
    {
        Trace($"Uploading {path}");
        int width, height;
        _backData = true;

        // TXWTODO: Read this from a property.
        int nLevel = _filteringMode != Texture.FilteringModes.Framebuffer ? NMipmaps : 1;

        if (!isAtlas)
        {
            _trace($"texture {path} is no atlas, uploading straight.");

            width = img.Width;
            height = img.Height;
            _haveMipmap = false;
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height,
                0,
                PixelFormat.Rgba, PixelType.UnsignedByte, null);
            if (_checkGLErrors) _checkError("TexImage2D");

#if true
            _processPixelChunks(img, (p, _jTexture, y, w, h, stride) =>
            {
                _trace($"y = {y}, nRows = {h}");
                _gl.PixelStore(PixelStoreParameter.PackRowLength, (int)(stride / 4));
                _gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, y, (uint)w, h,
                    PixelFormat.Rgba, PixelType.UnsignedByte, p.ToPointer());
                if (_checkGLErrors) _checkError($"TexParam w/o mipmap SubImage2D {y}");
            });
#else
                    img.ProcessPixelRows(accessor =>
                    {
                        for (int y = 0; y < accessor.Height; y++)
                        {
                            fixed (void* data = accessor.GetRowSpan(y))
                            {
                                _gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, y, (uint)accessor.Width, 1,
                                    PixelFormat.Rgba, PixelType.UnsignedByte, data);
                                if (_checkGLErrors) _checkError($"TexParam w/o mipmap SubImage2D {y}");

                            }
                        }
                    });
#endif
        }
        else
        {
            _trace($"texture {path} has an atlas, uploading...");

            width = img.Width / 2;
            height = img.Height;
            _haveMipmap = true;

            /*
             * Now, first step, allocate the textures.
             */
            for (int mm = 0; mm < nLevel; ++mm)
            {
                /*
                 *_gl.PixelStore(PixelStoreParameter.PackRowLength, (int)(stride / 4));
                 * If optimizing the texture atlas upload, use the row length parameter to upload more
                 * * of the individual texture chunks at once.
                 */
                _gl.TexImage2D(TextureTarget.Texture2D, mm, InternalFormat.Rgba8,
                    (uint)(width >> mm), (uint)(height >> mm),
                    0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
                if (_checkGLErrors) _checkError($"TexImage2D {mm}");
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

                            if (y < mmHeight)
                            {
                                _gl.TexSubImage2D(TextureTarget.Texture2D, mm, 0, y, (uint)mmWidth, 1,
                                    PixelFormat.Rgba, PixelType.UnsignedByte, ((byte*)data) + 4 * xOffset);
                                if (_checkGLErrors) _checkError($"TexParam with mipmap SubImage2D {y}");
                            }

                            xOffset += mmWidth;
                        }
                    }
                }
            });
        }
    }


    public unsafe void SetFrom(string path, bool isAtlas)
    {
        _trace($"Creating new Texture from path {path}");

        try
        {
            lock (_lo)
            {
                _resourceState = ATextureEntry.ResourceState.Loading;
            }
            #if false
            System.IO.Stream streamImage = engine.Assets.Open(path);
            var img = Image.Load<Rgba32>(streamImage);
            lock (_lo)
            {
                _resourceState = ATextureEntry.ResourceState.Uploading;
            }
            _uploadImage(img, path, isAtlas);
            #else
            I.Get<Engine>().Run( ()=>
            {
                Trace($"Loading {path}");
                System.IO.Stream streamImage = engine.Assets.Open(path);
                var img = Image.Load<Rgba32>(streamImage);
                I.Get<IThreeD>().Execute(() =>
                {
                    _checkReloadTexture();
                    _bindBack();
                    lock (_lo)
                    {
                        _resourceState = ATextureEntry.ResourceState.Uploading;
                    }
                    _uploadImage(img, path, isAtlas);
                    _generateMipmap();
                    _unbindBack();
                    _backToLive();
                    lock (_lo)
                    {
                        _resourceState = ATextureEntry.ResourceState.Using;
                    }
                });

            });
            #endif
        }
        catch (Exception e)
        {
            _checkReloadTexture();
            _bindBack();
            lock (_lo)
            {
                _resourceState = ATextureEntry.ResourceState.Uploading;
            }
            /*
             * As a fallback, generate a single-colored texture.
             */
            byte[] arrData = new byte[4] { 128, 128, 0, 255 };
            Span<byte> spanData = arrData.AsSpan();
            fixed (void* d = &spanData[0])
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba, 1, 1, 0, PixelFormat.Rgba,
                    PixelType.UnsignedByte, d);
                if (_checkGLErrors) _checkError("TexImage2D");
            }
            _generateMipmap();
            _unbindBack();
            _backToLive();
            lock (_lo)
            {
                _resourceState = ATextureEntry.ResourceState.Using;
            }
        }

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
            if (_checkGLErrors) _checkError("TexImage2D");
        }

        _generateMipmap();
        _unbindBack();
        _backToLive();
        lock (_lo)
        {
            _resourceState = ATextureEntry.ResourceState.Using;
        }
    }


    public unsafe void SetFrom(uint width, uint height)
    {
        _trace($"Creating new Texture {width}x{height}");
        _checkReloadTexture();
        _bindBack();
        _backData = true;
        _gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba, width, height, 0, PixelFormat.Rgba,
            PixelType.UnsignedByte, null);
        if (_checkGLErrors) _checkError("TexImage2D black");

        _generateMipmap();
        _unbindBack();
        _backToLive();
        lock (_lo)
        {
            _resourceState = ATextureEntry.ResourceState.Using;
        }
    }


    public void ActiveAndBind(TextureUnit textureSlot)
    {
        _trace($"Bind: Active slot {textureSlot}");
        _gl.ActiveTexture(textureSlot);
        if (_checkGLErrors) _checkError($"ActiveAndBind ActiveTexture {textureSlot}");
        if (!_liveData)
        {
            if (0xffffffff != _liveHandle)
            {
                Error($"Live handle for texture {_liveHandle} does not have data.");
            }

            return;
        }
        _trace($"Bind texture {_liveHandle}");
        if (_checkGLErrors) _checkError("flush");
        _gl.BindTexture(TextureTarget.Texture2D, _liveHandle);

        int err = _checkGLErrors ? _checkError("ActiveAndBind Bind Texture") : 0;
        if (0 == err)
        {
            _liveBound = true; 
        }
    }


    public void ActiveAndUnbind(TextureUnit textureSlot)
    {
        _trace($"Unbind: Active slot {textureSlot}");
        _gl.ActiveTexture(textureSlot);
        if (_checkGLErrors) _checkError($"ActiveAndUnbind ActiveTexture {textureSlot}");
        if (!_liveData)
        {
            if (0xffffffff != _liveHandle)
            {
                Error($"Live handle for texture {_liveHandle} does not have data.");
            }

            return;
        }
        _trace($"Unbind texture 0");
        if (_checkGLErrors) _checkError("flush");
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        int err = _checkGLErrors ? _checkError("ActiveAndUnbind Bind Texture"):0;
        if (err == 0)
        {
            _liveBound = false;
        }
    }
    
    private void _bindBack()
    {
        _trace("_bindBack");
        _trace($"bind {_backHandle}");
        if (_checkGLErrors) _checkError("flush");
        // TXWTODO: This is a bit ugly. But don't mess up the main texture channel by our background operations.
        _gl.ActiveTexture(TextureUnit.Texture3);
        _gl.BindTexture(TextureTarget.Texture2D, _backHandle);
        int err = _checkGLErrors?_checkError("_bindBack Texture"):0;
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
        // TXWTODO: This is a bit ugly. But don't mess up the main texture channel by our background operations.
        _gl.ActiveTexture(TextureUnit.Texture3);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        _backBound = false;
    }


    public void Dispose()
    {
        _trace($"Dispose {_liveHandle} and {_backHandle}");
        _gl.DeleteTexture(_liveHandle);
        _liveHandle = 0xffffffff;
        _liveBound = false;
        if (_checkGLErrors) _checkError("DeleteTexture live");
        _gl.DeleteTexture(_backHandle);
        _backHandle = 0xffffffff;
        _backBound = false;
        if (_checkGLErrors) _checkError("DeleteTexture back");
    }


    public unsafe SkTexture(GL gl, engine.joyce.Texture jTexture, engine.joyce.Texture.FilteringModes filteringMode)
    {
        _trace($"new SkTexture filteringMode={filteringMode}");

        _gl = gl;
        _filteringMode = filteringMode;
        _jTexture = jTexture;
        _allocateBack();
    }
    
}