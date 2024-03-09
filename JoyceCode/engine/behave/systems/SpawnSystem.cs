using System;
using System.Collections.Generic;
using DefaultEcs;
using engine.behave.components;
using engine.joyce.components;
using engine.world;

namespace engine.behave.systems;


[DefaultEcs.System.With(typeof(engine.behave.components.Behavior))]
[DefaultEcs.System.With(typeof(engine.joyce.components.Transform3ToWorld))]
public class SpawnSystem : DefaultEcs.System.AEntitySetSystem<BehaviorStats>
{
    private readonly Engine _engine;

    protected override void Update(BehaviorStats foo, ReadOnlySpan<Entity> entities)
    {
        /*
         * Count and sort the entities per fragment.
         */
        foreach (var entity in entities)
        {
            ref Transform3ToWorld cTransformWorld = ref entity.Get<Transform3ToWorld>();
            ref Behavior cBehavior = ref entity.Get<Behavior>();

            if (null == cBehavior.Provider) continue;

            foo.FindPerBehaviorStats(cBehavior.Provider.GetType())
                .FindPerFragmentStats(Fragment.PosToIndex3(cTransformWorld.Matrix.Translation))
                .Add();
        }
    }


    protected override void PostUpdate(BehaviorStats foo)
    {
    }
    

    protected override void PreUpdate(BehaviorStats foo)
    {
    }
    
    
    public SpawnSystem() 
        : base(I.Get<Engine>().GetEcsWorld())
    {
        _engine = I.Get<Engine>();
    }
}