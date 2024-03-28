using System;
using System.Collections.Generic;
using System.Numerics;
using engine.behave.components;
using engine.joyce.components;

namespace engine.behave.systems;

[DefaultEcs.System.With(typeof(components.Particle))]
[DefaultEcs.System.With(typeof(Transform3ToWorld))]
internal class ParticleSystem : DefaultEcs.System.AEntitySetSystem<float>
{
    private engine.Engine _engine;

    protected override void Update(
        float dt, ReadOnlySpan<DefaultEcs.Entity> entities)
    {
        List<DefaultEcs.Entity> listDelete = new();
        foreach (var entity in entities)
        {
            ref var cParticle = ref entity.Get<Particle>();
            if (0 == --cParticle.TimeToLive)
            {
                listDelete.Add(entity);
            }
            else
            {
                ref var cTransform = ref entity.Get<joyce.components.Transform3ToWorld>();
                Vector3 v3Translate = cTransform.Matrix.Translation;
                // TXWTODO: This heavily can be optzimized.
                cTransform.Matrix *=
                    Matrix4x4.CreateTranslation(-v3Translate)
                    * Matrix4x4.CreateFromQuaternion(cParticle.Spin*dt)
                    * Matrix4x4.CreateTranslation(v3Translate+cParticle.Velocity*dt);
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