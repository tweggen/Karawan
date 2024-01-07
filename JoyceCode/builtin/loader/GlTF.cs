using System;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using engine;
using engine.geom;
using engine.joyce;
using engine.joyce.components;
using glTFLoader;
using glTFLoader.Schema;
using static engine.Logger;
using Skin = engine.joyce.Skin;

namespace builtin.loader;

public class GlTF
{
    private Gltf _gltfModel;
    private byte[] _gltfBinary;
    private SortedDictionary<int, ModelNode> _dictNodes;
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void _readVector2(int ofs, out Vector2 v2)
    {
        v2.X = BitConverter.ToSingle(_gltfBinary, ofs);
        v2.Y = BitConverter.ToSingle(_gltfBinary, ofs + sizeof(float));
    }
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void _readVector3(int ofs, out Vector3 v3)
    {
        v3.X = BitConverter.ToSingle(_gltfBinary, ofs);
        v3.Y = BitConverter.ToSingle(_gltfBinary, ofs + sizeof(float));
        v3.Z = BitConverter.ToSingle(_gltfBinary, ofs + 2*sizeof(float));
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void _readMatrix4x4(int ofs, out Matrix4x4 m)
    {
        float[] f = new float[16];
        for (int i = 0; i < 16; ++i)
        {
            f[i] = BitConverter.ToSingle(_gltfBinary, ofs + i * sizeof(float));
        }

        m = new(
            f[0], f[1], f[2], f[3],
            f[4], f[5], f[6], f[7],
            f[8], f[9], f[10], f[11],
            f[12], f[13], f[14], f[15]);
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


    private void _readTri16Array(int ofs, int length, ref IList<uint> arr)
    {
        int l = (length / 3) * 3;
        for (int j = 0; j < l; ++j)
        {
            arr.Add((uint)BitConverter.ToUInt16(_gltfBinary, ofs + j * sizeof(ushort)));
        }        
    }


    private void _readTri32Array(int ofs, int length, ref IList<uint> arr)
    {
        int l = (length / 3) * 3;
        for (int j = 0; j < l; ++j)
        {
            arr.Add(BitConverter.ToUInt32(_gltfBinary, ofs + j * sizeof(uint)));
        }        
    }


    private void _readVertices(Accessor acc, engine.joyce.Mesh jMesh)
    {
        BufferView bvwVertices = _gltfModel.BufferViews[acc.BufferView.Value];

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
        BufferView bvwVertices = _gltfModel.BufferViews[acc.BufferView.Value];

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
        BufferView bvwVertices = _gltfModel.BufferViews[acc.BufferView.Value];

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


    private void _readMatrixFromArray(Accessor acc, out Matrix4x4 m)
    {
        BufferView bvwMatrices = _gltfModel.BufferViews[acc.BufferView.Value];
        if (acc.Type == Accessor.TypeEnum.MAT4)
        {
            if (acc.ComponentType == Accessor.ComponentTypeEnum.FLOAT)
            {
                _readMatrix4x4(bvwMatrices.ByteOffset, out m);
            }
            else
            {
                ErrorThrow($"Unsupported component type {acc.ComponentType}.", m => new InvalidDataException(m));
                m = new();
            }
        }
        else
        {
            ErrorThrow($"Unsupported type {acc.Type}.", m => new InvalidDataException(m));
            m = new();
        }
    }
    
    
    private void _readTriangles(Accessor acc, engine.joyce.Mesh jMesh)
    {
        BufferView bvwVertices = _gltfModel.BufferViews[acc.BufferView.Value];

        if (acc.Type == Accessor.TypeEnum.SCALAR)
        {
            switch (acc.ComponentType)
            {
                case Accessor.ComponentTypeEnum.UNSIGNED_SHORT:
                    _readTri16Array(bvwVertices.ByteOffset,acc.Count, ref jMesh.Indices);
                    break;
                case Accessor.ComponentTypeEnum.UNSIGNED_INT:
                    _readTri32Array(bvwVertices.ByteOffset,acc.Count, ref jMesh.Indices);
                    break;
                default:
                    ErrorThrow($"Unsupported component type {acc.ComponentType}.", m => new InvalidDataException(m));
                    break;
            }
        }
        else
        {
            ErrorThrow($"Unsupported type {acc.Type}.", m => new InvalidDataException(m));
        }
    }


    private void _readMesh(glTFLoader.Schema.Mesh fbxMesh, out MatMesh matMesh)
    {
        matMesh = new();
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
                Warning("Found mesh without indices (no index).");
                /*
                 * A mesh without vertex positions is pointless.
                 */
                continue;
            }

            if (null == fbxMeshPrimitive.Indices)
            {
                Warning("Found mesh without indices (null pointer).");
                /*
                 * Also, without an index we do not need to access this mesh.
                 */
                continue;
            }

            engine.joyce.Mesh jMesh = new("gltf", new List<Vector3>(), new List<uint>(), new List<Vector2>());
            jMesh.Normals = new List<Vector3>();
            
            /*
             * Now let's iterate through the vertex positions, adding normals and
             * texcoords, if we have any.
             */
            _readVertices(_gltfModel.Accessors[idxPosition], jMesh);
            if (idxNormal != -1)
            {
                _readNormals(_gltfModel.Accessors[idxNormal], jMesh);
            }

            if (idxTexcoord0 != -1)
            {
                _readTexcoords0(_gltfModel.Accessors[idxTexcoord0], jMesh);
            }

            _readTriangles(_gltfModel.Accessors[fbxMeshPrimitive.Indices.Value], jMesh);
            
            matMesh.Add(new() { AlbedoColor = 0xff888888}, jMesh);
        }
    }


    /**
     * Read one node from the gltf. One node translates to one entity.
     */
    private void _readNode(glTFLoader.Schema.Node gltfNode, out ModelNode mn)
    {
        var mf = gltfNode.Matrix;
        Matrix4x4 m = new(
            mf[0], mf[1], mf[2], mf[3],
            mf[4], mf[5], mf[6], mf[7],
            mf[8], mf[9], mf[10], mf[11],
            mf[12], mf[13], mf[14], mf[15]
        );
        /*
         * Now apply the explicit transformation etc.
         */
        m *= Matrix4x4.CreateScale(gltfNode.Scale[0], gltfNode.Scale[1], gltfNode.Scale[2]);
        m *= Matrix4x4.CreateFromQuaternion(
            new Quaternion(
                gltfNode.Rotation[0], gltfNode.Rotation[1],
                gltfNode.Rotation[2], gltfNode.Rotation[3]));
        m *= Matrix4x4.CreateTranslation(
            gltfNode.Translation[0], gltfNode.Translation[1], gltfNode.Translation[2]);
        mn = new();
        mn.Transform = new Transform3ToParent(
            true, 0xffffffff, m
            );
        
        /*
        * Then read the mesh for this node.
        */
        if (gltfNode.Mesh != null)
        {
            Trace("Reading a mesh.");
            _readMesh(_gltfModel.Meshes[gltfNode.Mesh.Value], out MatMesh matMesh);
            mn.InstanceDesc = InstanceDesc.CreateFromMatMesh(matMesh, 200f);
        }
        else
        {
            Trace("Encountered node without mesh.");
        }
        
        /*
         * Finally, recurse to children nodes.
         */
        if (gltfNode.Children != null)
        {
            mn.Children = new List<ModelNode>();
            foreach (var idxChildNode in gltfNode.Children)
            {
                var nChild = _gltfModel.Nodes[idxChildNode];
                _readNode(nChild, out var mnChild);
                mn.Children.Add(mnChild);
            }
        }
    }


    private void _readSkin(glTFLoader.Schema.Skin gltfSkin, out Skin jSkin)
    {
        jSkin = new();
        jSkin.Joints = new List<Joint>();

        int idxMatrix = 0;

        Accessor? accMatrix = null;
        if (gltfSkin.InverseBindMatrices != null)
        {
            accMatrix = _gltfModel.Accessors[gltfSkin.InverseBindMatrices.Value];
        }

        /*
         * The joints are store a bit while spread
         */
        foreach (var idxJoint in gltfSkin.Joints)
        {
            /*
             * joint info also is stored inside the nodes we already read.
             * So we just need to use the references.
             */
            var nJoint = _gltfModel.Nodes[idxJoint];
            Joint joint = new();
            jSkin.Joints.Add(joint);
            if (accMatrix != null)
            {
                _readMatrixFromArray(accMatrix, out joint.InverseBindMatrix);
            }
            ModelNode mnJoint = _dictNodes[idxJoint];
            foreach (int idxControlled in nJoint.Children)
            {
                var nControlled = _gltfModel.Nodes[idxControlled];
                var mnControlled = _dictNodes[idxControlled];
                joint.ControlledNodes.Add(mnControlled);
            }
            ++idxMatrix;
        }
    }
    
    
    private void _readScene(glTFLoader.Schema.Scene scene, out Model jModel)
    {
        _dictNodes = new();
        
        /*
         * First read the actual model's nodes.
         */
        List<ModelNode> rootNodes = new();
        foreach (var idxRootNode in scene.Nodes)
        {
            _readNode(_gltfModel.Nodes[idxRootNode], out var mnNode);
            _dictNodes[idxRootNode] = mnNode;
            rootNodes.Add(mnNode);
        }
        
        /*
         * Check, if we read all the nodes
         */
        // TXWTODO: do it

        jModel = new();
        if (rootNodes.Count == 1)
        {
            jModel.RootNode = rootNodes[0];
        }
        else if (rootNodes.Count > 0)
        {
            ModelNode mnRoot = new()
            {
                Children = rootNodes
            };
            jModel.RootNode = mnRoot;
        }
        else
        {
            ErrorThrow($"Root node has no children!?", m => new InvalidOperationException(m));
        }
    }
    
    
    private Model? _read()
    {
        if (null != _gltfModel.Scene)
        {
            _readScene(_gltfModel.Scenes[_gltfModel.Scene.Value], out var jModel);
            return jModel;
        }

        return null;
    }


    static public void LoadModelInstanceSync(
        string url,
        ModelProperties modelProperties,
        out Model jModel)
    {

        Gltf? gModel = null;
        byte[]? binary = null;
        using (var fileStream = engine.Assets.Open(url))
        {
            try
            {
                gModel = Interface.LoadModel(fileStream);
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

        if (gModel == null)
        {
            Warning($"Error load6ing model {url}.");
        }

        var g = new GlTF(gModel, binary);

        try
        {
            jModel = g._read();
        }
        catch (Exception e)
        {
            ErrorThrow($"Error while loading gltf scene from {url}: {e}.", m => new InvalidOperationException(m));
            jModel = new Model();
        }
    }


    public GlTF(Gltf gltfModel, byte[] gltfBinary)
    {
        _gltfModel = gltfModel;
        _gltfBinary = gltfBinary;
    }
    
    
    static public Task<Model> 
        LoadModelInstance(string url, ModelProperties modelProperties)
    {
        return Task.Run(() =>
        {
            LoadModelInstanceSync(url, modelProperties, out var model);
            return model;
        });
    }   
    
    
    public static void Unit()
    {
        LoadModelInstanceSync("u.glb", new ModelProperties(), out var _);
    }
}