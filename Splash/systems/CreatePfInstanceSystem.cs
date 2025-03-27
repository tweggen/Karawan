
using DefaultEcs.Resource;
using System;
using System.Linq;
using System.Numerics;
using engine;
using engine.joyce.components;
using static engine.Logger;


namespace Splash.systems;

/**
 * Create platform rendering infos for this entity.
 *
 * This creates a PfInstance component reflecting the instance3 component.
 * Most prominently, it uses the same order and association of materials and meshes.
 */
[DefaultEcs.System.With(typeof(engine.joyce.components.Instance3))]
[DefaultEcs.System.WithEither(new Type[]
{
    typeof(engine.joyce.components.Transform3ToWorld),
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
        Span<DefaultEcs.Entity> copiedEntities = stackalloc DefaultEcs.Entity[entities.Length];
        entities.CopyTo(copiedEntities);
        foreach (var entity in copiedEntities)
        {
            var cInstance3 = entity.Get<engine.joyce.components.Instance3>();
            engine.joyce.InstanceDesc? id = cInstance3.InstanceDesc;
            
            /*
             * The instance might be incompletely loaded yet.
             */
            if (null == id || null == id.Meshes) continue;

            var nMeshes = id.Meshes.Count;
            var nMeshMaterials = id.MeshMaterials.Count;

            if (nMeshes != nMeshMaterials)
            {
                Warning("We have a problem.");
                return;
            }

            /*
             * Create the platform entity. It will be filled by the instance manager.
             */
            entity.Set(new components.PfInstance(id));
        }
    }

    public unsafe CreatePfInstanceSystem()
        : base(I.Get<Engine>().GetEcsWorldNoAssert())
    {
        _engine = I.Get<Engine>();
    }
}