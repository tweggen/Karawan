using System;
using System.Numerics;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using engine.geom;
using engine.joyce;


namespace engine.joyce;

public class MergeMeshEntry
{
    public Matrix4x4 Transform;
    public Mesh Mesh;
}



public class Mesh : IComparable<Mesh>
{
    private IdHolder _idHolder = new();
    public int CompareTo(Mesh other) => _idHolder.CompareTo(other._idHolder);    
    
    private AABB _aabb;

    private bool _haveAABB = false;

    public AABB AABB
    {
        get
        {
            if (!_haveAABB)
            {
                _haveAABB = true;
                _computeAABB(out _aabb);
            }

            return _aabb;
        }

        set
        {
            _haveAABB = true;
            _aabb = value;
        }
    }

    public string Name = "unnamed mesh";
    
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

    /**
     * Indexable array of bone indices per Vertex.
     */
    public IList<Int4>? BoneIndices;

    /**
     * Indexable array of bone weights
     */
    public IList<Vector4>? BoneWeights;

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
             * Let's assume clockwise(?????) triangles.
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
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void p(float x, float y, float z)
    {
        p(new Vector3(x, y, z));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UV(in Vector2 uv)
    {
        if (uv.X >= 0f && uv.X <= 1f && uv.Y >= 0f && uv.Y <= 1f)
        {
           
        }
        UVs.Insert(WriteIndexUVs++, uv);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UV(float u, float v)
    {
        UV(new Vector2(u, v));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UVUnsafe(in Vector2 uv)
    {
        UVs.Insert(WriteIndexUVs++, uv);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UVUnsafe(float u, float v)
    {
        UVUnsafe(new Vector2(u, v));
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


    private void _computeAABB(out engine.geom.AABB aabb)
    {
        aabb = new();
        int l = Vertices.Count;
        for (int i = 0; i < l; ++i)
        {
            aabb.Add(Vertices[i]);
        }
    }
    
    
    public void Move(Vector3 off)
    {
        int l = Vertices.Count;
        for (int i = 0; i < l; ++i)
        {
            Vertices[i] += off;
        }

        _haveAABB = false;
    }


    public void Transform(in Matrix4x4 m)
    {
        int l = Vertices.Count;
        for (int i = 0; i < l; ++i)
        {
            Vertices[i] = Vector3.Transform(Vertices[i], m);
        }

        if (_haveAABB)
        {
            _aabb.Transform(m);
        }
    }


    public static Mesh CreateFrom(IList<MergeMeshEntry> others)
    {
        int nTotalVertices = 0;
        int nTotalIndices = 0;
        foreach (var mme in others)
        {
            var otherMesh = mme.Mesh;
            if (otherMesh.Normals == null)
            {
                otherMesh.GenerateCCWNormals();
            }
            nTotalVertices += otherMesh.Vertices.Count;
            Debug.Assert(otherMesh.Vertices.Count == otherMesh.UVs.Count);
            Debug.Assert(otherMesh.Normals == null || otherMesh.Normals.Count == otherMesh.UVs.Count);
            nTotalIndices += otherMesh.Indices.Count;
        }

        var arrVertices = new Vector3[nTotalVertices];
        var arrIndices = new uint[nTotalIndices];
        var arrUVs = new Vector2[nTotalVertices];
        var arrNormals = new Vector3[nTotalVertices];

        int off = 0;
        int offIndices = 0;
        int l, k;
        string lastName = "(nomesh)";

        /*
         * Copy each of the meshes into the arrays.
         */
        foreach (var mme in others)
        {
            var otherMesh = mme.Mesh;
            lastName = otherMesh.Name;
            l = otherMesh.Vertices.Count;
            k = otherMesh.Indices.Count;
            if (mme.Transform.IsIdentity)
            {
                for (int i = 0; i < l; ++i)
                {
                    arrVertices[off + i] = otherMesh.Vertices[i];
                    arrUVs[off + i] = otherMesh.UVs[i];
                    arrNormals[off + i] = otherMesh.Normals[i];
                }
            }
            else
            {
                for (int i = 0; i < l; ++i)
                {
                    arrVertices[off + i] = Vector3.Transform(otherMesh.Vertices[i], mme.Transform);
                    arrNormals[off + i] = Vector3.TransformNormal(otherMesh.Normals[i], mme.Transform);
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
        return new Mesh(lastName, arrVertices, arrIndices, arrUVs) { Normals = arrNormals };
    }


    /*
     * merge a couple of meshes sharing the same position.
     */
    public static Mesh CreateFrom(IList<Mesh> others)
    {
        List<MergeMeshEntry> mmelist = new();
        foreach (var mesh in others)
        {
            mmelist.Add(new MergeMeshEntry() { Transform = Matrix4x4.Identity, Mesh = mesh});
        }

        return CreateFrom(mmelist);
    }


    public Mesh(string name, IList<Vector3> vertices, IList<uint> indices, IList<Vector2> uvs, IList<Vector3> normals = null)
    {
        Name = name;
        Vertices = vertices;
        Indices = indices;
        UVs = uvs;
        Normals = normals;
        WriteIndexVertices = Vertices.Count;
        WriteIndexIndices = Indices.Count;
        WriteIndexUVs = Vertices.Count;
        WriteIndexNormals = Normals!=null?Normals.Count:0;
    }


    public Mesh(string name)
    {
        Name = name;
        Vertices = new List<Vector3>();
        Indices = new List<uint>();
        UVs = new List<Vector2>();
        Normals = null;
    }

    public static Mesh CreateListInstance(string name)
    {
        return new Mesh(name, new List<Vector3>(), new List<uint>(), new List<Vector2>());
    }
    
    public static Mesh CreateNormalsListInstance(string name)
    {
        return new Mesh(name, new List<Vector3>(), new List<uint>(), new List<Vector2>(), new List<Vector3>());
    }
}

