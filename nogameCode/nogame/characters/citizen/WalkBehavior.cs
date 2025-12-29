using System;
using System.Numerics;
using builtin.tools;
using DefaultEcs;
using engine;
using engine.joyce;
using engine.joyce.components;
using engine.news;
using engine.physics;
using nogame.cities;
using static engine.Logger;

namespace nogame.characters.citizen;

public class WalkBehavior : builtin.tools.SimpleNavigationBehavior
{
    public required CharacterModelDescription CharacterModelDescription;
    
    public float _previousSpeed = Single.MinValue;

    /**
     * Verify that the animation of the character matches the behavior.
     */
    private void _behaveUpdateAnimation(in DefaultEcs.Entity entity, float dt)
    {
        if (!entity.IsAlive) return;
        
        float speed = (Navigator as SegmentNavigator)!.Speed;
        if (speed == _previousSpeed) return;
        /*
         * Ensure we have a gpu navigation state component, including the animation
         * state.
         */
        if (!entity.Has<engine.joyce.components.GPUAnimationState>())
        {
            entity.Set(new engine.joyce.components.GPUAnimationState()
            {
                AnimationState = CharacterModelDescription.AnimationState 
            });
        }
        
        ref var cGpuAnimationState = ref entity.Get<engine.joyce.components.GPUAnimationState>();
        ref var cFromModel = ref entity.Get<engine.joyce.components.FromModel>();
        ref var model = ref cFromModel.Model;
        string strAnimation;

        if (speed > 7f / 3.6f)
        {
            strAnimation = CharacterModelDescription.RunAnimName;
        }
        else if (speed > 0f)
        {
            strAnimation = CharacterModelDescription.WalkAnimName;
        }
        else
        {
            strAnimation = CharacterModelDescription.IdleAnimName;
        }

        _previousSpeed = speed;
        cGpuAnimationState.AnimationState?.SetAnimation(model, strAnimation, 0);
    }

    
    public override void OnCollision(ContactEvent cev)
    {
        base.OnCollision(cev);
        
        /*
         * Notify the owning strategy about the collision.
         */
        var me = cev.ContactInfo.PropertiesA;
        var other = cev.ContactInfo.PropertiesB;

        if (other != null)
        {
            if (0 != (other.SolidLayerMask & CollisionProperties.Layers.AnyWeapon))
            {
                I.Get<EventQueue>().Push(new Event(EntityStrategy.HitEventPath(me.Entity), ""));
            }

            if (0 != (other.SolidLayerMask & CollisionProperties.Layers.AnyVehicle))
            {
                I.Get<EventQueue>().Push(new Event(EntityStrategy.CrashEventPath(me.Entity), ""));
            }
        }
    }


    public override void Behave(in Entity entity, float dt)
    {
        base.Behave(entity, dt);
        _behaveUpdateAnimation(entity, dt);
    }


    public override void OnAttach(in engine.Engine engine0, in Entity entity0)
    {
        base.OnAttach(engine0, entity0);

        /*
         * When attaching, we need to invalid the previously cached values.
         */
        _previousSpeed = Single.MinValue;
        
        /*
         * Make me a dynamic object to respond to the collision.
         */
        ref engine.physics.components.Body cCitizenBody = ref entity0.Get<engine.physics.components.Body>();
        cCitizenBody.PhysicsObject?.MakeKinematic(ref cCitizenBody.Reference);
    }
}