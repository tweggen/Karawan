using System;
using System.Collections.Generic;
using DefaultEcs;
using System.Numerics;
using engine.world.components;

namespace engine.behave.systems;


[DefaultEcs.System.With(typeof(components.Behavior))]
[DefaultEcs.System.With(typeof(joyce.components.Transform3ToWorld))]
[DefaultEcs.System.With(typeof(Owner))]
internal class WiperSystem : DefaultEcs.System.AEntitySetSystem<engine.geom.AABB>
{
    private engine.Engine _engine;
    private 
        List<Entity> _listToWipe = null;

    protected override void Update(
        engine.geom.AABB aabb, ReadOnlySpan<DefaultEcs.Entity> entities)
    {
        /*
         * We'll eventually remove entities.
         */
        Span<Entity> copy = stackalloc Entity[entities.Length];
        entities.CopyTo(copy);
        foreach (var entity in copy)
        {
            Vector3 pos = entity.Get<joyce.components.Transform3ToWorld>().Matrix.Translation;
            if (aabb.Contains(pos))
            {
                /*
                 * Keep entity.
                 */
            }
            else
            {
                entity.Disable();
                _listToWipe.Add(entity);
            }
        }
    }


    protected override void PostUpdate(engine.geom.AABB aabb)
    {
        _engine.AddDoomedEntities(_listToWipe);
        _listToWipe = null;
    }
    

    protected override void PreUpdate(engine.geom.AABB aabb)
    {
        _listToWipe = new();
    }


    public WiperSystem()
        : base(I.Get<Engine>().GetEcsWorldAnyThread())
    {
        _engine = I.Get<Engine>();
    }
}
