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
        throw new NotImplementedException();
    }

    public void ClearRectangle(Context context, Vector2 ul, Vector2 lr)
    {
        throw new NotImplementedException();
    }

    public void DrawText(Context context, Vector2 ul, Vector2 lr, string text, int fontSize)
    {
        throw new NotImplementedException();
    }

    public void GetMemory(out Span<byte> spanBytes)
    {
        throw new NotImplementedException();
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

        var info = new SKImageInfo((int)width, (int)height, SKColorType.Rgba8888);
        _skiaSurface = SKSurface.Create(info);
    }
}
