using System;
using System.Numerics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Karawan.platform.cs1.splash.systems
{
    class MeshBatch
    {
        // public Raylib_CsLo.Mesh Mesh;
        public List<Matrix4x4> Matrices;

        public MeshBatch()
        {
            Matrices = new();
        }
    }

    class MaterialBatch
    {
        // public Raylib_CsLo.Material Material;
        public Dictionary<RlMeshEntry, MeshBatch> MeshBatches;

        public MaterialBatch()
        {
            MeshBatches = new();
        }
    }


    [DefaultEcs.System.With(typeof(engine.transform.components.Transform3ToWorld))]
    [DefaultEcs.System.With(typeof(splash.components.RlMesh))]
    [DefaultEcs.System.With(typeof(splash.components.RlMaterial))]
    /**
     * Render the raylib meshes.
     * 
     * Groups by material and mesh.
     */
    sealed class DrawRlMeshesSystem : DefaultEcs.System.AEntitySetSystem<uint>
    {
        private engine.Engine _engine;
        private MaterialManager _materialManager;

        private Dictionary<RlMaterialEntry, MaterialBatch> _materialBatches;

        private void _renderBatches()
        {
            if( null==_materialBatches )
            {
                return;
            }
            foreach( var materialItem in _materialBatches )
            {
                foreach (var meshItem in materialItem.Value.MeshBatches)
                {
                    var nMatrices = meshItem.Value.Matrices.Count;
#if true
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
#else
                    for ( int i=0; i< nMatrices; ++i )
                    {
                        Raylib_CsLo.Raylib.DrawMesh(
                            meshItem.Key.RlMesh,
                            materialItem.Key.RlMaterial,
                            (Matrix4x4) meshItem.Value.Matrices[i]
                        );
                    }
#endif
                }
            }
        }

        private void _appendMeshRenderList(in ReadOnlySpan<DefaultEcs.Entity> entities, uint cameraMask)
        {
            foreach (var entity in entities)
            {
                var transform3ToWorld = entity.Get<engine.transform.components.Transform3ToWorld>();
                if (0 != (transform3ToWorld.CameraMask & cameraMask))
                {
                    var rlMeshEntry = entity.Get<splash.components.RlMesh>().MeshEntry;
                    var rlMaterialEntry = entity.Get<splash.components.RlMaterial>().MaterialEntry;

                    // Skip things that incompletely are loaded.
                    if( null==rlMeshEntry) {
                        continue;
                    }
                    if( null==rlMaterialEntry )
                    {
                        rlMaterialEntry = _materialManager.GetUnloadedMaterial();
                    }

                    var rMatrix = Matrix4x4.Transpose(transform3ToWorld.Matrix);

                    /*
                     * Do we have an entry for the material?
                     */
                    MaterialBatch materialBatch;
                    _materialBatches.TryGetValue( rlMaterialEntry, out materialBatch );
                    if (null == materialBatch)
                    {
                        materialBatch = new MaterialBatch();
                        _materialBatches[rlMaterialEntry] = materialBatch;
                    }

                    /*
                     * And do we have an entry for the mesh in the material?
                     */
                    MeshBatch meshBatch;
                    materialBatch.MeshBatches.TryGetValue( rlMeshEntry, out meshBatch );
                    if (null == meshBatch)
                    {
                        meshBatch = new MeshBatch();
                        materialBatch.MeshBatches[rlMeshEntry] = meshBatch;
                    }

                    /*
                     * Now we can add our matrix to the list of matrices.
                     */
                    meshBatch.Matrices.Add(rMatrix);
                }
            }
        }


        protected override void PreUpdate(uint cameraMask)
        {
        }

        protected override void PostUpdate(uint cameraMask)
        {
            _renderBatches();
            /*
             * Null out the references after rendering.
             */
            _materialBatches = new();
        }

        protected override void Update(uint cameraMask, ReadOnlySpan<DefaultEcs.Entity> entities)
        {
            _appendMeshRenderList(entities, cameraMask);
        }

        public DrawRlMeshesSystem(
            engine.Engine engine,
            MaterialManager materialManager
        )
            : base(engine.GetEcsWorld())
        {
            _engine = engine;
            _materialManager = materialManager;
            _materialBatches = new();
        }
    }
}
