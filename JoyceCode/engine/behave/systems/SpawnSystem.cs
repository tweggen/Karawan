using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using DefaultEcs;
using engine.behave.components;
using engine.joyce;
using engine.joyce.components;
using engine.world;

namespace engine.behave.systems;


/**
 * This is the system to apply mass manipulation to behaviors.
 *
 * - it counts every behavior that has a spawn operator attached.
 * - if there should be characters killed in a fragment, it creates a list of possible
 *   victims (based on type and the fact they are behind the camera). It does, however
 *   not trigger killing the characters.
 */
[DefaultEcs.System.With(typeof(engine.behave.components.Behavior))]
[DefaultEcs.System.With(typeof(engine.joyce.components.Transform3ToWorld))]
public class SpawnSystem : DefaultEcs.System.AEntitySetSystem<BehaviorStats>
{
    private readonly Engine _engine;
    private CameraInfo _cameraInfo;
    

    protected override void Update(BehaviorStats behaviorStats, ReadOnlySpan<Entity> entities)
    {
        /*
         * Count and sort the entities per fragment.
         */
        foreach (var entity in entities)
        {
            ref Transform3ToWorld cTransformWorld = ref entity.Get<Transform3ToWorld>();
            ref Behavior cBehavior = ref entity.Get<Behavior>();
            if (cBehavior.MaxDistance < 0f) continue;
            if (null == cBehavior.Provider) continue;

            Index3 idxEntity = Fragment.PosToIndex3(cTransformWorld.Matrix.Translation);
            PerBehaviorStats? perBehaviorStats = behaviorStats.GetPerBehaviorStats(cBehavior.Provider.GetType());
            
            /*
             * Only count behaviors that are tracked by spawn operators.
             */
            if (perBehaviorStats != null)
            {
                PerFragmentStats perFragmentStats = perBehaviorStats.FindPerFragmentStats(idxEntity);

                /*
                 * If we might need to kill some in this fragment, track the list of entities of that kind
                 * in a list to decide whom we should kill. Don't consider anything in front of the camera.
                 * (in a wild guess, we set the opening angle to 90 degrees
                 */
                if (perFragmentStats.ToKill > 0)
                {
                    SpawnInfo si = new()
                    {
                        Position = cTransformWorld.Matrix.Translation,
                        CBehavior = cBehavior,
                        Entity = entity
                    };
                    if (_cameraInfo.IsValid)
                    {
                        if (Vector3.Dot(_cameraInfo.Front, si.Position - _cameraInfo.Position) <= 0.7f)
                        {
                            perFragmentStats.NeedVictims().Add(si);
                        }
                    }
                }
                perFragmentStats.Add();
            }
            
        }
    }


    protected override void PostUpdate(BehaviorStats foo)
    {
    }
    

    protected override void PreUpdate(BehaviorStats foo)
    {
        _cameraInfo = _engine.CameraInfo;
    }
    
    
    public SpawnSystem() 
        : base(I.Get<Engine>().GetEcsWorld())
    {
        _engine = I.Get<Engine>();
    }
}