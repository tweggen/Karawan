
using System;


namespace Karawan.platform.cs1.splash.systems
{
    [DefaultEcs.System.With(typeof(engine.joyce.components.Instance3))]
    [DefaultEcs.System.With(typeof(engine.transform.components.Transform3ToWorld))]
    [DefaultEcs.System.Without(typeof(splash.components.RlMesh))]

    /**
     * Create a raylib mesh for every mesh defined.
     * Totally unoptimized.
     */
    sealed class CreateRlMeshesSystem : DefaultEcs.System.AEntitySetSystem<engine.Engine>
    {
        private engine.Engine _engine;

        private MeshManager _meshManager;
        private MaterialManager _materialManager;

        protected override void PreUpdate(engine.Engine state)
        {
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
                    Console.WriteLine("We have a problem.");
                    return;
                }
                var nMaterials = cInstance3.Materials.Count;

                for(var i=0; i<nMeshes; ++i)
                {
                    var jMesh = (engine.joyce.Mesh) cInstance3.Meshes[i];
                    var materialIndex = (int) cInstance3.MeshMaterials[i];
                    var jMaterial = (engine.joyce.Material) cInstance3.Materials[materialIndex];

                    var rlMeshEntry =_meshManager.FindRlMesh(jMesh);
                    var rlMaterialEntry = _materialManager.FindRlMaterial(jMaterial);

                    entity.Set<splash.components.RlMesh>(
                        new splash.components.RlMesh(rlMeshEntry, rlMaterialEntry));
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