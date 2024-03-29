using System;
using System.Collections.Generic;
using System.Numerics;
using builtin.tools;
using DefaultEcs;
using engine.behave.components;
using engine.joyce.components;

namespace engine.behave.systems;

[DefaultEcs.System.With(typeof(components.ParticleEmitter))]
[DefaultEcs.System.With(typeof(Transform3ToWorld))]
public class ParticleEmitterSystem : DefaultEcs.System.AEntitySetSystem<float>
{
    private engine.Engine _engine;
    private readonly RandomSource _rnd;

    protected override void Update(
        float dt, ReadOnlySpan<DefaultEcs.Entity> entities)
    {
        List<DefaultEcs.Entity> listDelete = new();
        foreach (var entity in entities)
        {
            ref var cTransform3ToWorld = ref entity.Get<Transform3ToWorld>();
            ref var cParticleEmitter = ref entity.Get<ParticleEmitter>();
            if (0 == --cParticleEmitter.EmitterTimeToLive)
            {
                listDelete.Add(entity);
            }
            else
            {
                Entity eParticle = _engine.GetEcsWorld().CreateEntity();
                Vector3 v3Position =
                    cParticleEmitter.Position
                    + new Vector3(
                        (-1f + 2f * _rnd.GetFloat()) * cParticleEmitter.RandomPos.X,
                        (-1f + 2f * _rnd.GetFloat()) * cParticleEmitter.RandomPos.Y,
                        (-1f + 2f * _rnd.GetFloat()) * cParticleEmitter.RandomPos.Z
                    ); 
                eParticle.Set(
                    new Particle()
                    {
                        Position = v3Position
                        TimeToLive = cParticleEmitter.ParticleTimeToLive,
                        Orientation = Quaternion.Identity,
                        VelocityPerFrame = cParticleEmitter.Velocity * 1f/60f,
                        SpinPerFrame = Quaternion.Identity
                    }
                );
                eParticle.Set(new Transform3ToWorld()
                    {
                        Matrix = Matrix4x4.CreateTranslation(v3Position),
                        CameraMask = cParticleEmitter.CameraMask,
                        IsVisible = true
                    }
                );
            }
        }

        foreach (var entity in listDelete)
        {
            entity.Dispose();
        }
    }
    
    public ParticleEmitterSystem()
        : base(I.Get<Engine>().GetEcsWorld())
    {
        _engine = I.Get<Engine>();
        _rnd = new builtin.tools.RandomSource("particleemitter");
    }
}