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

    private DefaultEcs.Entity _eEntity;

    private void _onBumpEvent(Event ev)
    {
        if (ev is not BumpEvent be) return;
        (Navigator as SegmentNavigator)?.ApplyLateralBump(be.Direction, 0.4f, 0.4f);
    }

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
        CitizenCollisionRouter.Dispatch(
            cev.ContactInfo.PropertiesA,
            cev.ContactInfo.PropertiesB,
            cev.ContactInfo.ContactNormal);
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

        _eEntity = entity0;
        I.Get<SubscriptionManager>().Subscribe(
            EntityStrategy.BumpEventPath(_eEntity), _onBumpEvent);
    }


    public override void OnDetach(in Entity entity)
    {
        if (_eEntity != default)
        {
            I.Get<SubscriptionManager>().Unsubscribe(
                EntityStrategy.BumpEventPath(_eEntity), _onBumpEvent);
            _eEntity = default;
        }
        base.OnDetach(entity);
    }
}