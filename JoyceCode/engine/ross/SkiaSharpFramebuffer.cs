using System;
using System.IO;
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

    private SKTypeface _skiaTypefacePrototype;
    
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
            Color = context.ClearColor, 
            Style = SKPaintStyle.Fill,
            BlendMode = SKBlendMode.Src
        };
        lock (_lo)
        {
            // Trace($"ul is {ul} lr is {lr}");
            _skiaSurface.Canvas.DrawRect(ul.X, ul.Y, lr.X-ul.X+1, lr.Y-ul.Y+1, paint);
            _applyModified(ul, lr);
        }
    }

    public void DrawPoly(Context context, in Vector2[] polyPoints)
    {
        int l = polyPoints.Length;
        if (0 == l) return;
        var paint = new SKPaint
        {
            Color = context.FillColor, 
            Style = SKPaintStyle.Stroke,
            //BlendMode = SKBlendMode.Src
        };
        SKPoint[] skPoints = new SKPoint[l];
        for (int i = 0; i < l; ++i)
        {
            skPoints[i] = new SKPoint(polyPoints[i].X, polyPoints[i].Y);
        }

        SKPath skiaPath = new();
        skiaPath.AddPoly(skPoints,true);
        
        lock (_lo)
        {
            _skiaSurface.Canvas.DrawPath(skiaPath, paint);
        }
    }


    public void FillPoly(Context context, in Vector2[] polyPoints)
    {
        int l = polyPoints.Length;
        if (0 == l) return;
        var paint = new SKPaint
        {
            Color = context.FillColor, 
            Style = SKPaintStyle.StrokeAndFill,
            //BlendMode = SKBlendMode.Src
        };
        SKPoint[] skPoints = new SKPoint[l];
        for (int i = 0; i < l; ++i)
        {
            skPoints[i] = new SKPoint(polyPoints[i].X, polyPoints[i].Y);
        }

        SKPath skiaPath = new();
        skiaPath.AddPoly(skPoints,true);
        
        lock (_lo)
        {
            _skiaSurface.Canvas.DrawPath(skiaPath, paint);
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
        SKFont font = new SKFont(_skiaTypefacePrototype, fontSize);
        var metrics = font.Metrics;
        
        var paint = new SKPaint(font)
        {
            Color = context.TextColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            TextAlign = _toSkiaTextAlign(context.HAlign),
            TextSize = fontSize
        };
        float x;
        switch (context.HAlign)
        {
            default:
            case HAlign.Left:
                x = ul.X;
                break;
            case HAlign.Center:
                x = (lr.X - ul.X) / 2f;
                break;
            case HAlign.Right:
                x = lr.X;
                break;
        }

        if (fontSize > 20)
        {
            string foo;
            foo = "2";
        }
        // float y = ul.Y + fontSize; //lr.Y - ul.Y
        float y = ul.Y - metrics.Ascent; //lr.Y - ul.Y
        string remainingText = text;
        lock (_lo)
        {
            while (remainingText.Length > 0)
            {
                string renderText = "";
                /*
                 * Render text until exluding the next \n .
                 */
                int i = remainingText.IndexOf('\n');
                if (i >= 0)
                {
                    if (i > 0)
                    {
                        renderText = remainingText.Substring(0, i);
                    }
                    else
                    {
                        // Leave rendertext empty.
                    }

                    remainingText = remainingText.Substring(i + 1);
                } else 
                {
                    renderText = remainingText;
                    remainingText = "";
                }

                if (renderText.Length > 0)
                {
                    _skiaSurface.Canvas.DrawText(renderText, x, y, paint);
                }

                //y += fontSize;
                y += metrics.Leading + metrics.Descent - metrics.Ascent;
            }
            // TXWTODO: We do not need y+the entire fontSize, just the under lengths.
            //_applyModified(ul, lr with { Y = y + fontSize});
            _applyModified(ul, lr with { Y = y /* metrics.Descent-metrics.Ascent */ });
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
        
        System.IO.Stream streamFont = engine.Assets.Open("Prototype.ttf");
        _skiaTypefacePrototype = SKTypeface.FromStream(streamFont);
#if false
        /*
         * We need to put a memorystream on top because android does not implement seeking natively.
         */
        using (var assetStreamReader = new StreamReader(streamFont))
        {
            using (var ms = new MemoryStream())
            {
                assetStreamReader.BaseStream.CopyTo(ms);
                ms.Position = 0;

                _skiaTypefacePrototype = SKTypeface.FromStream(ms);
            }
        }
#endif
    }
}
