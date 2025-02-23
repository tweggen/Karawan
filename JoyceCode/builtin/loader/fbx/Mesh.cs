using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.joyce;
using Java.Util;

namespace builtin.loader.fbx;


public class Mesh : IDisposable
{
    /**
     * Contains five floats per vertex:
     * x/y/z/u/v
     */
    public float[] Vertices { get; private set; }

    private const int FLOATS_PER_VERTEX = 5;
    public uint[] Indices { get; private set; }
    public IReadOnlyList<Texture> Textures { get; private set; }

    
    public void Dispose()
    {
        Textures = null;
    }


    public void AddToMatmesh(MatMesh matMesh)
    {
        int nVertices = Vertices.Length / FLOATS_PER_VERTEX;
        int nIndices = Indices.Length;
        int nTriangles = nIndices / 3;
        if (nTriangles > 0)
        {
            List<Vector3> vertices = new();
            List<Vector2> uvs = new();
            for (int i = 0; i < nVertices; i++)
            {
                vertices.Add(new Vector3(Vertices[i * FLOATS_PER_VERTEX + 0], Vertices[i * FLOATS_PER_VERTEX + 1], Vertices[i * FLOATS_PER_VERTEX + 2]));
                uvs.Add(new Vector2(Vertices[i * FLOATS_PER_VERTEX + 3], Vertices[i * FLOATS_PER_VERTEX + 4]));
            }

            List<uint> indices = new();
            for (int i = 0; i < nTriangles*3; ++i)
            {
                uint idx = Indices[i];
                if (idx >= nVertices) idx = (uint) nVertices - 1;
                indices.Add(idx);
            }
            engine.joyce.Mesh jMesh = new("fromFbx", vertices, indices, uvs);
            matMesh.Add(new() { Texture = I.Get<TextureCatalogue>().FindColorTexture(0xff888888)}, jMesh);
        }
    }


    public Mesh(float[] vertices, uint[] indices, List<Texture> textures)
    {
        Vertices = vertices;
        Indices = indices;
        Textures = textures;
    }
}