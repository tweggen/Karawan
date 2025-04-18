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
    

    /**
     * This system iterates through all living entity by behaviors and checks
     * if they are supposed to be killed according to the spawn rules.
     */
    protected override void Update(BehaviorStats behaviorStats, ReadOnlySpan<Entity> entities)
    {
        /*
         * Count and sort the entities per fragment.
         */
        foreach (var entity in entities)
        {
            ref Behavior cBehavior = ref entity.Get<Behavior>();
            if (null == cBehavior.Provider) continue;
            if (cBehavior.MaxDistance < 0f) continue;

            //var behaviorType = cBehavior.Provider.GetType();
            var perBehaviorStats = behaviorStats.GetPerBehaviorStats(cBehavior.Provider.GetType());
            //Trace($"Entity behavior type: {behaviorType.FullName}, Found perBehaviorStats: {perBehaviorStats != null}");

            /*
             * Only count behaviors that are tracked by spawn operators.
             */
            if (perBehaviorStats != null)
            {
                ref Transform3ToWorld cTransformWorld = ref entity.Get<Transform3ToWorld>();
                Index3 idxEntity = Fragment.PosToIndex3(cTransformWorld.Matrix.Translation);

                PerFragmentStats perFragmentStats = perBehaviorStats.FindPerFragmentStats(idxEntity);

                /*
                 * If we might need to kill some in this fragment, track the list of entities of that kind
                 * in a list to decide whom we should kill. Don't consider anything in front of the camera,
                 * unless it is too far away from the camera.
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
                        if (cBehavior.MayBePurged())
                        {
                            Vector3 v3Relative = si.Position - _cameraInfo.Position;
                            bool isFarAway = v3Relative.LengthSquared() > cBehavior.MaxDistance*cBehavior.MaxDistance;
                            bool isBehind = Vector3.Dot(_cameraInfo.Front, v3Relative) <= 0.7f;
                            if (isFarAway /* || isBehind*/) 
                            {
                                perFragmentStats.NeedVictims().Add(si);
                            }
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
        : base(I.Get<Engine>().GetEcsWorldNoAssert())
    {
        _engine = I.Get<Engine>();
    }
}