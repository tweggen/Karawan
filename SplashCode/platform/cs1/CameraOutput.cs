using Karawan.platform.cs1.splash.systems;
using Karawan.platform.cs1.splash;
using System;
using System.Collections.Generic;
using System.Text;
using System.Numerics;
using System.Runtime.InteropServices;
using BepuUtilities;

namespace Karawan.platform.cs1
{

    public class CameraOutput
    {
        public uint CameraMask = 0;

        public int NEntities = 0;
        public int NMeshes = 0;
        public int NMaterials = 0;

        public Dictionary<RlMaterialEntry, MaterialBatch> MaterialBatches;
        public Dictionary<RlMaterialEntry, MaterialBatch> TransparentMaterialBatches;

        private void _renderMaterialBatches(in Dictionary<RlMaterialEntry, MaterialBatch> mb)
        {
            foreach (var materialItem in mb)
            {
                foreach (var meshItem in materialItem.Value.MeshBatches)
                {
                    var nMatrices = meshItem.Value.Matrices.Count;
                    /*
                     * I must draw using the instanced call because I only use an instanced shader.
                     */
                    // var arrayMatrices = meshItem.Value.Matrices.ToArray();
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
            NEntities++;

            /*
             * Do we have an entry for the material?
             */
            Dictionary<RlMaterialEntry, MaterialBatch> mbs;
            if (!rlMaterialEntry.HasTransparency)
            {
                mbs = MaterialBatches;
            }
            else
            {
                mbs = TransparentMaterialBatches;
            }
            MaterialBatch materialBatch;
            mbs.TryGetValue(rlMaterialEntry, out materialBatch);
            if (null == materialBatch)
            {
                materialBatch = new MaterialBatch();
                mbs[rlMaterialEntry] = materialBatch;
                NMaterials++;
            }

            /*
             * And do we have an entry for the mesh in the material?
             */
            MeshBatch meshBatch;
            materialBatch.MeshBatches.TryGetValue(rlMeshEntry, out meshBatch);
            if (null == meshBatch)
            {
                meshBatch = new MeshBatch();
                materialBatch.MeshBatches[rlMeshEntry] = meshBatch;
                NMeshes++;
            }

            /*
             * Now we can add our matrix to the list of matrices.
             */
            meshBatch.Matrices.Add(Matrix4x4.Transpose(matrix));
        }


        public void RenderStandard()
        {
            if (null != MaterialBatches)
            {
                _renderMaterialBatches(MaterialBatches);
            }
        }


        public void RenderTransparent()
        {
            if (null != TransparentMaterialBatches)
            {
                _renderMaterialBatches(TransparentMaterialBatches);
            }
        }


        public string GetDebugInfo()
        {
            return $"BatchCollector: {NEntities} entities, {NMaterials} materials, {NMeshes} meshes, 1 shaders.";
        }


        public CameraOutput(in uint cameraMask)
        {
            CameraMask = cameraMask;
            MaterialBatches = new();
            TransparentMaterialBatches = new();
        }
    }

}
