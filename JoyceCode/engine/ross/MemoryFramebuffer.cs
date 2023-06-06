
using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using engine.draw;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using static engine.Logger;

namespace engine.ross;

public class MemoryFramebuffer : engine.draw.IFramebuffer
{
    private object _lo = new object();
    
    private FontCollection _fontCollection;
    private FontFamily _ffPrototype;
    private Font _fontPrototype;
    private string _resourcePath;
    
    private uint _generation = 0;

    private Vector2 _ulModified;
    private Vector2 _lrModified;


    private void _resetModified()
    {
        _ulModified = new Vector2(100000, 100000);
        _lrModified = new Vector2(-1, -1);
    }

    private void _applyModified(in Vector2 ul, in Vector2 lr)
    {
        _ulModified = new Vector2(Math.Min(_ulModified.X, ul.X), Math.Min(_ulModified.Y, ul.Y));
        _lrModified = new Vector2(Math.Min(_lrModified.X, ul.X), Math.Min(_lrModified.Y, ul.Y));
    }
    
    private byte _r(uint color)
    {
        return (byte)(color & 0xff);
    } 
    
    private byte _g(uint color)
    {
        return (byte)((color>>8) & 0xff);
    }
    private byte _b(uint color)
    {
        return (byte)((color>>16) & 0xff);
    }

    private byte _a(uint color)
    {
        return (byte)((color>>24) & 0xff);
    }

    private Color _col(uint color)
    {
        return Color.FromRgba(_r(color), _g(color), _b(color), _a(color));
    }

    public uint Generation
    {
        get => _generation; 
    }
    
    private Image<SixLabors.ImageSharp.PixelFormats.Rgba32> _image;
    public uint Width
    {
        get => (uint)_image.Width; 
    }
    public uint Height
    {
        get => (uint)_image.Height; 
    }

    public void BeginModification()
    {
    }

    public void EndModification()
    {
        lock (_lo)
        {
            _generation++;
        }
    }

    private DrawingOptions _doClear = new()
    {
        GraphicsOptions = new()
        {
            // ColorBlendingMode = PixelColorBlendingMode.Multiply,
            AlphaCompositionMode = PixelAlphaCompositionMode.Clear
        }
    };

    private DrawingOptions _doText = new()
    {
        GraphicsOptions = new()
        {
            // ColorBlendingMode = PixelColorBlendingMode.Multiply,
            //AlphaCompositionMode = PixelAlphaCompositionMode.Clear
            Antialias = false
        }
    };

    public void ClearRectangle(Context context, Vector2 ul, Vector2 lr)
    {
        Vector2 size = lr - ul;
        Rectangle rectangle = new((int)ul.X, (int)ul.Y, (int)size.X, (int)size.Y);
        _image.Mutate(x => 
            x.Fill(_doClear, _col(context.FillColor), rectangle )
        );
        lock (_lo)
        {
            _applyModified(ul, lr);
        }
    }
        
    public void FillRectangle(Context context, Vector2 ul, Vector2 lr)
    {
        Vector2 size = lr - ul;
        Rectangle rectangle = new((int)ul.X, (int)ul.Y, (int)size.X, (int)size.Y);
        _image.Mutate(x => 
            x.Fill( _col(context.FillColor), rectangle )
        );
        lock (_lo)
        {
            _applyModified(ul, lr);
        }
    }

    public void DrawText(Context context, Vector2 ul, Vector2 lr, string text)
    {
        _image.Mutate(x=> x.DrawText(
            _doText, text, _fontPrototype, 
            _col(context.TextColor),
            new PointF(ul.X, ul.Y)));    
        lock (_lo)
        {
            _applyModified(ul, lr);
        }
    }

    
    public void GetMemory(out Span<byte> spanBytes)
    {
        Memory<SixLabors.ImageSharp.PixelFormats.Rgba32> memoryRgba;
        bool done = _image.DangerousTryGetSinglePixelMemory(out memoryRgba);
        if (!done)
        {
            ErrorThrow("Unable to read the memory of the framebuffer.", (m) => new InvalidOperationException(m));
        }

        Span<SixLabors.ImageSharp.PixelFormats.Rgba32> spanRgba = memoryRgba.Span;
        spanBytes = MemoryMarshal.Cast<SixLabors.ImageSharp.PixelFormats.Rgba32, byte>(spanRgba);
    }

    public void GetModified(out Vector2 ul, out Vector2 lr)
    {
        ul = _ulModified;
        lr = _lrModified;
    }

    public void SetConsumed()
    {
        lock (_lo)
        {
            _resetModified();
        }
    }


    public MemoryFramebuffer(uint width, uint height)
    {
        _image = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>((int)width, (int)height);
        _resourcePath = engine.GlobalSettings.Get("Engine.ResourcePath");
        _resetModified();

        _fontCollection = new();

        System.IO.Stream streamFont = engine.Assets.Open("Prototype.ttf");

        using (var assetStreamReader = new StreamReader(streamFont))
        {
            using (var ms = new MemoryStream())
            {
                assetStreamReader.BaseStream.CopyTo(ms);

                ms.Position = 0;

                _ffPrototype = _fontCollection.Add(ms);
            }
        }
        _fontPrototype = _ffPrototype.CreateFont(10, FontStyle.Regular);
    }

}