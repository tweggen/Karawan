using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using DefaultEcs;
using engine.behave.components;
using engine.joyce;
using engine.joyce.components;
using engine.world;
using static engine.Logger;

namespace engine.behave.systems;


/**
 * This is the system to apply mass manipulation to behaviors.
 *
 * - it counts every behavior that has a spawn operator attached.
 * - it triggers deletion for characters it finds
 *   - using a comparator function provided by the operator,
 *   - using a deletion function provided by the operator.
 */
[DefaultEcs.System.With(typeof(engine.behave.components.Behavior))]
[DefaultEcs.System.With(typeof(engine.joyce.components.Transform3ToWorld))]
public class SpawnSystem : DefaultEcs.System.AEntitySetSystem<BehaviorStats>
{
    private readonly Engine _engine;

    protected override void Update(BehaviorStats behaviorStats, ReadOnlySpan<Entity> entities)
    {
        /*
         * Count and sort the entities per fragment.
         */
        foreach (var entity in entities)
        {
            ref Transform3ToWorld cTransformWorld = ref entity.Get<Transform3ToWorld>();
            ref Behavior cBehavior = ref entity.Get<Behavior>();

            if (null == cBehavior.Provider) continue;

            Index3 idxEntity = Fragment.PosToIndex3(cTransformWorld.Matrix.Translation);
            PerBehaviorStats? perBehaviorStats = behaviorStats.GetPerBehaviorStats(cBehavior.Provider.GetType());
            
            /*
             * Only count behaviors that are tracked by spawn operators.
             */
            if (perBehaviorStats != null)
            {
                PerFragmentStats perFragmentStats = perBehaviorStats.FindPerFragmentStats(idxEntity);
                perFragmentStats.Add();
            }
            
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