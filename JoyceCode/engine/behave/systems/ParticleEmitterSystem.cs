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
    private CameraInfo? _cameraInfo = null;
    private List<Entity> _listDelete = null;

    protected override void Update(
        float dt, ReadOnlySpan<Entity> entities)
    {
        if (null == _cameraInfo) return;
        
        List<Entity> listDelete = new();
        Span<DefaultEcs.Entity> copiedEntities = stackalloc DefaultEcs.Entity[entities.Length];
        entities.CopyTo(copiedEntities);
        foreach (var entity in copiedEntities)
        {
            ref var cTransform3ToWorld = ref entity.Get<Transform3ToWorld>();
            ref var cParticleEmitter = ref entity.Get<ParticleEmitter>();
            if (0 >= cParticleEmitter.EmitterTimeToLive)
            {
                listDelete.Add(entity);
            }
            else
            {
                --cParticleEmitter.EmitterTimeToLive;
                float maxDistSquared = cParticleEmitter.MaxDistance * cParticleEmitter.MaxDistance;

                Vector3 v3EmitterPosition = cTransform3ToWorld.Matrix.Translation;
                if ((v3EmitterPosition - _cameraInfo.Position).LengthSquared() > maxDistSquared)
                {
                    continue;
                }
                Vector3 v3Position =
                    v3EmitterPosition
                    + cParticleEmitter.Position
                    + new Vector3(
                        (-1f + 2f * _rnd.GetFloat()) * cParticleEmitter.RandomPos.X,
                        (-1f + 2f * _rnd.GetFloat()) * cParticleEmitter.RandomPos.Y,
                        (-1f + 2f * _rnd.GetFloat()) * cParticleEmitter.RandomPos.Z
                    ); 

                Entity eParticle = _engine.GetEcsWorld().CreateEntity();
                eParticle.Set(
                    new Particle()
                    {
                        Position = v3Position,
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
                eParticle.Set(new Instance3()
                {
                    InstanceDesc = cParticleEmitter.InstanceDesc
                });
            }
        }
    }


    protected override void PostUpdate(float dt)
    {
        base.PostUpdate(dt);
        foreach (var entity in _listDelete)
        {
            entity.Dispose();
        }
        _listDelete = null;
    }
    

    protected override void PreUpdate(float dt)
    {
        base.PreUpdate(dt);
        _cameraInfo = _engine.CameraInfo;
        _listDelete = new();
    }
    
    public ParticleEmitterSystem()
        : base(I.Get<Engine>().GetEcsWorld())
    {
        _engine = I.Get<Engine>();
        _rnd = new builtin.tools.RandomSource("particleemitter");
    }
}