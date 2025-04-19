using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using builtin.extensions;
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
    private Model _jModel;
    private Gltf _gltfModel;
    private byte[] _gltfBinary;
    private SortedDictionary<int, ModelNode> _dictNodesByGltf;
    
    
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
    private void _readVector4(int ofs, out Vector4 v4)
    {
        v4.X = BitConverter.ToSingle(_gltfBinary, ofs);
        v4.Y = BitConverter.ToSingle(_gltfBinary, ofs + sizeof(float));
        v4.Z = BitConverter.ToSingle(_gltfBinary, ofs + 2*sizeof(float));
        v4.W = BitConverter.ToSingle(_gltfBinary, ofs + 3*sizeof(float));
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

    
    private void _readVector2Array(in Accessor acc, in IList<Vector2> arr)
    {
        BufferView bvw = _gltfModel.BufferViews[acc.BufferView.Value];
        int ofs = bvw.ByteOffset;
        int length = acc.Count;
        
        for (int j = 0; j < length; ++j)
        {
            _readVector2(ofs+j*2*sizeof(float), out var v2);
            arr.Add(v2);
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void _readVector3Array(in Accessor acc, in IList<Vector3> arr)
    {
        BufferView bvw = _gltfModel.BufferViews[acc.BufferView.Value];
        int ofs = bvw.ByteOffset;
        int length = acc.Count;
        
        for (int j = 0; j < length; ++j)
        {
            _readVector3(ofs+j*3*sizeof(float), out var v3);
            arr.Add(v3);
        }
    }
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void _readVector4Array(in Accessor acc, in IList<Vector4> arr)
    {
        BufferView bvw = _gltfModel.BufferViews[acc.BufferView.Value];
        int ofs = bvw.ByteOffset;
        int length = acc.Count;
        
        for (int j = 0; j < length; ++j)
        {
            _readVector4(ofs+j*4*sizeof(float), out var v4);
            arr.Add(v4);
        }
    }
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void _readInt4ArrayFromUint8(int ofs, int length, in IList<Int4> arr)
    {
        for (int j = 0; j < length; ++j)
        {
            arr.Add(new Int4
            {
                B0 = (int) _gltfBinary[ofs+j+0],
                B1 = (int) _gltfBinary[ofs+j+1],
                B2 = (int) _gltfBinary[ofs+j+2],
                B3 = (int) _gltfBinary[ofs+j+3]
            });
        }
    }

    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void _readInt4ArrayFromUint16(int ofs, int length, in IList<Int4> arr)
    {
        for (int j = 0; j < length; ++j)
        {
            arr.Add(new Int4
            {
                B0 = (int) BitConverter.ToUInt16(_gltfBinary, ofs+4*sizeof(ushort)+0),
                B1 = (int) BitConverter.ToUInt16(_gltfBinary, ofs+4*sizeof(ushort)+1),
                B2 = (int) BitConverter.ToUInt16(_gltfBinary, ofs+4*sizeof(ushort)+2),
                B3 = (int) BitConverter.ToUInt16(_gltfBinary, ofs+4*sizeof(ushort)+3)
            });
        }
    }

    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void _readInt4ArrayFromUint32(int ofs, int length, in IList<Int4> arr)
    {
        for (int j = 0; j < length; ++j)
        {
            arr.Add(new Int4
            {
                B0 = (int) BitConverter.ToUInt32(_gltfBinary, ofs+4*sizeof(uint)+0),
                B1 = (int) BitConverter.ToUInt32(_gltfBinary, ofs+4*sizeof(uint)+1),
                B2 = (int) BitConverter.ToUInt32(_gltfBinary, ofs+4*sizeof(uint)+2),
                B3 = (int) BitConverter.ToUInt32(_gltfBinary, ofs+4*sizeof(uint)+3)
            });
        }
    }

    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void _readUint8Array(int ofs, int length, in IList<uint> arr)
    {
        for (int j = 0; j < length; ++j)
        {
            arr.Add(_gltfBinary[ofs+j]);
        }        
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void _readUint16Array(int ofs, int length, in IList<uint> arr)
    {
        for (int j = 0; j < length; ++j)
        {
            arr.Add((uint)BitConverter.ToUInt16(_gltfBinary, ofs + j * sizeof(ushort)));
        }        
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void _readUint32Array(int ofs, int length, in IList<uint> arr)
    {
        for (int j = 0; j < length; ++j)
        {
            arr.Add(BitConverter.ToUInt32(_gltfBinary, ofs + j * sizeof(uint)));
        }        
    }


    private void _readInt4Array(in Accessor acc, in IList<Int4> arr)
    {
        BufferView bvw = _gltfModel.BufferViews[acc.BufferView.Value];
        if (acc.Type == Accessor.TypeEnum.VEC4)
        {
            switch (acc.ComponentType)
            {
                case Accessor.ComponentTypeEnum.UNSIGNED_SHORT:
                    _readInt4ArrayFromUint16(bvw.ByteOffset,acc.Count, arr);
                    break;
                case Accessor.ComponentTypeEnum.UNSIGNED_INT:
                    _readInt4ArrayFromUint32(bvw.ByteOffset,acc.Count, arr);
                    break;
                case Accessor.ComponentTypeEnum.UNSIGNED_BYTE:
                    _readInt4ArrayFromUint8(bvw.ByteOffset,acc.Count, arr);
                    break;
                default:
                    ErrorThrow($"Unsupported component type {acc.ComponentType}.", m => new InvalidDataException(m));
                    break;
            }
        }
        else
        {
            ErrorThrow<InvalidDataException>($"Unsupported type {acc.Type}.");
        }
    }


    private void _readUintArray(in Accessor acc, in IList<uint> arr)
    {
        BufferView bvw = _gltfModel.BufferViews[acc.BufferView.Value];
        if (acc.Type == Accessor.TypeEnum.SCALAR)
        {
            switch (acc.ComponentType)
            {
                case Accessor.ComponentTypeEnum.UNSIGNED_SHORT:
                    _readUint16Array(bvw.ByteOffset,acc.Count, arr);
                    break;
                case Accessor.ComponentTypeEnum.UNSIGNED_INT:
                    _readUint32Array(bvw.ByteOffset,acc.Count, arr);
                    break;
                case Accessor.ComponentTypeEnum.UNSIGNED_BYTE:
                    _readUint8Array(bvw.ByteOffset,acc.Count, arr);
                    break;
                default:
                    ErrorThrow($"Unsupported component type {acc.ComponentType}.", m => new InvalidDataException(m));
                    break;
            }
        }
        else
        {
            ErrorThrow<InvalidDataException>($"Unsupported type {acc.Type}.");
        }
    }
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void _readSingleArray(int ofs, int length, in IList<float> arr)
    {
        for (int j = 0; j < length; ++j)
        {
            arr.Add((float)BitConverter.ToSingle(_gltfBinary, ofs + j * sizeof(float)));
        }        
    }


    private void _readFloatArray(in Accessor acc, in IList<float> arr)
    {
        BufferView bvw = _gltfModel.BufferViews[acc.BufferView.Value];
        if (acc.Type == Accessor.TypeEnum.SCALAR)
        {
            switch (acc.ComponentType)
            {
                case Accessor.ComponentTypeEnum.FLOAT:
                    _readSingleArray(bvw.ByteOffset,acc.Count, arr);
                    break;
                default:
                    ErrorThrow($"Unsupported component type {acc.ComponentType}.", m => new InvalidDataException(m));
                    break;
            }
        }
        else
        {
            ErrorThrow<InvalidDataException>($"Unsupported type {acc.Type}.");
        }
    }
    
    
    private void _readVertices(in Accessor acc, engine.joyce.Mesh jMesh)
    {
        if (acc.Type == Accessor.TypeEnum.VEC3)
        {
            if (acc.ComponentType == Accessor.ComponentTypeEnum.FLOAT)
            {
                _readVector3Array(acc, ref jMesh.Vertices);
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


    private void _readNormals(in Accessor acc, engine.joyce.Mesh jMesh)
    {
        if (acc.Type == Accessor.TypeEnum.VEC3)
        {
            if (acc.ComponentType == Accessor.ComponentTypeEnum.FLOAT)
            {
                _readVector3Array(acc, ref jMesh.Normals);
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


    private void _readTexcoords0(in Accessor acc, engine.joyce.Mesh jMesh)
    {
        if (acc.Type == Accessor.TypeEnum.VEC2)
        {
            if (acc.ComponentType == Accessor.ComponentTypeEnum.FLOAT)
            {
                _readVector2Array(acc, ref jMesh.UVs);
            }
            else
            {
                ErrorThrow<InvalidDataException>($"Unsupported component type {acc.ComponentType}.");
            }
        }
        else
        {
            ErrorThrow<InvalidDataException>($"Unsupported type {acc.Type}.");
        }
    }


    private void _readJointsWeights(in Accessor accJoints, Accessor accWeights, engine.joyce.Mesh jMesh)
    {
        if (accJoints.Type != Accessor.TypeEnum.VEC4)
        {
            ErrorThrow<InvalidDataException>($"Unexpected joint type {accJoints.Type}");
        }
        if (accWeights.Type != Accessor.TypeEnum.VEC4)
        {
            ErrorThrow<InvalidDataException>($"Unexpected weights type {accWeights.Type}");
        }

        jMesh.BoneIndices = new List<Int4>();
        jMesh.BoneWeights = new List<Vector4>();
        _readInt4Array(accJoints, ref jMesh.BoneIndices);
        _readVector4Array(accWeights, ref jMesh.BoneWeights);
    }
    

    private void _readMatrixFromArray(in Accessor acc, out Matrix4x4 m)
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


    private void _readTriangles(in Accessor acc, engine.joyce.Mesh jMesh)
    {
        BufferView bvwVertices = _gltfModel.BufferViews[acc.BufferView.Value];

        _readUintArray(acc, ref jMesh.Indices);
        
        /*
         * For triangles, make sure we are a count dividale by 3.
         */
        int overhead = jMesh.Indices.Count % 3;
        for (int i = 0; i < overhead; ++i)
        {
            jMesh.Indices.RemoveAt(jMesh.Indices.Count-1);
        }
    }


    private void _readMesh(ModelNode mn, glTFLoader.Schema.Mesh fbxMesh, out MatMesh matMesh)
    {
        matMesh = new();
        foreach (var fbxMeshPrimitive in fbxMesh.Primitives)
        {
            int idxPosition = -1;
            int idxNormal = -1;
            int idxTexcoord0 = -1;
            int idxJoints0 = -1;
            int idxWeights0 = -1;

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
                    case "JOINTS_0":
                        idxJoints0 = fbxAttr.Value;
                        break;
                    case "WEIGHTS_0":
                        idxWeights0 = fbxAttr.Value;
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

            if (idxJoints0 != -1 && idxWeights0 != -1)
            {
                _readJointsWeights(_gltfModel.Accessors[idxJoints0], _gltfModel.Accessors[idxWeights0], jMesh);
            }

            _readTriangles(_gltfModel.Accessors[fbxMeshPrimitive.Indices.Value], jMesh);
            
            matMesh.Add(new() { Texture = I.Get<TextureCatalogue>().FindColorTexture(0xff888888)}, jMesh, mn);
        }
    }


    /**
     * Read one node from the gltf. One node translates to one entity.
     */
    private void _readNode(int idxGltfNode, ModelNode mnParent, out ModelNode mn)
    {
        var gltfNode = _gltfModel.Nodes[idxGltfNode];
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
        mn = new() { Model = _jModel, Parent = mnParent, Name = gltfNode.Name };
        _dictNodesByGltf[idxGltfNode] = mn;
        mn.Transform = new Transform3ToParent(
            true, 0xffffffff, m
            );
        
        /*
        * Then read the mesh for this node.
        */
        if (gltfNode.Mesh != null)
        {
            Trace("Reading a mesh.");
            _readMesh(mn, _gltfModel.Meshes[gltfNode.Mesh.Value], out MatMesh matMesh);
            var id = InstanceDesc.CreateFromMatMesh(matMesh, 200f);
            mn.InstanceDesc = id;
        }
        else
        {
            // Trace("Encountered node without mesh.");
        }
        
        /*
         * Recurse to children nodes.
         */
        if (gltfNode.Children != null)
        {
            
            foreach (var idxChildNode in gltfNode.Children)
            {
                _readNode(idxChildNode, mn, out var mnChild);
                mn.AddChild(mnChild);
            }
        }
    }
    
    
    private void _readSkin(ModelNode mnParent, glTFLoader.Schema.Skin gltfSkin)
    {
        int idxMatrix = 0;
        var skeleton = _jModel.FindSkeleton();

        Accessor? accMatrix = null;
        if (gltfSkin.InverseBindMatrices != null)
        {
            accMatrix = _gltfModel.Accessors[gltfSkin.InverseBindMatrices.Value];
        }

        /*
         * For each node containing a mesh, a skin may be defined.
         * The skin consists of
         * - an array of nodes, each defining a transformation and hierarchy.
         * - an array of inverse bind matrices, each defining the model to bone transformation.
         */

        uint realIndex = 0;
        foreach (var idxJoint in gltfSkin.Joints)
        {
            if (!_dictNodesByGltf.TryGetValue(idxJoint, out var mnJointNode))
            {
                Warning($"Invalid joint node index {idxJoint} discovered.");
                continue;
            }
            
            var nodeJoint = _gltfModel.Nodes[idxJoint];

            var jBone = skeleton.FindBone(nodeJoint.Name);
            if (accMatrix != null)
            {
                _readMatrixFromArray(accMatrix, out jBone.Model2Bone);
                jBone.Bone2Model = MatrixInversion.Invert(jBone.Model2Bone);
            }
            /*
             * This is relying on two independent counters. Let's be safe.
             */
            Debug.Assert(realIndex == jBone.Index);
            ++realIndex;
        }
    }


    private void _readSkins(int idxGltfNode, engine.joyce.ModelNode mn)
    {
        var gltfNode = _gltfModel.Nodes[idxGltfNode];
        
        /*
         * Is there a skin for this node? Then read it.
         */
        if (gltfNode.Skin != null)
        {
            if (gltfNode.Mesh != null)
            {
                Warning("Encountered a skin without a mesh");
            }
            Trace("Reading a skin.");
            _readSkin(mn, _gltfModel.Skins[gltfNode.Skin.Value]);
        }

        /*
         * Recurse to children nodes.
         */
        if (gltfNode.Children != null)
        {
            
            foreach (var idxChildNode in gltfNode.Children)
            {
                var mnChild = _dictNodesByGltf[idxChildNode];
                _readSkins(idxChildNode, mnChild);
            }
        }
        
    }
    

    class SamplerKeyframes
    {
        public float[] TimeDomain;
        public List<Vector3> Vector3 = null;
        public List<Vector4> Vector4 = null;
        
        public KeyFrame<Vector3>[] AsVector3Keyframes()
        {
            if (null == TimeDomain)
            {
                Error("No time domain for sampler.");
                return null;
            }

            if (null == Vector3 && null == Vector4)
            {
                Error("No data for sampler.");
            }
            int l = Int32.Max(TimeDomain.Length, Vector3.Count);
            var arr = new KeyFrame<Vector3>[l];

            if (null != Vector3)
            {
                for (int i = 0; i < l; ++i)
                {
                    arr[i].Time = TimeDomain[i];
                    arr[i].Value = Vector3[i];
                }
            } else if (null != Vector4)
            {
                for (int i = 0; i < l; ++i)
                {
                    arr[i].Time = TimeDomain[i];
                    arr[i].Value = new Vector3(Vector4[i].X, Vector4[i].Y, Vector4[i].Z);
                }
            }

            return arr;
        }

        
        public KeyFrame<Quaternion>[] AsQuaternionKeyframes()
        {
            if (null == TimeDomain)
            {
                Error("No time domain for sampler.");
                return null;
            }

            if (null == Vector4)
            {
                Error("No data for sampler.");
            }
            int l = Int32.Max(TimeDomain.Length, Vector4.Count);
            var arr = new KeyFrame<Quaternion>[l];

            for (int i = 0; i < l; ++i)
            {
                arr[i].Time = TimeDomain[i];
                arr[i].Value = new Quaternion(Vector4[i].X, Vector4[i].Y, Vector4[i].Z, Vector4[i].W);
            }

            return arr;
        }

    }
    

    private void _loadAnimations(Model jModel)
    {
        var glAnimations = _gltfModel.Animations;
        if (null == glAnimations)
        {
            return;
        }

        foreach (var glAnimation in glAnimations)
        {
            ModelAnimation ma = jModel.CreateAnimation();
            ma.Name = glAnimation.Name;
            
            /*
             * A gltf animation contains "samplers", contaning the actual
             * time sampled values over time, and "channels", building the association
             * between an animation target and a sampler.
             */

            SamplerKeyframes[] samplerKeyframes = new SamplerKeyframes[glAnimation.Samplers.Length];

            uint idxSampler = 0; 
            foreach (var glSampler in glAnimation.Samplers)
            {
                SamplerKeyframes mak = new();
                samplerKeyframes[idxSampler] = mak;
                ++idxSampler;
                
                /*
                 * glSampler.input contains the index of the accessor of a float array
                 * containing the time domain values.
                 *
                 * glSampler.output contains a Vector3 or Vector4 array of data. It is
                 * associated with the proper channel later on.
                 */
                Accessor accInput = _gltfModel.Accessors[glSampler.Input];
                List<float> arrTimeDomain = new List<float>(accInput.Count);
                _readFloatArray(accInput, arrTimeDomain);
                mak.TimeDomain = arrTimeDomain.ToArray();
                
                Accessor accOutput = _gltfModel.Accessors[glSampler.Output];
                BufferView bvwOutput = _gltfModel.BufferViews[accOutput.BufferView.Value];

                switch (accOutput.Type)
                {
                    case Accessor.TypeEnum.VEC3:
                        mak.Vector3 = new List<Vector3>(accOutput.Count);
                        _readVector3Array(accOutput, mak.Vector3);
                        break;
                    case Accessor.TypeEnum.VEC4:
                        mak.Vector4 = new List<Vector4>(accOutput.Count);
                        _readVector4Array(accOutput, mak.Vector4);
                        break;
                    default:
                        continue;
                }
            }

            /*
             * Now read all anim channels in the gltf sense, mapping one sampler
             * to one node and some property of it.
             */
            SortedDictionary<int, ModelAnimChannel> mapNodeAnimChannel = new();

            var glChannels = glAnimation.Channels;
            ma.MapChannels = new();
            
            foreach (var glAnimChannel in glChannels)
            {
                ModelAnimChannel mac;
                if (glAnimChannel.Sampler < 0 || glAnimChannel.Sampler >= samplerKeyframes.Length)
                {
                    Error($"Invalid sampler index {glAnimChannel.Sampler} encountered.");
                    continue;
                }

                var skf = samplerKeyframes[glAnimChannel.Sampler];
                if (null == skf)
                {
                    Error($"Referencing unknown sampler {glAnimChannel.Sampler}");
                    continue;
                }
                
                if (!mapNodeAnimChannel.TryGetValue(glAnimChannel.Target.Node.Value, out mac))
                {
                    var jTargetNode = _dictNodesByGltf[glAnimChannel.Target.Node.Value];
                    mac = ma.CreateChannel(jTargetNode, null, null, null);
                    ma.MapChannels.Add(jTargetNode, mac);
                    mapNodeAnimChannel.Add(glAnimChannel.Target.Node.Value, mac);
                }

                switch (glAnimChannel.Target.Path)
                {
                    case AnimationChannelTarget.PathEnum.translation:
                        mac.Positions = skf.AsVector3Keyframes();
                        break;
                    case AnimationChannelTarget.PathEnum.rotation:
                        mac.Rotations = skf.AsQuaternionKeyframes();
                        break;
                    case AnimationChannelTarget.PathEnum.scale:
                        mac.Scalings = skf.AsVector3Keyframes();
                        break;
                    default:
                        break;
                }
            }

            /*
             * After we read all channels, we need to compute the duration etc. of the animation.
             * Unfortunately, we need to look at the end time of each of the channels to find out
             * the total running duration.
             */
            float tMax = 0f;
            foreach (var skf in samplerKeyframes)
            {
                if (skf.TimeDomain == null)
                {
                    continue;
                }

                var l = skf.TimeDomain.Length;
                if (0 == l)
                {
                    continue;
                }

                tMax = Single.Max(skf.TimeDomain[l - 1], tMax);
            }

            ma.Duration = tMax;
            ma.TicksPerSecond = 60;
            ma.NTicks = (uint)(tMax * 60f);
            ma.NFrames = (uint)(tMax * 60f + 0.5f); 
            jModel.PushAnimFrames(ma.NFrames);
        }
    }
    
    
    private void _readScene(glTFLoader.Schema.Scene scene, out Model jModel)
    {
        _dictNodesByGltf = new();
        _jModel = jModel = new();
        
        /*
         * First read the actual model's nodes.
         */
        List<ModelNode> rootNodes = new();
        foreach (var idxRootNode in scene.Nodes)
        {
            _readNode(idxRootNode, null, out var mnNode);
            rootNodes.Add(mnNode);
        }
        foreach (var idxRootNode in scene.Nodes)
        {
            _readSkins(idxRootNode, _dictNodesByGltf[idxRootNode]);
        }
        
        /*
         * If there is exactly one root node, make it the child of the entity.  
         */
        if (rootNodes.Count == 1)
        {
            jModel.RootNode = rootNodes[0];
        }
        else if (rootNodes.Count > 0)
        {
            ModelNode mnRoot = new()
            {
                Parent = null,
                Children = rootNodes,
                Model = jModel
            };
            jModel.RootNode = mnRoot;
            foreach (var mnRootChild in rootNodes)
            {
                mnRootChild.Parent = mnRoot;
            }
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
            _loadAnimations(jModel);
            jModel.BakeAnimations();
            jModel.Polish();
            return jModel;
        }

        return null;
    }


    public static void LoadModelInstanceSync(
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
            jModel.Name = url;
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
    
    
    public static Task<Model> 
        LoadModelInstance(string url, ModelProperties modelProperties)
    {
        return Task.Run(() =>
        {
            LoadModelInstanceSync(url, modelProperties, out var model);
            return model;
        });
    }   
}