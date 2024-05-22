using System;
using System.Collections.Generic;

namespace builtin.loader.fbx;


public class Mesh : IDisposable
{
    public float[] Vertices { get; private set; }
    public uint[] Indices { get; private set; }
    public IReadOnlyList<Texture> Textures { get; private set; }

    
    public void Dispose()
    {
        Textures = null;
    }
    
    
    public Mesh(float[] vertices, uint[] indices, List<Texture> textures)
    {
        Vertices = vertices;
        Indices = indices;
        Textures = textures;
    }
}