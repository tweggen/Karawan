using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using engine;
using engine.joyce;
using glTFLoader;
using glTFLoader.Schema;
using static engine.Logger;

namespace builtin.loader;

public class GlTF
{
    private engine.Engine _engine;
    private Gltf _model;
    private byte[] _binary;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void _readVector2(int ofs, out Vector2 v2)
    {
        v2.X = BitConverter.ToSingle(_binary, ofs);
        v2.Y = BitConverter.ToSingle(_binary, ofs + sizeof(float));
    }
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void _readVector3(int ofs, out Vector3 v3)
    {
        v3.X = BitConverter.ToSingle(_binary, ofs);
        v3.Y = BitConverter.ToSingle(_binary, ofs + sizeof(float));
        v3.Z = BitConverter.ToSingle(_binary, ofs + 2*sizeof(float));
    }

    private void _readVector3Array(int ofs, int length, ref IList<Vector3> arr)
    {
        for (int j = 0; j < length; ++j)
        {
            _readVector3(ofs+j*3*sizeof(float), out var v3);
            arr.Add(v3);
        }
    }

    
    private void _readVector2Array(int ofs, int length, ref IList<Vector2> arr)
    {
        for (int j = 0; j < length; ++j)
        {
            _readVector2(ofs+j*2*sizeof(float), out var v2);
            arr.Add(v2);
        }
    }


    private void _readTri32Array(int ofs, int length, ref IList<uint> arr)
    {
        int l = (length / 3) * 3;
        for (int j = 0; j < l; ++j)
        {
            arr.Add((uint)BitConverter.ToInt32(_binary, ofs + j * sizeof(int)));
        }        
    }


    private void _readVertices(Accessor acc, engine.joyce.Mesh jMesh)
    {
        BufferView bvwVertices = _model.BufferViews[acc.BufferView.Value];

        if (acc.Type == Accessor.TypeEnum.VEC3)
        {
            if (acc.ComponentType == Accessor.ComponentTypeEnum.FLOAT)
            {
                _readVector3Array(bvwVertices.ByteOffset,acc.Count, ref jMesh.Vertices);
            }
            else
            {
                ErrorThrow($"Unsupported component type {acc.ComponentType}.", m => new InvalidDataException(m));
            }
        }
        else
        {
            ErrorThrow($"Unsupported type {acc.Type}.", m => new InvalidDataException(m));
        }
    }


    private void _readNormals(Accessor acc, engine.joyce.Mesh jMesh)
    {
        BufferView bvwVertices = _model.BufferViews[acc.BufferView.Value];

        if (acc.Type == Accessor.TypeEnum.VEC3)
        {
            if (acc.ComponentType == Accessor.ComponentTypeEnum.FLOAT)
            {
                _readVector3Array(bvwVertices.ByteOffset,acc.Count, ref jMesh.Normals);
            }
            else
            {
                ErrorThrow($"Unsupported component type {acc.ComponentType}.", m => new InvalidDataException(m));
            }
        }
        else
        {
            ErrorThrow($"Unsupported type {acc.Type}.", m => new InvalidDataException(m));
        }
    }


    private void _readTexcoords0(Accessor acc, engine.joyce.Mesh jMesh)
    {
        BufferView bvwVertices = _model.BufferViews[acc.BufferView.Value];

        if (acc.Type == Accessor.TypeEnum.VEC2)
        {
            if (acc.ComponentType == Accessor.ComponentTypeEnum.FLOAT)
            {
                _readVector2Array(bvwVertices.ByteOffset,acc.Count, ref jMesh.UVs);
            }
            else
            {
                ErrorThrow($"Unsupported component type {acc.ComponentType}.", m => new InvalidDataException(m));
            }
        }
        else
        {
            ErrorThrow($"Unsupported type {acc.Type}.", m => new InvalidDataException(m));
        }
    }
    
    
    private void _readTriangles(Accessor acc, engine.joyce.Mesh jMesh)
    {
        BufferView bvwVertices = _model.BufferViews[acc.BufferView.Value];

        if (acc.Type == Accessor.TypeEnum.SCALAR)
        {
            if (acc.ComponentType == Accessor.ComponentTypeEnum.UNSIGNED_SHORT)
            {
                _readTri32Array(bvwVertices.ByteOffset,acc.Count, ref jMesh.Indices);
            }
            else
            {
                ErrorThrow($"Unsupported component type {acc.ComponentType}.", m => new InvalidDataException(m));
            }
        }
        else
        {
            ErrorThrow($"Unsupported type {acc.Type}.", m => new InvalidDataException(m));
        }
    }


    private void _readMesh(glTFLoader.Schema.Mesh fbxMesh, out engine.joyce.Mesh jMesh)
    { 
        jMesh = new("gltf", new List<Vector3>(), new List<uint>(), new List<Vector2>());
        jMesh.Normals = new List<Vector3>();

        foreach (var fbxMeshPrimitive in fbxMesh.Primitives)
        {
            int idxPosition = -1;
            int idxNormal = -1;
            int idxTexcoord0 = -1;

            /*
             * Collect the attributes
             */
            foreach (var fbxAttr in fbxMeshPrimitive.Attributes)
            {
                switch (fbxAttr.Key)
                {
                    case "POSITION":
                        idxPosition = fbxAttr.Value;
                        break;
                    case "NORMAL":
                        idxNormal = fbxAttr.Value;
                        break;
                    case "TEXCOORD_0":
                        idxTexcoord0 = fbxAttr.Value;
                        break;
                    default:
                        break;
                }
            }

            if (idxPosition == -1)
            {
                /*
                 * A mesh without vertex positions is pointless.
                 */
                continue;
            }

            if (null == fbxMeshPrimitive.Indices)
            {
                /*
                 * Also, without an index we do not need to access this mesh.
                 */
                continue;
            }

            /*
             * Now let's iterate through the vertex positions, adding normals and
             * texcoords, if we have any.
             */
            _readVertices(_model.Accessors[idxPosition], jMesh);
            if (idxNormal != -1)
            {
                _readNormals(_model.Accessors[idxNormal], jMesh);
            }

            if (idxTexcoord0 != -1)
            {
                _readTexcoords0(_model.Accessors[idxTexcoord0], jMesh);
            }

            _readTriangles(_model.Accessors[fbxMeshPrimitive.Indices.Value], jMesh);
        }
    }


    /**
     * Read one node from the gltf. One node translates to one entity. 
     */
    private void _readNode(glTFLoader.Schema.Node gltfNode, out DefaultEcs.Entity eNode)
    {
        /*
         * First, create the entity
         */
        eNode = _engine.CreateEntity("gltfnode");
        
        /*
         * Then read the mesh for this node.
         */
        if (gltfNode.Mesh != null)
        {
            _readMesh(_model.Meshes[gltfNode.Mesh.Value], out var jMesh);
        }

        /*
         * Finally, recurse to children nodes.
         */
        foreach (var idxChildNode in gltfNode.Children)
        {
            var nChild = _model.Nodes[idxChildNode];
            _readNode(nChild, out var eChild);
            I.Get<HierarchyApi>().SetParent(eChild, eNode); 
        }
    }
    

    private void _readScene(glTFLoader.Schema.Scene scene)
    {
        foreach (var idxOneRootNode in scene.Nodes)
        {
            _readNode(_model.Nodes[idxOneRootNode], out var eNode);
        }
    }
    
    
    private void _read()
    {
        if (null != _model.Scene)
        {
            _readScene(_model.Scenes[_model.Scene.Value]);
        }
    }


    static public void LoadModelInstanceSync(
        engine.Engine engine0,
        string url,
        ModelProperties modelProperties,
        out engine.joyce.InstanceDesc instanceDesc, out engine.ModelInfo modelInfo)
    {
        instanceDesc = new(
            new List<engine.joyce.Mesh>(),
            new List<int>(),
            new List<engine.joyce.Material>(),
            400f);

        modelInfo = new();

        Gltf? model = null;
        byte[]? binary = null;
        using (var fileStream = engine.Assets.Open(url))
        {
            try
            {
                model = Interface.LoadModel(fileStream);
            }
            catch (Exception e)
            {
                Error($"Unable to load json from gltf file: Exception: {e}");
            }
        }

        using (var fileStream = engine.Assets.Open(url))
        {
            try
            {
                binary = Interface.LoadBinaryBuffer(fileStream);
            }
            catch (Exception e)
            {
                Error($"Unable to load binaries from gltf file: Exception: {e}");
            }
        }

        if (model == null)
        {
            Warning($"Error load6ing model {url}.");
        }

        var g = new GlTF(engine0, model, binary);

        try
        {
            g._read();
        }
        catch (Exception e)
        {
            Error($"Error while loading gltf scene from {url}: {e}.");
        }
    }


    public GlTF(engine.Engine engine, Gltf model, byte[] binary)
    {
        _engine = engine;
        _model = model;
        _binary = binary;
    }
    
    
    public static void Unit(engine.Engine engine0)
    {
        LoadModelInstanceSync(engine0,
            "u.glb", 
            new ModelProperties()
            , out var _, out var _);
    }
}