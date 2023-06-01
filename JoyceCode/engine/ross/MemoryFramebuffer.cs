
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using engine.draw;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using static engine.Logger;

namespace engine.ross;

public class MemoryFramebuffer : engine.draw.IFramebuffer
{
    private object _lo = new object();
    
    private uint _generation = 0;

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
        
    public void FillRectangle(in Context context, in Vector2 ul, in Vector2 lr)
    {
        Vector2 size = lr - ul;
        Rectangle rectangle = new((int)ul.X, (int)ul.Y, (int)size.X, (int)size.Y);
        _image.Mutate(x => x.Fill(Color.White, rectangle));
        lock (_lo)
        {
            ++_generation;
        }
    }

    public void DrawText(in Context context, in Vector2 ul, in Vector2 lr, in string text)
    {
        throw new System.NotImplementedException();
    }

    
    public void MarkDirty()
    {
        lock (_lo)
        {
            ++_generation;
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


    public MemoryFramebuffer(uint width, uint height)
    {
        _image = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>((int)width, (int)height);
    }

}