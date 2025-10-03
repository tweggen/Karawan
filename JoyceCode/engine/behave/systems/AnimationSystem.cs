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

            ref AnimationState cAnimationState = ref entity.Get<AnimationState>();
            ref var modelAnimation = ref cAnimationState.ModelAnimation;
            if (null != modelAnimation)
            {
                ushort frameno = cAnimationState.ModelAnimationFrame;
                ushort nframes = (ushort) modelAnimation.NFrames;
                if ((cAnimationState.Flags & AnimationState.IsOneShot) == 0)
                {
                    frameno += advanceNow;
                    while (frameno >= nframes)
                    {
                        frameno -= nframes;
                    }

                    cAnimationState.ModelAnimationFrame = frameno;
                    entity.Set(cAnimationState);
                }
                else
                {
                    ushort newframeno = (ushort)(frameno + advanceNow);
                    newframeno = UInt16.Max((ushort)(nframes-1), newframeno);
                    
                    if (newframeno != frameno)
                    {
                        cAnimationState.ModelAnimationFrame = frameno;
                        entity.Set(cAnimationState);
                    }
                }
            }
        }

    }

    public AnimationSystem()
        : base(I.Get<Engine>().GetEcsWorldNoAssert())
    {
        _engine = I.Get<Engine>();
    }
}