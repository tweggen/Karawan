using System;
using System.Numerics;
using System.Collections;
using System.Collections.Generic;


namespace Karawan.platform.cs1.splash.systems
{
    class MeshBatch
    {
        // public Raylib_CsLo.Mesh Mesh;
        public ArrayList Matrices;

        public MeshBatch()
        {
            Matrices = new();
        }
    }

    class MaterialBatch
    {
        // public Raylib_CsLo.Material Material;
        public Dictionary<Raylib_CsLo.Mesh, MeshBatch> MeshBatches;

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

        private Dictionary<Raylib_CsLo.Material, MaterialBatch> _materialBatches;


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
                    for( int i=0; i< nMatrices; ++i )
                    {
                        Raylib_CsLo.Raylib.DrawMesh(
                            meshItem.Key,
                            materialItem.Key,
                            (Matrix4x4) meshItem.Value.Matrices[i]
                        );
                    }
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
                    var rMatrix = Matrix4x4.Transpose(transform3ToWorld.Matrix);

                    /*
                     * Do we have an entry for the material?
                     */
                    MaterialBatch materialBatch;
                    _materialBatches.TryGetValue( cMesh.Material, out materialBatch );
                    if (null == materialBatch)
                    {
                        materialBatch = new MaterialBatch();
                        _materialBatches[cMesh.Material] = materialBatch;
                    }

                    /*
                     * And do we have an entry for the mesh in the material?
                     */
                    MeshBatch meshBatch;
                    materialBatch.MeshBatches.TryGetValue( cMesh.Mesh, out meshBatch );
                    if (null == meshBatch)
                    {
                        meshBatch = new MeshBatch();
                        materialBatch.MeshBatches[cMesh.Mesh] = meshBatch;
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

        public DrawRlMeshesSystem(engine.Engine engine)
            : base(engine.GetEcsWorld())
        {
            _engine = engine;
        }
    }
}
