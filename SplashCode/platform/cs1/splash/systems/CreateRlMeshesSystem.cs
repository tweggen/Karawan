
using DefaultEcs.Resource;
using System;
using static engine.Logger;


namespace Karawan.platform.cs1.splash.systems
{
    [DefaultEcs.System.With(typeof(engine.joyce.components.Instance3))]
    [DefaultEcs.System.WithEither(new Type[] {
        typeof(engine.transform.components.Transform3ToWorld),
        typeof(engine.joyce.components.Skybox)
    })]
    [DefaultEcs.System.Without(typeof(splash.components.PfMesh))]

    /**
     * Create a raylib mesh for every mesh defined.
     * Totally unoptimized.
     */
    sealed class CreateRlMeshesSystem : DefaultEcs.System.AEntitySetSystem<engine.Engine>
    {
        private engine.Engine _engine;

        private MeshManager _meshManager;
        private MaterialManager _materialManager;

        private int _runNumber = 0;

        protected override void PreUpdate(engine.Engine state)
        {
            ++_runNumber;
        }

        protected override void PostUpdate(engine.Engine state)
        {
        }

        protected override void Update(engine.Engine state, ReadOnlySpan<DefaultEcs.Entity> entities)
        {
            foreach (var entity in entities)
            {
                // TXWTODO: Only consider visible Instace3 things.

                var cInstance3 = entity.Get<engine.joyce.components.Instance3>();
                
                var nMeshes = cInstance3.Meshes.Count;
                var nMeshMaterials = cInstance3.MeshMaterials.Count;
                
                if( nMeshes!=nMeshMaterials)
                {
                    Warning("We have a problem.");
                    return;
                }
                var nMaterials = cInstance3.Materials.Count;

                for(var i=0; i<nMeshes; ++i)
                {
                    var jMesh = (engine.joyce.Mesh) cInstance3.Meshes[i];
                    // Trace($"run {_runNumber}: Creating platform mesh from jMesh with {jMesh.Vertices.Count} vertices.");
                    var materialIndex = (int) cInstance3.MeshMaterials[i];
                    var jMaterial = (engine.joyce.Material) cInstance3.Materials[materialIndex];

                    entity.Set(new ManagedResource<engine.joyce.Mesh, AMeshEntry>(jMesh));
                    entity.Set(new ManagedResource<engine.joyce.Material, AMaterialEntry>(jMaterial));
                }
            }
        }

        public unsafe CreateRlMeshesSystem(
            engine.Engine engine,
            MeshManager meshManager,
            MaterialManager materialManager
        )
            : base( engine.GetEcsWorld() )
        {
            _engine = engine;
            _meshManager = meshManager;
            _materialManager = materialManager;
        }
    }
}
;