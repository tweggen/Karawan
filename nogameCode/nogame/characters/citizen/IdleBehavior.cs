using System;
using DefaultEcs;
using engine;
using engine.behave;
using engine.joyce.components;
using engine.physics;

namespace nogame.characters.citizen;

/// <summary>
/// Stationary behavior: holds the NPC in place, plays an idle animation.
/// Used by StayAtStrategy when the NPC is at their destination.
/// </summary>
public class IdleBehavior : ABehavior
{
    public required CharacterModelDescription CharacterModelDescription;
    private bool _animationSet = false;

    public override void Behave(in Entity entity, float dt)
    {
        if (!entity.IsAlive) return;

        if (!_animationSet)
        {
            _animationSet = true;
            if (entity.Has<GPUAnimationState>() && entity.Has<FromModel>())
            {
                ref var cGpuAnimationState = ref entity.Get<GPUAnimationState>();
                ref var cFromModel = ref entity.Get<FromModel>();
                ref var model = ref cFromModel.Model;
                cGpuAnimationState.AnimationState?.SetAnimation(
                    model, CharacterModelDescription.IdleAnimName, 0);
            }
        }
    }


    public override void OnCollision(ContactEvent cev)
    {
        base.OnCollision(cev);
        CitizenCollisionRouter.Dispatch(
            cev.ContactInfo.PropertiesA,
            cev.ContactInfo.PropertiesB,
            cev.ContactInfo.ContactNormal);
    }


    public override void OnAttach(in engine.Engine engine0, in Entity entity0)
    {
        base.OnAttach(engine0, entity0);
        _animationSet = false;

        ref engine.physics.components.Body cBody = ref entity0.Get<engine.physics.components.Body>();
        cBody.PhysicsObject?.MakeKinematic(ref cBody.Reference);
    }
}
