using System;
using System.Numerics;
using System.Runtime.Intrinsics.X86;
using engine.draw;
using SkiaSharp;

namespace engine.ross;

public class SkiaSharpFramebuffer : IFramebuffer
{
    private object _lo = new();
    private readonly string _id;
    private readonly uint _width;
    private readonly uint _height;
    private uint _generation = 0xfffffffe;

    private Vector2 _ulModified = new();
    private Vector2 _lrModufied = new();

    private SKSurface _skiaSurface;
    
    public string Id { get => _id; }
    public uint Width { get => _width; }
    public uint Height { get => _height; }
    public uint Generation { get => _generation; }

    private byte[] _superfluousBackBuffer;
    
    
    public void GetModified(out Vector2 ul, out Vector2 lr)
    {
        lock (_lo)
        {
            ul = _ulModified;
            lr = _lrModufied;
        }
    }
        

    public void SetConsumed()
    {
        lock (_lo)
        {
            _resetModified();
        }
    }
    

    public void BeginModification()
    {
        
    }

    
    public void EndModification()
    {
        
    }


    public void FillRectangle(Context context, Vector2 ul, Vector2 lr)
    {
        var paint = new SKPaint
        {
            Color = context.FillColor,
            IsAntialias = false,
            Style = SKPaintStyle.Fill
        };
        lock (_lo)
        {
            _skiaSurface.Canvas.DrawRect(ul.X, ul.Y, lr.X, lr.Y, paint);
        }
    }


    public void ClearRectangle(Context context, Vector2 ul, Vector2 lr)
    {
        var paint = new SKPaint
        {
            Color = context.ClearColor,
            IsAntialias = false,
            Style = SKPaintStyle.Fill
        };
        lock (_lo)
        {
            _skiaSurface.Canvas.DrawRect(ul.X, ul.Y, lr.X, lr.Y, paint);
        }
    }


    private SKTextAlign _toSkiaTextAlign(engine.draw.HAlign hAlign)
    {
        switch (hAlign)
        {
            default:
            case HAlign.Left:
                return SKTextAlign.Left;
            case HAlign.Center:
                return SKTextAlign.Center;
            case HAlign.Right:
                return SKTextAlign.Right;
        }
    }

    public void DrawText(Context context, Vector2 ul, Vector2 lr, string text, int fontSize)
    {
        var paint = new SKPaint
        {
            Color = context.TextColor,
            IsAntialias = false,
            Style = SKPaintStyle.Fill,
            TextAlign = _toSkiaTextAlign(context.HAlign),
            TextSize = fontSize
        };
        var coord = new SKPoint(ul.X, ul.Y);
        lock (_lo)
        {
            _skiaSurface.Canvas.DrawText(text, coord, paint);
        }
    }
    

    public unsafe void GetMemory(out Span<byte> spanBytes)
    {
        SKImageInfo info = new((int)_width, (int)_height, SKColorType.Rgba8888);
        SKPixmap skiaPixmap = _skiaSurface.PeekPixels();
        lock (_lo)
        {
            fixed (byte* p = _superfluousBackBuffer)
            {
                IntPtr intptr = (IntPtr)p;

                skiaPixmap.ReadPixels(info, intptr,  (int)_width * 4);
            }

        }

        spanBytes = _superfluousBackBuffer.AsSpan();

    }

    
    private void _resetModified()
    {
        lock (_lo)
        {
            _ulModified.X = 0;
            _ulModified.Y = 0;
            _lrModufied.X = _width - 1;
            _lrModufied.Y = _height - 1;
        }
    }
    

    public SkiaSharpFramebuffer(string id, uint width, uint height)
    {
        _id = id;
        _width = width;
        _height = height;

        _superfluousBackBuffer = new byte[_width * _height * 4];

        var info = new SKImageInfo((int)width, (int)height, SKColorType.Rgba8888);
        _skiaSurface = SKSurface.Create(info);
        _skiaSurface.Canvas.Clear();
    }
}
