
using DefaultEcs.Resource;
using System;
using System.Linq;
using static engine.Logger;


namespace Splash.systems
{
    /**
     * Create platform rendering infos for this entity.
     *
     * This creates a PfInstance component reflecting the instance3 component.
     * Most prominently, it uses the same order and association of materials and meshes.
     */
    [DefaultEcs.System.With(typeof(engine.joyce.components.Instance3))]
    [DefaultEcs.System.WithEither(new Type[] {
        typeof(engine.transform.components.Transform3ToWorld),
        typeof(engine.joyce.components.Skybox)
    })]
    [DefaultEcs.System.Without(typeof(Splash.components.PfInstance))]

    sealed class CreatePfInstanceSystem : DefaultEcs.System.AEntitySetSystem<engine.Engine>
    {
        private engine.Engine _engine;

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

                /*
                 * Create the platform entity. It will be filled by the instance manager.
                 */
                entity.Set(new components.PfInstance(
                    cInstance3.ModelTransform,
                    cInstance3.Meshes,
                    cInstance3.MeshMaterials,
                    cInstance3.Materials,
                    cInstance3.MeshProperties));
            }
        }

        public unsafe CreatePfInstanceSystem(
            engine.Engine engine
        )
            : base( engine.GetEcsWorld() )
        {
            _engine = engine;
        }
    }
}
;