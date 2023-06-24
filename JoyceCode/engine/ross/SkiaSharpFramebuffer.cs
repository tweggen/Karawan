using System;
using System.Numerics;
using engine.draw;
using SkiaSharp;
using static engine.Logger;
using Trace = System.Diagnostics.Trace;

namespace engine.ross;

public class SkiaSharpFramebuffer : IFramebuffer
{
    private object _lo = new();
    private readonly string _id;
    private readonly uint _width;
    private readonly uint _height;
    private uint _generation = 0xfffffffe;

    private Vector2 _ulModified = new();
    private Vector2 _lrModified = new();

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
            lr = _lrModified;
        }
    }
        

    public void SetConsumed()
    {
        lock (_lo)
        {
            _resetModified();
        }
    }
    

    private void _applyModified(in Vector2 ul, in Vector2 lr)
    {
        _ulModified = new Vector2(Math.Min(_ulModified.X, ul.X), Math.Min(_ulModified.Y, ul.Y));
        _lrModified = new Vector2(Math.Max(_lrModified.X, lr.X), Math.Max(_lrModified.Y, lr.Y));
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
            _skiaSurface.Canvas.DrawRect(ul.X, ul.Y, lr.X-ul.X+1, lr.Y-ul.Y+1, paint);
            _applyModified(ul, lr);
        }
    }


    public void ClearRectangle(Context context, Vector2 ul, Vector2 lr)
    {
        var paint = new SKPaint
        {
            Color = 0xffff0000, // context.ClearColor,
            Style = SKPaintStyle.Fill,
            //BlendMode = SKBlendMode.Src
        };
        lock (_lo)
        {
            Trace($"ul is {ul} lr is {lr}");
            _skiaSurface.Canvas.DrawRect(ul.X, ul.Y, lr.X-ul.X+1, lr.Y-ul.Y+1, paint);
            _applyModified(ul, lr);
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
            _applyModified(ul, lr);
        }
    }
    

    public unsafe void GetMemory(out Span<byte> spanBytes)
    {
        try
        {
            SKImageInfo info = new((int)_width, (int)_height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            SKPixmap skiaPixmap = _skiaSurface.PeekPixels();
            lock (_lo)
            {
                fixed (byte* p = _superfluousBackBuffer)
                {
                    IntPtr intptr = (IntPtr)p;

                    skiaPixmap.ReadPixels(info, intptr, (int)_width * 4);
                }

            }

            spanBytes = _superfluousBackBuffer.AsSpan();
        }
        catch (Exception e)
        {
            Error($"Exception {e}");
            spanBytes = _superfluousBackBuffer.AsSpan();
        }

    }

    
    private void _resetModified()
    {
        lock (_lo)
        {
            _ulModified.X = _width - 1;
            _ulModified.Y = _height - 1;
            _lrModified.X = 0;
            _lrModified.Y = 0;
        }
    }
    

    public SkiaSharpFramebuffer(string id, uint width, uint height)
    {
        _id = id;
        _width = width;
        _height = height;

        _superfluousBackBuffer = new byte[_width * _height * 4];

        var info = new SKImageInfo((int)width, (int)height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        _skiaSurface = SKSurface.Create(info);
        //_skiaSurface.Canvas.Clear();
        _applyModified(new Vector2(0,0), new Vector2(_width-1, _height-1));
    }
}
