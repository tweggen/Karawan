using System;
using Silk.NET.Assimp;

namespace builtin.loader;

public class Texture : IDisposable
{
    public string Path { get; set; }
    public TextureType Type { get; }

    public Texture(string path, TextureType type = TextureType.None)
    {
        Path = path;
        Type = type;
    }

    public Texture(Span<byte> data, uint width, uint height)
    {
    }
}
    