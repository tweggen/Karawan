
using System;
using static engine.Logger; 

namespace engine.joyce;

public class Framebuffer
{
    public string Name;
    public uint Width;
    public uint Height;

    public Framebuffer(string name, uint width, uint height)
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