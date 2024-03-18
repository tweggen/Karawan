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


[DefaultEcs.System.With(typeof(engine.behave.components.Behavior))]
[DefaultEcs.System.With(typeof(engine.joyce.components.Transform3ToWorld))]
public class SpawnSystem : DefaultEcs.System.AEntitySetSystem<BehaviorStats>
{
    private readonly Engine _engine;

    private bool haveCamera;
    private DefaultEcs.Entity _eCamera;
    private Camera3 _cCamCamera3;
    private Transform3ToWorld _cCamTransform3ToWorld;
    private Vector3 _v3CameraPos;
    private Vector3 _v3CameraFront;
    
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
        _eCamera = _engine.GetCameraEntity();
        if (_eCamera.IsAlive && _eCamera.Has<Camera3>() && _eCamera.Has<Transform3ToWorld>())
        {
            haveCamera = true;
            _cCamCamera3 = _eCamera.Get<Camera3>();
            _cCamTransform3ToWorld = _eCamera.Get<Transform3ToWorld>();
            _v3CameraPos = _cCamTransform3ToWorld.Matrix.Translation;
            _v3CameraFront = new Vector3(_cCamTransform3ToWorld.Matrix.M31, _cCamTransform3ToWorld.Matrix.M32, _cCamTransform3ToWorld.Matrix.M33);
        }
    }
    
    
    public SpawnSystem() 
        : base(I.Get<Engine>().GetEcsWorld())
    {
        _engine = I.Get<Engine>();
    }
}