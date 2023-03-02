using Karawan.platform.cs1.splash.systems;
using Karawan.platform.cs1.splash;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using BepuUtilities;

namespace Karawan.platform.cs1.splash
{
    public class CameraOutput
    {
        private uint _cameraMask = 0;
        public uint CameraMask { get => _cameraMask; }

        private int _nEntities = 0;
        private int _nMeshes = 0;
        private int _nMaterials = 0;
        private int _nInstances = 0;

        private readonly Dictionary<RlMaterialEntry, MaterialBatch> _materialBatches = new();
        private readonly Dictionary<RlMaterialEntry, MaterialBatch> _transparentMaterialBatches = new();

        private readonly MaterialManager _materialManager;
        private readonly MeshManager _meshManager;

        /**
         * The actual rendering method. Must be called from the context of
         * the render thread class.
         * 
         * Uploaders meshes/textures if required.
         */
        private void _renderMaterialBatches(in Dictionary<RlMaterialEntry, MaterialBatch> mb)
        {
            Stopwatch swUpload = new();
            foreach (var materialItem in mb)
            {
                bool haveMaterial = materialItem.Value.RlMaterialEntry.HasRlMaterial();
                if (!haveMaterial && swUpload.Elapsed.TotalMilliseconds < 5f)
                {
                    swUpload.Start();
                    _materialManager.FillRlMaterialEntry(materialItem.Value.RlMaterialEntry);
                    haveMaterial = true;
                    swUpload.Stop();
                }

                if (!haveMaterial)
                {
                    continue;
                }
                foreach (var meshItem in materialItem.Value.MeshBatches)
                {
                    bool haveMesh = meshItem.Value.RlMeshEntry.IsMeshUploaded(); 
                    if (!haveMesh && swUpload.Elapsed.TotalMilliseconds < 5f)
                    {
                        swUpload.Start();
                        _meshManager.FillRlMeshEntry(meshItem.Value.RlMeshEntry);
                        haveMesh = true;
                        swUpload.Stop();
                    }

                    if (!haveMesh)
                    {
                        continue;
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
                    Raylib_CsLo.Raylib.DrawMeshInstanced(
                            meshItem.Key.RlMesh,
                            materialItem.Key.RlMaterial,
                            spanMatrices,
                            nMatrices
                    );
                }
            }
        }

        public void AppendInstance(
            in RlMeshEntry rlMeshEntry,
            in RlMaterialEntry rlMaterialEntry,
            in Matrix4x4 matrix)
        {
            _nEntities++;

            /*
             * Do we have an entry for the material?
             */
            Dictionary<RlMaterialEntry, MaterialBatch> mbs;
            if (!rlMaterialEntry.HasTransparency())
            {
                mbs = _materialBatches;
            }
            else
            {
                mbs = _transparentMaterialBatches;
            }
            MaterialBatch materialBatch;
            mbs.TryGetValue(rlMaterialEntry, out materialBatch);
            if (null == materialBatch)
            {
                materialBatch = new MaterialBatch(rlMaterialEntry);
                mbs[rlMaterialEntry] = materialBatch;
                _nMaterials++;
            }

            /*
             * And do we have an entry for the mesh in the material?
             */
            MeshBatch meshBatch;
            materialBatch.MeshBatches.TryGetValue(rlMeshEntry, out meshBatch);
            if (null == meshBatch)
            {
                meshBatch = new MeshBatch(rlMeshEntry);
                materialBatch.MeshBatches[rlMeshEntry] = meshBatch;
                _nMeshes++;
            }

            /*
             * Now we can add our matrix to the list of matrices.
             */
            meshBatch.Matrices.Add(Matrix4x4.Transpose(matrix));
            _nInstances++;
        }


        public void RenderStandard()
        {
            _renderMaterialBatches(_materialBatches);
        }


        public void RenderTransparent()
        {
            _renderMaterialBatches(_transparentMaterialBatches);
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
