using System;
using DefaultEcs;
using engine.joyce.components;
using static engine.Logger;

namespace engine.joyce.systems;


/**
 * Automatically advance all animated things.
 * Note: It is actually not perfect to restrict this on GPUAnimationState.
 */
[DefaultEcs.System.With(typeof(GPUAnimationState))]
public class AnimationSystem : DefaultEcs.System.AEntitySetSystem<float>
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

            ref GPUAnimationState cGpuAnimationState = ref entity.Get<GPUAnimationState>();
            ref var animationState = ref cGpuAnimationState.AnimationState;
            if (null == animationState) continue;
            ref var modelAnimation = ref animationState.ModelAnimation;
            if (null == modelAnimation) continue;

            ushort frameno = animationState.ModelAnimationFrame;
            ushort nframes = (ushort) modelAnimation.NFrames;
            if ((animationState.Flags & AnimationState.IsOneShot) == 0)
            {
                frameno += advanceNow;
                while (frameno >= nframes)
                {
                    frameno -= nframes;
                }

                animationState.ModelAnimationFrame = frameno;
            }
            else
            {
                ushort newframeno = (ushort)(frameno + advanceNow);
                newframeno = UInt16.Min((ushort)(nframes - 1), newframeno);

                if (newframeno != frameno)
                {
                    animationState.ModelAnimationFrame = newframeno;
                }
            }

        }

    }

    public AnimationSystem()
        : base(I.Get<Engine>().GetEcsWorldAnyThread())
    {
        _engine = I.Get<Engine>();
    }
}