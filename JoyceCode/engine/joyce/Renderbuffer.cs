
using System;
using static engine.Logger; 

namespace engine.joyce;

/**
 * A framebuffer represents a physical rendering target on the graphics card to
 * render into.
 */
public class Renderbuffer
{
    public string Name;
    public uint Width;
    public uint Height;
    
    /**
     * This counter can be used by the renderer to keep state.
     */
    public uint LastFrame;
    
    public string TextureName
    {
        get => $"framebuffer://{Name}";
    }

    public Renderbuffer(string name, uint width, uint height)
    {
        if (0 == width || 0 == height)
        {
            ErrorThrow("width and height cannot be null.", (m)=>new ArgumentException(m));
        }

        Name = name;
        Width = width;
        Height = height;
    }
}