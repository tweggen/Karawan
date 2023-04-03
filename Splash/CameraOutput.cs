using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using static engine.Logger;

namespace Splash
{
    public class CameraOutput
    {
        private uint _cameraMask = 0;
        public uint CameraMask { get => _cameraMask; }

        private int _nEntities = 0;
        private int _nMeshes = 0;
        private int _nMaterials = 0;
        private int _nInstances = 0;

        private readonly Dictionary<AMaterialEntry, MaterialBatch> _materialBatches = new();
        private readonly Dictionary<AMaterialEntry, MaterialBatch> _transparentMaterialBatches = new();

        private readonly MaterialManager _materialManager;
        private readonly MeshManager _meshManager;

        /**
         * The actual rendering method. Must be called from the context of
         * the render thread class.
         * 
         * Uploaders meshes/textures if required.
         */
        private void _renderMaterialBatches(in IThreeD threeD, in Dictionary<AMaterialEntry, MaterialBatch> mb)
        {
            Stopwatch swUpload = new();
            int nSkippedMaterials = 0;
            int nSkippedMeshes = 0;
            foreach (var materialItem in mb)
            {
                var aMaterialEntry = materialItem.Value.AMaterialEntry;
                var jMaterial = aMaterialEntry.JMaterial; 
                bool haveMaterial = aMaterialEntry.IsUploaded();
                if (!haveMaterial)
                {
                    if (jMaterial.UploadImmediately || swUpload.Elapsed.TotalMilliseconds < 1f) {
                        swUpload.Start();
                        _materialManager.FillMaterialEntry(aMaterialEntry);
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
                    bool haveMesh = aMeshEntry.IsMeshUploaded();
                    if (!haveMesh)
                    {
                        if (jMesh.UploadImmediately || swUpload.Elapsed.TotalMilliseconds < 1f)
                        {
                            swUpload.Start();
                            _meshManager.FillMeshEntry(meshItem.Value.AMeshEntry);
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

        public void AppendInstance(
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
            materialBatch.MeshBatches.TryGetValue(aMeshEntry, out meshBatch);
            if (null == meshBatch)
            {
                meshBatch = new MeshBatch(aMeshEntry);
                materialBatch.MeshBatches[aMeshEntry] = meshBatch;
                _nMeshes++;
            }

            /*
             * Now we can add our matrix to the list of matrices.
             */
            // meshBatch.Matrices.Add(Matrix4x4.Transpose(matrix) );
            meshBatch.Matrices.Add(matrix);
            _nInstances++;
        }


        public void RenderStandard(in IThreeD threeD)
        {
            _renderMaterialBatches(threeD, _materialBatches);
        }


        public void RenderTransparent(in IThreeD threeD)
        {
            _renderMaterialBatches(threeD, _transparentMaterialBatches);
        }


        public string GetDebugInfo()
        {
            return $"BatchCollector: {_nEntities} entities, {_nMaterials} materials, {_nMeshes} meshes, {_nInstances} instances, 1 shaders.";
        }


        public CameraOutput(
            in MaterialManager materialManager, 
            in MeshManager meshManager,
            in uint cameraMask)
        {
            _cameraMask = cameraMask;

            _materialManager = materialManager;
            _meshManager = meshManager;
        }
    }

}
