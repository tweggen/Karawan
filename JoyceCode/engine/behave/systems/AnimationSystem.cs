using System;
using DefaultEcs;
using engine.joyce.components;
using static engine.Logger;

namespace engine.behave.systems;

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
            ref var modelAnimation = ref cGpuAnimationState.ModelAnimation;
            if (null != modelAnimation)
            {
                ushort frameno = cGpuAnimationState.ModelAnimationFrame;
                ushort nframes = (ushort) modelAnimation.NFrames;
                if ((cGpuAnimationState.Flags & GPUAnimationState.IsOneShot) == 0)
                {
                    frameno += advanceNow;
                    while (frameno >= nframes)
                    {
                        frameno -= nframes;
                    }

                    cGpuAnimationState.ModelAnimationFrame = frameno;
                    entity.Set(cGpuAnimationState);
                }
                else
                {
                    ushort newframeno = (ushort)(frameno + advanceNow);
                    newframeno = UInt16.Min((ushort)(nframes-1), newframeno);
                    
                    if (newframeno != frameno)
                    {
                        cGpuAnimationState.ModelAnimationFrame = newframeno;
                        entity.Set(cGpuAnimationState);
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