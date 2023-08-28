using System;
using System.Numerics;
using System.Collections.Generic;
using System.Text;

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
        if (!_havePlayerPosition)
        {
            return;
        }
        
        Span<DefaultEcs.Entity> copiedEntities = stackalloc DefaultEcs.Entity[entities.Length];
        entities.CopyTo(copiedEntities);
        foreach (var entity in copiedEntities)
        {
            /*
             * We automagically update velocity from behavior.
             * Assuming, that it modifies Transform3
             */
            bool hadTransform3 = entity.Has<transform.components.Transform3>();
            transform.components.Transform3 oldTransform = new();
            if (hadTransform3)
            {
                oldTransform = entity.Get<transform.components.Transform3>();
            }
            else
            {
            }

            var cBehavior = entity.Get<behave.components.Behavior>();

            if (hadTransform3)
            {
                if (false && Vector3.DistanceSquared(oldTransform.Position, _vPlayerPos) >= cBehavior.MaxDistance * cBehavior.MaxDistance)
                {
                    if (oldTransform.IsVisible)
                    {
                        //_engine.GetATransform().SetVisible(entity, false);
                    }
                    continue;
                }
            }

            if (cBehavior.Provider == null)
            {
                continue;
            }
            
            /*
             * Behave shall make it visible if it can.
             */
            cBehavior.Provider.Behave(entity, dt);
 
            if (dt > 0.0000001 && hadTransform3)
            {
                if (entity.Has<transform.components.Transform3>())
                {
                    Vector3 vNewPosition = entity.Get<transform.components.Transform3>().Position;
                    /*
                     * Write back/create motion for that one.
                     */
                    Vector3 vVelocity = (vNewPosition - oldTransform.Position) / dt;
                    entity.Set(new joyce.components.Motion(vVelocity));
                }
            }
        }
    }

    protected override void PostUpdate(float dt)
    {
    }


    protected override void PreUpdate(float dt)
    {
        _havePlayerPosition = false;
        _ePlayer = _engine.GetPlayerEntity();
        if (_ePlayer.IsAlive && _ePlayer.IsEnabled())
        {
            if (_ePlayer.Has<engine.transform.components.Transform3ToWorld>())
            {
                _mPlayerTransform = _ePlayer.Get<engine.transform.components.Transform3ToWorld>().Matrix;
                _vPlayerPos = _mPlayerTransform.Translation;
                _havePlayerPosition = true;
            }
        }
    }

    public BehaviorSystem(in engine.Engine engine)
        : base(engine.GetEcsWorld())
    {
        _engine = engine;
    }
}

