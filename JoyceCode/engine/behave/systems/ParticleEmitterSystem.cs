using System;
using System.Collections.Generic;
using System.Numerics;
using builtin.tools;
using DefaultEcs;
using engine.behave.components;
using engine.joyce.components;
using static engine.Logger;

namespace engine.behave.systems;


/**
 * This system continuously creates new particles.
 */
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
        
        Span<DefaultEcs.Entity> copiedEntities = stackalloc DefaultEcs.Entity[entities.Length];
        entities.CopyTo(copiedEntities);
        foreach (var entity in copiedEntities)
        {
            if (!entity.IsEnabled())
            {
                Error($"Did not expect an entity that is not enabled.");
                continue;
            }

            ref var cTransform3ToWorld = ref entity.Get<Transform3ToWorld>();
            ref var cParticleEmitter = ref entity.Get<ParticleEmitter>();
            if (0 >= cParticleEmitter.EmitterTimeToLive)
            {
                entity.Disable();
                _listDelete.Add(entity);
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
                var v = cParticleEmitter.Velocity.Length();
                var v3RandDirection = _rnd.GetVector3();
                v3RandDirection = Vector3.Normalize(v3RandDirection);
                Vector3 v3EffectiveDirection;
                if (v < 0.0001f)
                {
                    v3EffectiveDirection = Vector3.Normalize(v3RandDirection) * cParticleEmitter.RandomDirection;
                }
                else
                {
                    v3EffectiveDirection = v3RandDirection * cParticleEmitter.RandomDirection + (cParticleEmitter.Velocity / v) * (1f-cParticleEmitter.RandomDirection);
                    if (Single.Abs(v3EffectiveDirection.X) < 0.00001f) v3EffectiveDirection.X = 0.00001f;
                    v3EffectiveDirection = Vector3.Normalize(v3EffectiveDirection) * v;
                }

                Entity eParticle = _engine.CreateEntity("particle");
                eParticle.Set(
                    new Particle()
                    {
                        Position = v3Position,
                        TimeToLive = cParticleEmitter.ParticleTimeToLive,
                        Orientation = Quaternion.Identity,
                        VelocityPerFrame = v3EffectiveDirection * 1f/60f,
                        SpinPerFrame = cParticleEmitter.RotationVelocity
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
        _engine.AddDoomedEntities(_listDelete);
        _listDelete = null;
    }
    

    protected override void PreUpdate(float dt)
    {
        base.PreUpdate(dt);
        _cameraInfo = _engine.CameraInfo;
        _listDelete = new();
    }
    
    public ParticleEmitterSystem()
        : base(I.Get<Engine>().GetEcsWorldAnyThread())
    {
        _engine = I.Get<Engine>();
        _rnd = new builtin.tools.RandomSource("particleemitter");
    }
}