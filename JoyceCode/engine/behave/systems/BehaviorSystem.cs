using System;
using System.Numerics;
using System.Collections.Generic;
using System.Text;
using engine.quest;
using static engine.Logger;

namespace engine.behave.systems;

[DefaultEcs.System.With(typeof(components.Behavior))]
internal class BehaviorSystem : DefaultEcs.System.AEntitySetSystem<float>
{
    private engine.Engine _engine;

    private DefaultEcs.Entity _ePlayer;
    private Matrix4x4 _mPlayerTransform;
    private Vector3 _vPlayerPos;
    
    private bool _havePlayerPosition = false; 
    
    protected override void Update(
        float dt, ReadOnlySpan<DefaultEcs.Entity> entities)
    {
        Vector3 vPlayerPos;
        if (!_havePlayerPosition)
        {
            vPlayerPos = Vector3.Zero;
        }
        else
        {
            vPlayerPos = _vPlayerPos;
        }
        
        Span<DefaultEcs.Entity> copiedEntities = stackalloc DefaultEcs.Entity[entities.Length];
        entities.CopyTo(copiedEntities);
        foreach (var entity in copiedEntities)
        {
            /*
             * We automagically update velocity from behavior.
             * Assuming, that it modifies Transform3
             */
            bool hadTransform3 = entity.Has<joyce.components.Transform3>();
            joyce.components.Transform3 oldTransform = new();
            if (hadTransform3)
            {
                oldTransform = entity.Get<joyce.components.Transform3>();
            }

            bool hadWorldTransform3 = entity.Has<joyce.components.Transform3ToWorld>();
            joyce.components.Transform3ToWorld oldWorldTransform3 = new();
            Vector3 v3WorldPosition = new();
            if (hadWorldTransform3)
            {
                oldWorldTransform3 = entity.Get<joyce.components.Transform3ToWorld>();
                v3WorldPosition = oldWorldTransform3.Matrix.Translation;
            }

            ref var cBehavior = ref entity.Get<behave.components.Behavior>();

            if (11f == cBehavior.MaxDistance)
            {
                int a = 1;
            }
          
            if (hadWorldTransform3 && hadTransform3)
            {
                if (Vector3.DistanceSquared(v3WorldPosition, vPlayerPos) >= cBehavior.MaxDistance * cBehavior.MaxDistance)
                {
                    if (0 != (cBehavior.Flags & (ushort)components.Behavior.BehaviorFlags.InRange))
                    {
                        cBehavior.Provider?.OutOfRange(_engine, entity);
                        cBehavior.Flags = (ushort) (cBehavior.Flags & ~(uint)components.Behavior.BehaviorFlags.InRange);
                    }

                    if (oldTransform.IsVisible && 0 == (cBehavior.Flags & (ushort)components.Behavior.BehaviorFlags.DontVisibInRange))
                    {
                        I.Get<engine.joyce.TransformApi>().SetVisible(entity, false, true);
                    }

                    if (0 == (cBehavior.Flags & (ushort)components.Behavior.BehaviorFlags.MissionCritical))
                    {
                        /*
                         * Do not even advance to the point of calling the behaviour. 
                         */
                        continue;
                    }
                }
                else
                {
                    if (0 == (cBehavior.Flags & (ushort)components.Behavior.BehaviorFlags.InRange))
                    {
                        cBehavior.Provider?.InRange(_engine, entity);
                        cBehavior.Flags = (ushort) (cBehavior.Flags | (uint)components.Behavior.BehaviorFlags.InRange);
                    }

                    if (!oldTransform.IsVisible && 0 == (cBehavior.Flags & (ushort)components.Behavior.BehaviorFlags.DontVisibInRange))
                    {
                        I.Get<engine.joyce.TransformApi>().SetVisible(entity, true, false);
                    }
                }
            }
            else
            {
            }

            if (cBehavior.Provider == null)
            {
                continue;
            }

            if (0 == (cBehavior.Flags & (ushort)components.Behavior.BehaviorFlags.DontCallBehave))
            {
                /*
                 * Behave shall make it visible if it can.
                 */
                cBehavior.Provider.Behave(entity, dt);
            }

            if (0 == (cBehavior.Flags & (ushort)components.Behavior.BehaviorFlags.DontEstimateMotion))
            {
                if (dt > 0.0000001 && hadTransform3)
                {
                    if (entity.Has<joyce.components.Transform3>())
                    {
                        /*
                         * TXWTODO: We use the local position to estimate the velocity, that's bad.
                         * Unfortunately, we do not know the previous world position.
                         */
                        Vector3 vNewPosition = entity.Get<joyce.components.Transform3>().Position;
                        /*
                         * Write back/create motion for that one.
                         */
                        Vector3 vVelocity = (vNewPosition - oldTransform.Position) / dt;
                        entity.Set(new joyce.components.Motion(vVelocity));
                    }
                }
            }
        }
    }

    protected override void PostUpdate(float dt)
    {
        base.PostUpdate(dt);
    }


    protected override void PreUpdate(float dt)
    {
        base.PreUpdate(dt);
        _havePlayerPosition = false;
        if (_engine.Player.TryGet(out _ePlayer))
        {
            if (_ePlayer.Has<engine.joyce.components.Transform3ToWorld>())
            {
                _mPlayerTransform = _ePlayer.Get<engine.joyce.components.Transform3ToWorld>().Matrix;
                _vPlayerPos = _mPlayerTransform.Translation;
                _havePlayerPosition = true;
            }
        }
    }

    public BehaviorSystem()
        : base(I.Get<Engine>().GetEcsWorldAnyThread())
    {
        _engine = I.Get<Engine>();
    }
}

