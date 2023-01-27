using System;
using System.Numerics;
using System.Collections;
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
    /**
     * Render the raylib meshes.
     * 
     * Groups by material and mesh.
     */
    sealed class DrawRlMeshesSystem : DefaultEcs.System.AEntitySetSystem<engine.Engine>
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

        private void _appendMeshRenderList(in ReadOnlySpan<DefaultEcs.Entity> entities)
        {
            foreach (var entity in entities)
            {
                var transform3ToWorld = entity.Get<engine.transform.components.Transform3ToWorld>();
                if (transform3ToWorld.IsTotalVisible)
                {
                    var cMesh = entity.Get<splash.components.RlMesh>();
                    RlMaterialEntry materialEntry;

                    // Skip things that incompletely are loaded.
                    if( null==cMesh.MeshEntry ) {
                        continue;
                    }
                    if( null==cMesh.MaterialEntry )
                    {
                        materialEntry = _materialManager.GetUnloadedMaterial();
                    } else
                    {
                        materialEntry = cMesh.MaterialEntry;
                    }

                    var rMatrix = Matrix4x4.Transpose(transform3ToWorld.Matrix);

                    /*
                     * Do we have an entry for the material?
                     */
                    MaterialBatch materialBatch;
                    _materialBatches.TryGetValue( materialEntry, out materialBatch );
                    if (null == materialBatch)
                    {
                        materialBatch = new MaterialBatch();
                        _materialBatches[materialEntry] = materialBatch;
                    }

                    /*
                     * And do we have an entry for the mesh in the material?
                     */
                    MeshBatch meshBatch;
                    materialBatch.MeshBatches.TryGetValue( cMesh.MeshEntry, out meshBatch );
                    if (null == meshBatch)
                    {
                        meshBatch = new MeshBatch();
                        materialBatch.MeshBatches[cMesh.MeshEntry] = meshBatch;
                    }

                    /*
                     * Now we can add our matrix to the list of matrices.
                     */
                    meshBatch.Matrices.Add(rMatrix);
                }
            }
        }


        protected override void PreUpdate(engine.Engine state)
        {
            // TXWTODO: Totally inefficient to compute this every time. Or is it?
            _materialBatches = new();
        }

        protected override void PostUpdate(engine.Engine state)
        {
            _renderBatches();
        }

        protected override void Update(engine.Engine state, ReadOnlySpan<DefaultEcs.Entity> entities)
        {
            _appendMeshRenderList(entities);
        }

        public DrawRlMeshesSystem(
            engine.Engine engine,
            MaterialManager materialManager
        )
            : base(engine.GetEcsWorld())
        {
            _engine = engine;
            _materialManager = materialManager;
        }
    }
}
