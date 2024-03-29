using System;
using System.Collections.Generic;
using System.Numerics;
using DefaultEcs;
using engine.behave.components;
using engine.joyce.components;

namespace engine.behave.systems;

[DefaultEcs.System.With(typeof(components.Particle))]
[DefaultEcs.System.With(typeof(Transform3ToWorld))]
public class ParticleSystem : DefaultEcs.System.AEntitySetSystem<float>
{
    private engine.Engine _engine;

    protected override void Update(
        float dt, ReadOnlySpan<DefaultEcs.Entity> entities)
    {
        List<DefaultEcs.Entity> listDelete = new();
        foreach (var entity in entities)
        {
            ref var cTransform3ToWorld = ref entity.Get<Transform3ToWorld>();
            ref var cParticle = ref entity.Get<Particle>();
            if (0 == --cParticle.TimeToLive)
            {
                cTransform3ToWorld.IsVisible = false;
                listDelete.Add(entity);
            }
            else
            {
                cTransform3ToWorld.Matrix =
                    Matrix4x4.CreateFromQuaternion(cParticle.Orientation)
                    * Matrix4x4.CreateTranslation(cParticle.Position);
                cParticle.Orientation = 
                    Quaternion.Concatenate(cParticle.Orientation, cParticle.SpinPerFrame);
                cParticle.Position += 
                    cParticle.VelocityPerFrame;
            }
        }

        foreach (var entity in listDelete)
        {
            entity.Dispose();
        }
    }
    
    public ParticleSystem()
        : base(I.Get<Engine>().GetEcsWorld())
    {
        _engine = I.Get<Engine>();
    }
}