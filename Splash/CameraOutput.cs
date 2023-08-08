using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using engine;
using static engine.Logger;

namespace Splash
{
    public class CameraOutput
    {
        private object _lo = new();
        
        private engine.joyce.components.Camera3 _camera3;
        public uint CameraMask { get => _camera3.CameraMask; }

        public engine.joyce.components.Camera3 Camera3
        {
            get => _camera3;
        }

        public Matrix4x4 TransformToWorld;


        private int _nEntities = 0;
        private int _nMeshes = 0;
        private int _nMaterials = 0;
        private int _nInstances = 0;
        private int _nSkippedMeshes = 0;
        private int _nSkippedMaterials = 0;
        
        private Dictionary<AMaterialEntry, MaterialBatch> _materialBatches = new();
        private Dictionary<AMaterialEntry, MaterialBatch> _transparentMaterialBatches = new();

        private readonly IThreeD _threeD;
        
        private readonly InstanceManager _instanceManager;

        private float _defaultUploadMaterialPerFrame = 1f;
        private float _defaultUploadMeshPerFrame = 1f;

        private IScene _scene;
        
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
        private void _renderMaterialBatchesNoLock(in IThreeD threeD, in Dictionary<AMaterialEntry, MaterialBatch> mb)
        {
            Stopwatch swUpload = new();
            bool preloadOnly = (_camera3.CameraFlags & engine.joyce.components.Camera3.Flags.PreloadOnly) != 0;

            var msUploadMaterialPerFrame = _msUploadMaterialPerFrame(_scene);
            var msUploadMeshPerFrame = _msUploadMeshPerFrame(_scene);
            
            foreach (var materialItem in mb)
            {
                var aMaterialEntry = materialItem.Value.AMaterialEntry;
                var jMaterial = aMaterialEntry.JMaterial; 
                bool needMaterial = (!aMaterialEntry.IsUploaded()) || aMaterialEntry.IsOutdated();
                if (needMaterial)
                {
                    if (jMaterial.UploadImmediately || swUpload.Elapsed.TotalMilliseconds < msUploadMaterialPerFrame) {
                        swUpload.Start();
                        _threeD.FillMaterialEntry(aMaterialEntry);
                        swUpload.Stop();
                    }
                    else
                    {
                        ++_nSkippedMaterials;
                        continue;
                    } 
                }

                foreach (var meshItem in materialItem.Value.MeshBatches)
                {
                    var aMeshEntry = meshItem.Value.AMeshEntry;
                    var jMesh = aMeshEntry.JMesh;
                    bool haveMesh = aMeshEntry.IsUploaded();
                    if (!haveMesh)
                    {
                        if (jMesh.UploadImmediately || swUpload.Elapsed.TotalMilliseconds < msUploadMeshPerFrame)
                        {
                            swUpload.Start();
                            _threeD.UploadMesh(meshItem.Value.AMeshEntry);
                            swUpload.Stop();
                        }
                        else
                        {
                            ++_nSkippedMeshes;
                            continue;
                        }
                    }

                    if (!preloadOnly)
                    {
                        var nMatrices = meshItem.Value.Matrices.Count;
                        /*
                         * I must draw using the instanced call because I only use an instanced shader.
                         */
#if NET6_0_OR_GREATER
                        var spanMatrices = CollectionsMarshal.AsSpan<Matrix4x4>(meshItem.Value.Matrices);
#else
                        Span<Matrix4x4> spanMatrices = meshItem.Value.Matrices.ToArray();
#endif
                        threeD.DrawMeshInstanced(meshItem.Key, materialItem.Key, spanMatrices, nMatrices);
                    }
                    else
                    {
                        threeD.FinishUploadOnly(meshItem.Key);

                    }
                }
            }
        }


        private void _appendInstanceNoLock(
            in AMeshEntry aMeshEntry,
            in AMaterialEntry aMaterialEntry,
            in Matrix4x4 matrix)
        {
            _nEntities++;

            /*
             * Do we have an entry for the material?
             */
            Dictionary<AMaterialEntry, MaterialBatch> mbs;
                if (!aMaterialEntry.HasTransparency())
            {
                mbs = _materialBatches;
            }
            else
            {
                mbs = _transparentMaterialBatches;
            }

            MaterialBatch materialBatch;
            mbs.TryGetValue(aMaterialEntry, out materialBatch);
            if (null == materialBatch)
            {
                materialBatch = new MaterialBatch(aMaterialEntry);
                mbs[aMaterialEntry] = materialBatch;
                _nMaterials++;
            }

            /*
             * And do we have an entry for the mesh in the material?
             */
            MeshBatch meshBatch;
            materialBatch.MeshBatches.TryGetValue(aMeshEntry, out meshBatch); // TXWTODO: Hotspot! Optimize!
            if (null == meshBatch)
            {
                meshBatch = new MeshBatch(aMeshEntry);
                materialBatch.MeshBatches[aMeshEntry] = meshBatch;
                _nMeshes++;
            }

            /*
             * Now we can add our matrix to the list of matrices.
             */
            meshBatch.Matrices.Add(matrix);
            _nInstances++;
        }


#if false
        public void AppendInstance(
            in AMeshEntry aMeshEntry,
            in AMaterialEntry aMaterialEntry,
            in Matrix4x4 matrix)
        {
            lock (_lo)
            {
                _appendInstanceNoLock(aMeshEntry, aMaterialEntry, matrix);
            }
        }
#endif
        

        public void AppendInstance(in Splash.components.PfInstance pfInstance, Matrix4x4 matrix)
        {
            lock (_lo)
            {
                engine.joyce.InstanceDesc id = pfInstance.InstanceDesc;
                int nMeshes = pfInstance.AMeshEntries.Count;
                int nMaterialIndices = id.MeshMaterials.Count;
                int nMaterials = pfInstance.AMaterialEntries.Count;

                for (int i = 0; i < nMeshes; ++i)
                {
                    AMeshEntry aMeshEntry = pfInstance.AMeshEntries[i];
                    AMaterialEntry aMaterialEntry = null;

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

                    // Skip things that incompletely are loaded.
                    if (null == aMeshEntry)
                    {
                        continue;
                    }

                    if (null == aMaterialEntry)
                    {
                        aMaterialEntry = _threeD.GetDefaultMaterial();
                    }

                    _appendInstanceNoLock(aMeshEntry, aMaterialEntry, pfInstance.ModelTransform * matrix);
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
                _renderMaterialBatchesNoLock(threeD, _materialBatches);
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
                _renderMaterialBatchesNoLock(threeD, _transparentMaterialBatches);
            }
        }


        public void GetRenderStats(out int skipped, out int total)
        {
            lock (_lo)
            {
                skipped = _nSkippedMeshes + _nSkippedMaterials;
                total = _nMeshes + _nMaterials;
            }
        }


        public string GetDebugInfo()
        {
            lock (_lo)
            {
                return
                    $"BatchCollector: {_nEntities} entities, {_nMaterials} materials, {_nMeshes} meshes, {_nInstances} instances, 1 shaders.";
            }
        }


        public CameraOutput(
            IScene scene,
            in IThreeD threeD,
            in Matrix4x4 mTransformToWorld,
            in engine.joyce.components.Camera3 camera3)
        {
            _scene = scene;
            TransformToWorld = mTransformToWorld;
            _camera3 = camera3;
            _threeD = threeD;
        }
    }

}
