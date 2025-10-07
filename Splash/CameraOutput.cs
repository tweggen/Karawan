using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using engine;
using engine.joyce;
using engine.world;
using static engine.Logger;

namespace Splash;


public class CameraOutput
{
    private object _lo = new();

    private engine.joyce.components.Camera3 _camera3;

    public uint CameraMask
    {
        get => _camera3.CameraMask;
    }

    public engine.joyce.components.Camera3 Camera3
    {
        get => _camera3;
    }

    public Flags.AnimBatching AnimBatching = 0;

    public Matrix4x4 TransformToWorld;
    private Matrix4x4 _mInverseCameraRotation;
    private Vector3 _v3CameraPos;


    private FrameStats _frameStats;

    private Dictionary<MaterialBatchKey, MaterialBatch> _materialBatches = new();
    private Dictionary<MaterialBatchKey, MaterialBatch> _transparentMaterialBatches = new();
    private List<MaterialBatch> _transparentMaterialList = null;

    private readonly IThreeD _threeD;

    private readonly InstanceManager _instanceManager;

    private float _defaultUploadMaterialPerFrame = 1f;
    private float _defaultUploadMeshPerFrame = 1f;

    private IScene _scene;
    private readonly Vector3 _v3CameraZ;

    /**
     * Inverse the effect of camera scaling to implement unscalable
     * materials.
     */
    private Matrix4x4 _mUnscale;

    public void GetRotation(ref Matrix4x4 mOut, in Matrix4x4 mIn)
    {
        /*
         * Note: We know/assume, the camera matrix has a scaling of one.
         */
        mOut.M11 = mIn.M11;
        mOut.M12 = mIn.M12;
        mOut.M13 = mIn.M13;
        mOut.M14 = 0f;
        mOut.M21 = mIn.M21;
        mOut.M22 = mIn.M22;
        mOut.M23 = mIn.M23;
        mOut.M24 = 0f;
        mOut.M31 = mIn.M31;
        mOut.M32 = mIn.M32;
        mOut.M33 = mIn.M33;
        mOut.M34 = 0f;
        mOut.M41 = 0f;
        mOut.M42 = 0f;
        mOut.M41 = 0f;
        mOut.M44 = 1f;
    }


    private float _msUploadMaterialPerFrame(in IScene scene)
    {
        if ((_camera3.CameraFlags & engine.joyce.components.Camera3.Flags.PreloadOnly) != 0)
        {
            /*
             * If we do not render, grant a lot of time for uploading, cause we most likely
             * blank for upload
             */
            return 16f;
        }
        else
        {
            return _defaultUploadMaterialPerFrame;
        }
    }


    private float _msUploadMeshPerFrame(in IScene scene)
    {
        if ((_camera3.CameraFlags & engine.joyce.components.Camera3.Flags.PreloadOnly) != 0)
        {
            /*
             * If we do not render, grant a lot of time for uploading, cause we most likely
             * blank for upload
             */
            return 16f;
        }
        else
        {
            return _defaultUploadMeshPerFrame;
        }
    }


    /**
     * The actual rendering method. Must be called from the context of
     * the render thread class. The material batch must not be used concurrently.
     *
     * Uploaders meshes/textures if required.
     */
    private void _renderMaterialBatchesNoLock(in IThreeD threeD,
        in IEnumerable<MaterialBatch> mb)
    {
        Stopwatch swUpload = new();
        bool preloadOnly = (_camera3.CameraFlags & engine.joyce.components.Camera3.Flags.PreloadOnly) != 0;

        var msUploadMaterialPerFrame = _msUploadMaterialPerFrame(_scene);
        var msUploadMeshPerFrame = _msUploadMeshPerFrame(_scene);

#if false
        /*
         * Test code for Z Buffer issues: Invert drawing order of different materials.
         */
        IEnumerable<MaterialBatch> testMB;
        if (CameraMask == 0x00800000)
        {
            testMB = mb.Reverse();
        }
        else
        {
            testMB = mb;
        }
#endif
        
        foreach (var materialItem in mb)
        {
            var aMaterialEntry = materialItem.AMaterialEntry;
            var jMaterial = aMaterialEntry.JMaterial;
            bool needMaterial = (!aMaterialEntry.IsUploaded()) || aMaterialEntry.IsOutdated();
            if (needMaterial)
            {
                if (jMaterial.UploadImmediately || swUpload.Elapsed.TotalMilliseconds < msUploadMaterialPerFrame)
                {
                    swUpload.Start();
                    _threeD.FillMaterialEntry(aMaterialEntry);
                    swUpload.Stop();
                }
                else
                {
                    ++_frameStats.NSkippedMaterials;
                    continue;
                }
            }

            IEnumerable<MeshBatch> listMeshBatches = materialItem.ListMeshBatches;
            if (null == listMeshBatches)
            {
                listMeshBatches = materialItem.MeshBatches.Values;
            }
            
            foreach (var meshItem in listMeshBatches)
            {
                var aMeshEntry = meshItem.AMeshEntry;
                var jMesh = aMeshEntry.Params.JMesh;
                bool haveMesh = aMeshEntry.IsUploaded();
                if (!haveMesh)
                {
                    if (jMesh.UploadImmediately || swUpload.Elapsed.TotalMilliseconds < msUploadMeshPerFrame)
                    {
                        swUpload.Start();
                        _threeD.UploadMeshEntry(meshItem.AMeshEntry);
                        swUpload.Stop();
                    }
                    else
                    {
                        ++_frameStats.NSkippedMeshes;
                        continue;
                    }
                }

                if (!preloadOnly)
                {
                    IEnumerable<AnimationBatch> listAnimationBatches = meshItem.ListAnimationBatches;
                    if (null == listAnimationBatches)
                    {
                        listAnimationBatches = meshItem.AnimationBatches.Values;
                    }

                    foreach (var animationItem in listAnimationBatches)
                    {
                        var nMatrices = animationItem.Matrices.Count;

                        if (!animationItem.AAnimationsEntry.IsUploaded())
                        {
                            _threeD.UploadAnimationsEntry(animationItem.AAnimationsEntry);
                        }
                        
                        /*
                         * I must draw using the instanced call because I only use an instanced shader.
                         */
#if NET6_0_OR_GREATER
                        var spanMatrices = CollectionsMarshal.AsSpan<Matrix4x4>(animationItem.Matrices);
                        var spanFramenos = CollectionsMarshal.AsSpan<uint>(animationItem.FrameNos);
#else
                        Span<Matrix4x4> spanMatrices = meshItem.Value.Matrices.ToArray();
                        Span<uint> spanFramenos = meshItem.Value.Framenos.ToArray();
#endif
                        ModelBakedFrame? modelBakedFrame;
                        if (animationItem.AnimationState.ModelAnimation != null
                            && animationItem.AnimationState.ModelAnimation.BakedFrames != null)
                        {
                            uint frameno = animationItem.AnimationState.ModelAnimationFrame;
                            if (frameno >= (uint)(animationItem.AnimationState.ModelAnimation.BakedFrames.Count()))
                            {
                                Error($"Frame number out of bounds.");
                                frameno = 0;
                            }
                            modelBakedFrame =
                                animationItem.AnimationState.ModelAnimation.BakedFrames[frameno];
                        }
                        else
                        {
                            modelBakedFrame = null;
                        }
                        threeD.DrawMeshInstanced(meshItem.AMeshEntry, materialItem.AMaterialEntry, animationItem.AAnimationsEntry, 
                            spanMatrices, spanFramenos, nMatrices, modelBakedFrame);
                        _frameStats.NTriangles += nMatrices * jMesh.Indices.Count / 3;
                    }
                }
                else
                {
                    threeD.FinishUploadOnly(meshItem.AMeshEntry);
                }
            }
        }
    }


    private void _appendInstanceNoLock(
        in AMeshEntry aMeshEntry,
        in AMaterialEntry aMaterialEntry,
        AAnimationsEntry? aAnimationsEntry,
        in Matrix4x4 matrix,
        in engine.joyce.components.AnimationState cAnimationState)
    {
        _frameStats.NEntities++;
        bool trackPositions;

        /*
         * Do we have an entry for the material?
         */
        Dictionary<MaterialBatchKey, MaterialBatch> mbs;
        if (!aMaterialEntry.HasTransparency())
        {
            mbs = _materialBatches;
            trackPositions = false;
        }
        else
        {
            mbs = _transparentMaterialBatches;
            trackPositions = true;
        }

        MaterialBatch materialBatch;
        MaterialBatchKey materialBatchKey = new(aMaterialEntry);
        mbs.TryGetValue(materialBatchKey, out materialBatch);
        if (null == materialBatch)
        {
            materialBatch = new MaterialBatch(aMaterialEntry);
            mbs[materialBatchKey] = materialBatch;
            _frameStats.NMaterials++;
        }
        
        var meshBatch = materialBatch.Add(aMeshEntry, AnimBatching, _frameStats);

        uint frameno = 0;
        if (null == aAnimationsEntry)
        {
            if (cAnimationState.ModelAnimation != null && cAnimationState.ModelAnimationFrame != 0)
            {
                int a = 1;
            }
            aAnimationsEntry = NullAnimationsEntry.Instance();
        }
        else
        {
            ModelAnimation? ma = cAnimationState.ModelAnimation;
            if (ma != null)
            {
                frameno = ma.FirstFrame + cAnimationState.ModelAnimationFrame;
            }
        }
        
        meshBatch.Add(aAnimationsEntry, cAnimationState, matrix, frameno, _frameStats);

        /*
         * In particular when rendering transparency, we need to have average
         * distances to sort the draw instance calls.
         */
        _frameStats.NInstances++;
        if (trackPositions)
        {
            Vector3 pos = matrix.Translation + aMeshEntry.Params.JMesh.AABB.Center;
            materialBatch.NMeshes++;
            materialBatch.SumOfPositions += pos;
            meshBatch.SumOfPositions += pos;
        }
    }

    void _computeInverseBillboardMatrix(
        in AMaterialEntry aMaterialEntry,
        in AMeshEntry aMeshEntry, in Matrix4x4 transform3ToWorld,
        in InstanceDesc id,
        out Matrix4x4 m)
    {
        /*
         * This is inefficient.
         * Compute the center of the mesh.
         *
         * We have a mesh, aligned with z axis (which is close to perfect).
         *
         * Instance transform and transformToWorld matrices. ... but ..., the
         * canera natrix still will be applied.
         *
         * Everything we pass here as matrix argument will be multiplied by the
         * camera to world and the projection matrix by the vertex shader.
         *
         * What do we want? We want a matrix argument solely translating the
         * position.
         *
         * Then, we want to apply the inverse
         * Plus, we want to apply the inverse camera rotation matrix.
         * So let's start.
         */
        Vector3 vc = Vector3.Zero;
        {
            int l = aMeshEntry.Params.JMesh.Vertices.Count;
            for (int vi = 0; vi < l; ++vi)
            {
                vc += aMeshEntry.Params.JMesh.Vertices[vi];
            }

            vc /= l;
        }

        Matrix4x4 mTrans = id.ModelTransform * transform3ToWorld;
        Matrix4x4 mInvRot = Matrix4x4.Identity;
        GetRotation(ref mInvRot, mTrans);
        mInvRot = Matrix4x4.Transpose(mInvRot);

        m =
                Matrix4x4.CreateTranslation(-vc)
                * _mInverseCameraRotation
                * mInvRot
                * Matrix4x4.CreateTranslation(vc)
                * mTrans
            ;

        if (aMaterialEntry.JMaterial.IsUnscalable)
        {
            m = _mUnscale * m;
        }
    }


    public void AppendInstance(
        in Splash.components.PfInstance pfInstance, 
        in Matrix4x4 transform3ToWorld,
        in engine.joyce.components.AnimationState cAnimationState)
    {
        lock (_lo)
        {
            engine.joyce.InstanceDesc id = pfInstance.InstanceDesc;
            int nMeshes = pfInstance.AMeshEntries.Count;
            int nMaterialIndices = id.MeshMaterials.Count;
            int nMaterials = pfInstance.AMaterialEntries.Count;
            int nAnimations = pfInstance.AAnimationsEntries.Count;
            
            for (int i = 0; i < nMeshes; ++i)
            {
                AMeshEntry aMeshEntry = pfInstance.AMeshEntries[i];
                AMaterialEntry aMaterialEntry = null;
                AAnimationsEntry? aAnimationsEntry = null;
                
                if (i < nMaterialIndices)
                {
                    int materialIndex = id.MeshMaterials[i];
                    if (materialIndex < nMaterials)
                    {
                        aMaterialEntry = pfInstance.AMaterialEntries[materialIndex];
                    }
                    else
                    {
                        Error($"Invalid material index {materialIndex} > nMaterials=={nMaterials}");
                        continue;
                    }
                }
                else
                {
                    Error($"Invalid index ({i} >= nMaterialIndices=={nMaterialIndices}");
                    continue;
                }

                if (i < nAnimations)
                {
                    aAnimationsEntry = pfInstance.AAnimationsEntries[i];
                }

                /*
                 * The mesh might not yet be created, or ready for platform. In that case
                 * we do not need to add it to the rendering list.
                 */
                if (null == aMeshEntry)
                {
                    continue;
                }
                if (!aMeshEntry.IsFilled())
                {
                    _threeD.FillMeshEntry(aMeshEntry);
                }
                if (!aMeshEntry.IsFilled())
                {
                    continue;
                }
                        
                
                if (null == aMaterialEntry)
                {
                    aMaterialEntry = _threeD.GetDefaultMaterial();
                }

                /*
                 * Now we have the combination of mesh and material to add.
                 * Look, what kind of shape this is. What sort of transform matrix do we require?
                 * The regular one, from the transform component and from the instance?
                 * Something artificial, as for the billboard materials?
                 */
                if (!aMaterialEntry.JMaterial.IsBillboardTransform)
                {
                    if (!aMaterialEntry.JMaterial.IsUnscalable)
                    {
                        _appendInstanceNoLock(
                            aMeshEntry, aMaterialEntry, aAnimationsEntry,
                            id.ModelTransform * transform3ToWorld,
                            cAnimationState);
                    }
                    else
                    {
                        _appendInstanceNoLock(
                            aMeshEntry, aMaterialEntry, aAnimationsEntry,
                            _mUnscale * id.ModelTransform * transform3ToWorld,
                            cAnimationState
                            );
                    }
                }
                else
                {
                    _computeInverseBillboardMatrix(aMaterialEntry, aMeshEntry, transform3ToWorld, id, out var m);
                    _appendInstanceNoLock(aMeshEntry, aMaterialEntry, aAnimationsEntry, m, cAnimationState);
                }
            }
        }
    }


    public void RenderStandard(in IThreeD threeD)
    {
        /*
         * We assume that nobody would modify the render batch anyway,
         * so we lock during the entire rendering.
         */
        lock (_lo)
        {
            _renderMaterialBatchesNoLock(threeD, _materialBatches.Values);
        }
    }


    public void RenderTransparent(in IThreeD threeD)
    {
        /*
         * We assume that nobody would modify the render batch anyway,
         * so we lock during the entire rendering.
         */
        lock (_lo)
        {
            if (null != _transparentMaterialList)
            {
                _renderMaterialBatchesNoLock(threeD, _transparentMaterialList);
            }
        }
    }


    /**
     * After we have all of the render batches, we need some computations.
     * That includes sorting transparent renders.
     */
    public void ComputeAfterAppend()
    {
        lock (_lo)
        {
            if (_transparentMaterialBatches != null && _transparentMaterialBatches.Count > 0)
            {
                _transparentMaterialList = new(_transparentMaterialBatches.Values);
                _transparentMaterialList.Sort((b, a) =>
                {
                    float da;
                    float db;
                    if (_camera3.Angle != 0f)
                    {
                        da = -(a.AveragePosition - _v3CameraPos).LengthSquared();
                        db = -(b.AveragePosition - _v3CameraPos).LengthSquared();
                    }
                    else
                    {
                        da = Vector3.Dot(a.AveragePosition - _v3CameraPos, _v3CameraZ);
                        db = Vector3.Dot(b.AveragePosition - _v3CameraPos, _v3CameraZ);
                    }
                    if (da < db)
                    {
                        return -1;
                    }
                    else if (da > db)
                    {
                        return 1;
                    }

                    return 0;
                });

                /*
                 * We sort a maximum of 10 closest materials.
                 */
                int sortMax = 10;
                foreach (var mb in _transparentMaterialList)
                {
                    if (--sortMax < 0)
                    {
                        break;
                    }

                    mb.Sort(_v3CameraPos, _v3CameraZ, _camera3.Angle);
                }
            } 
            else
            {
                _transparentMaterialList = null;
            }
        }
    }

    
    public CameraOutput(
        IScene scene,
        in IThreeD threeD,
        in Matrix4x4 mTransformToWorld,
        in engine.joyce.components.Camera3 camera3,
        FrameStats frameStats)
    {
        _scene = scene;
        TransformToWorld = mTransformToWorld;
        _v3CameraZ = new Vector3(-TransformToWorld.M31, -TransformToWorld.M32, -TransformToWorld.M33);
        _camera3 = camera3;
        _v3CameraPos = mTransformToWorld.Translation;
        _threeD = threeD;
        _frameStats = frameStats;
        _mUnscale = Matrix4x4.CreateScale(1f / camera3.Scale / (camera3.LR.X-camera3.UL.X));
        GetRotation(ref _mInverseCameraRotation, in mTransformToWorld);
    }
}