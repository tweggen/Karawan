using System;
using DefaultEcs;
using engine.joyce.components;
using static engine.Logger;

namespace engine.behave.systems;

[DefaultEcs.System.With(typeof(AnimationState))]
public class AnimationSystem : DefaultEcs.System.AEntitySetSystem<float>
{
    private engine.Engine _engine;

    private float _error = 0f; 

    protected override void Update(
        float dt, ReadOnlySpan<Entity> entities)
    {
        return;
        Span<Entity> copiedEntities = stackalloc Entity[entities.Length];
        entities.CopyTo(copiedEntities);

        uint advanceNow = (uint)((dt+_error) * 60f);
        _error += dt - (advanceNow / 60f);
        
        foreach (var entity in copiedEntities)
        {
            if (!entity.IsEnabled())
            {
                Error($"Did not expect an entity that is not enabled.");
                continue;
            }

            ref AnimationState cAnimationState = ref entity.Get<AnimationState>();
            ref var modelAnimation = ref cAnimationState.ModelAnimation;
            if (null != modelAnimation)
            {
                uint frameno = cAnimationState.ModelAnimationFrame;
                frameno += advanceNow;
                uint nframes = modelAnimation.NFrames;
                while (frameno >= nframes)
                {
                    frameno -= nframes;
                }

                cAnimationState.ModelAnimationFrame = frameno;
                entity.Set(cAnimationState);
            }
        }

    }

    public AnimationSystem()
        : base(I.Get<Engine>().GetEcsWorldNoAssert())
    {
        _engine = I.Get<Engine>();
    }
}