using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using static engine.Logger;

namespace Splash
{
    public class CameraOutput
    {
        private object _lo = new();
        
        private uint _cameraMask = 0;
        public uint CameraMask { get => _cameraMask; }

        private int _nEntities = 0;
        private int _nMeshes = 0;
        private int _nMaterials = 0;
        private int _nInstances = 0;

        private Dictionary<AMaterialEntry, MaterialBatch> _materialBatches = new();
        private Dictionary<AMaterialEntry, MaterialBatch> _transparentMaterialBatches = new();

        private readonly IThreeD _threeD;
        
        private readonly InstanceManager _instanceManager;

        /**
         * The actual rendering method. Must be called from the context of
         * the render thread class. The material batch must not be used concurrently. 
         * 
         * Uploaders meshes/textures if required.
         */
        private void _renderMaterialBatchesNoLock(in IThreeD threeD, in Dictionary<AMaterialEntry, MaterialBatch> mb)
        {
            Stopwatch swUpload = new();
            int nSkippedMaterials = 0;
            int nSkippedMeshes = 0;
            foreach (var materialItem in mb)
            {
                var aMaterialEntry = materialItem.Value.AMaterialEntry;
                var jMaterial = aMaterialEntry.JMaterial; 
                bool needMaterial = (!aMaterialEntry.IsUploaded()) || aMaterialEntry.IsOutdated();
                if (needMaterial)
                {
                    if (jMaterial.UploadImmediately || swUpload.Elapsed.TotalMilliseconds < 1f) {
                        swUpload.Start();
                        _threeD.FillMaterialEntry(aMaterialEntry);
                        swUpload.Stop();
                    }
                    else
                    {
                        ++nSkippedMaterials;
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
                        if (jMesh.UploadImmediately || swUpload.Elapsed.TotalMilliseconds < 1f)
                        {
                            swUpload.Start();
                            _threeD.UploadMesh(meshItem.Value.AMeshEntry);
                            swUpload.Stop();
                        }
                        else
                        {
                            ++nSkippedMeshes;
                            continue;
                        }
                    }

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
            }

            if (0 < nSkippedMaterials || 0 < nSkippedMeshes)
            {
                Trace($"Needed to skip material uploads: {nSkippedMaterials}, skpped mesh uploads: {nSkippedMeshes}");
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
        

        public void AppendInstance(in Splash.components.PfInstance pfInstance, in Matrix4x4 matrix)
        {
            lock (_lo)
            {
                int nMeshes = pfInstance.AMeshEntries.Count;
                int nMaterialIndices = pfInstance.MeshMaterials.Count;
                int nMaterials = pfInstance.AMaterialEntries.Count;
                for (int i = 0; i < nMeshes; ++i)
                {
                    AMeshEntry aMeshEntry = pfInstance.AMeshEntries[i];
                    AMaterialEntry aMaterialEntry = null;

                    if (i < nMaterialIndices)
                    {
                        int materialIndex = pfInstance.MeshMaterials[i];
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


        public string GetDebugInfo()
        {
            lock (_lo)
            {
                return
                    $"BatchCollector: {_nEntities} entities, {_nMaterials} materials, {_nMeshes} meshes, {_nInstances} instances, 1 shaders.";
            }
        }


        public CameraOutput(
            in IThreeD threeD,
            in uint cameraMask)
        {
            _cameraMask = cameraMask;
            _threeD = threeD;
        }
    }

}
