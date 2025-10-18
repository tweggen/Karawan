using System;
using System.Diagnostics;
using DefaultEcs;
using engine.joyce.components;
using static engine.Logger;

namespace engine.joyce.systems;

[DefaultEcs.System.With(typeof(CpuAnimated))]
[DefaultEcs.System.With(typeof(Transform3ToParent))]
public class CpuBoneSystem : DefaultEcs.System.AEntitySetSystem<float>
{
    private engine.Engine _engine;

    private float _error = 0f; 

    protected override void Update(
        float dt, ReadOnlySpan<Entity> entities)
    {
        Span<Entity> copiedEntities = stackalloc Entity[entities.Length];
        entities.CopyTo(copiedEntities);

        ushort advanceNow = (ushort)((dt+_error) * 60f);
        _error += dt - (advanceNow / 60f);
        
        foreach (var entity in copiedEntities)
        {
            if (!entity.IsEnabled())
            {
                Error($"Did not expect an entity that is not enabled.");
                continue;
            }

            ref CpuAnimated cCpuAnimated = ref entity.Get<CpuAnimated>();
            ref var animationState = ref cCpuAnimated.AnimationState;
            if (null == animationState) continue;
            ref var modelAnimation = ref animationState.ModelAnimation;
            if (null == modelAnimation) continue;

            ushort frameno = animationState.ModelAnimationFrame;
            ushort nframes = (ushort) modelAnimation.NFrames;
            Debug.Assert(frameno < nframes);
            
            /*
             * Now read the resulting baked matrix from the animation and apply it
             * to the local Transform2Parent
             */
            if (modelAnimation.CpuFrames.TryGetValue(
                    cCpuAnimated.ModelNodeName, 
                    out var arrMatrices))
            {
                Debug.Assert(frameno < arrMatrices.Length);
                ref var m4Transform = ref arrMatrices[frameno];
                ref var cTransform3 = ref entity.Get<Transform3ToParent>();
                cTransform3.Matrix = m4Transform;
            }
        }

    }

    public CpuBoneSystem()
        : base(I.Get<Engine>().GetEcsWorldAnyThread())
    {
        _engine = I.Get<Engine>();
    }
}