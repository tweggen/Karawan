using System;
using System.Numerics;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Runtime.CompilerServices;


namespace engine.joyce;

public class MergeMeshEntry
{
    public Matrix4x4 Transform;
    public Mesh Mesh;
}

public class Mesh
{
    public int WriteIndexVertices;
    public int WriteIndexIndices;
    public int WriteIndexUVs;
    public int WriteIndexNormals;

    // TXWTODO: Come up with a supersmart concept only storing the mesh source/factory

    /**
         * Indexable array like of Vector3
         */
    public IList<Vector3> Vertices;

    /**
         * Indexable array like of int
         */
    public IList<uint> Indices;

    /**
         * Indexable array like of Vector2
         */
    public IList<Vector2> UVs;

    /**
         * Indexable array like of Vector3 or null.
         */
    public IList<Vector3> Normals;

    public bool UploadImmediately = false;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEmpty()
    {
        return Indices.Count == 0;
    }

    /**
         * Generate smoothed normals for this mesh.
         */
    public void GenerateCCWNormals()
    {
        if (null != Normals)
        {
            // TXWTODO: Throw
            return;
        }

        var nVertices = Vertices.Count;
        /*
         * These are the normals we create. In the beginning,
         * all of the normals are null.
         */
        Normals = new Vector3[nVertices];

        /*
         * We most likely sum more than one normal.
         * Store the count of normal vectors we added.
         * 
         * TXWTODO: Don't we want to normalize them anyway?
         */
        var normalCount = new int[Vertices.Count];

        var nIndices = Indices.Count;
        /*
         * 
         */
        for (int i = 0; i < nIndices; i += 3)
        {
            /*
             * Let's assume clockwise triangles.
             */
            var v0 = (Vector3)Vertices[(int)Indices[i + 0]];
            var v1 = (Vector3)Vertices[(int)Indices[i + 1]];
            var v2 = (Vector3)Vertices[(int)Indices[i + 2]];

            v2 -= v0;
            v1 -= v0;

            var vn = Vector3.Cross(v1, v2);

            for (int j = 0; j < 3; ++j)
            {
                int idx = (int)Indices[i + j];
                if (null == Normals[idx])
                {
                    Normals[idx] = vn;
                    normalCount[idx] = 1;
                }
                else
                {
                    Normals[idx] = ((Vector3)Normals[idx]) + vn;
                    normalCount[idx]++;
                }
            }
        }

        for (int n = 0; n < nVertices; ++n)
        {
            if (null == Normals[n])
            {
                // Normal is not referenced, ignore it.
            }
            else
            {
                int nNormals = normalCount[n];
                if (nNormals > 0)
                {
                    var vn = (Vector3)Normals[n];
                    Normals[n] = Vector3.Normalize(vn);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint GetNextVertexIndex()
    {
        return (uint)Vertices.Count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void p(in Vector3 p)
    {
        Vertices.Insert(WriteIndexVertices++, p);
        // Vertices[WriteIndexVertices++] = p;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void p(float x, float y, float z)
    {
        p(new Vector3(x, y, z));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UV(in Vector2 uv)
    {
        UVs.Insert(WriteIndexUVs++, uv);
        // UVs[WriteIndexUVs++] = uv;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UV(float u, float v)
    {
        UV(new Vector2(u, v));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void N(in Vector3 p)
    {
        Normals.Insert(WriteIndexNormals++, p);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void N(float x, float y, float z)
    {
        N(new Vector3(x, y, z));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Idx(uint a, uint b, uint c)
    {
        Indices.Insert(WriteIndexIndices++, a);
        Indices.Insert(WriteIndexIndices++, b);
        Indices.Insert(WriteIndexIndices++, c);
    }


    public static Mesh CreateFrom(IList<MergeMeshEntry> others)
    {
        // TXWTODO: We ignore normals right now because we only create them after adding everything.
        int nTotalVertices = 0;
        int nTotalIndices = 0;
        foreach (var mme in others)
        {
            var otherMesh = mme.Mesh;
            nTotalVertices += otherMesh.Vertices.Count;
            nTotalIndices += otherMesh.Indices.Count;
        }

        var arrVertices = new Vector3[nTotalVertices];
        var arrIndices = new uint[nTotalIndices];
        var arrUVs = new Vector2[nTotalVertices];

        int off = 0;
        int offIndices = 0;
        int l, k;

        /*
         * Copy each of the meshes into the arrays.
         */
        foreach (var mme in others)
        {
            var otherMesh = mme.Mesh;
            l = otherMesh.Vertices.Count;
            k = otherMesh.Indices.Count;
            if (mme.Transform.IsIdentity)
            {
                for (int i = 0; i < l; ++i)
                {
                    arrVertices[off + i] = otherMesh.Vertices[i];
                    arrUVs[off + i] = otherMesh.UVs[i];
                }
            }
            else
            {
                for (int i = 0; i < l; ++i)
                {
                    arrVertices[off + i] = Vector3.Transform(otherMesh.Vertices[i], mme.Transform);
                    arrUVs[off + i] = otherMesh.UVs[i];
                }
            }

            for (int i = 0; i < k; ++i)
            {
                arrIndices[offIndices + i] = (uint)off + otherMesh.Indices[i];
            }

            off += l;
            offIndices += k;
        }

        /*
         * Finally create a new mesh consisting of the arrays
         * we have created.
         */
        return new Mesh(arrVertices, arrIndices, arrUVs);
    }


    public Mesh(IList<Vector3> vertices, IList<uint> indices, IList<Vector2> uvs)
    {
        Vertices = vertices;
        Indices = indices;
        UVs = uvs;
        Normals = null;
        WriteIndexVertices = Vertices.Count;
        WriteIndexIndices = Indices.Count;
        WriteIndexUVs = Vertices.Count;
        WriteIndexNormals = 0;
    }


    public static Mesh CreateListInstance()
    {
        return new Mesh(new List<Vector3>(), new List<uint>(), new List<Vector2>());
    }

    public static Mesh CreateArrayListInstance()
    {
        return new Mesh(new List<Vector3>(), new List<uint>(), new List<Vector2>());
    }
}

