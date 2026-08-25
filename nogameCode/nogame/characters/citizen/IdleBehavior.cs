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
    private StuckAnimationReporter _stuckReporter;

    public override void Behave(in Entity entity, float dt)
    {
        if (!entity.IsAlive) return;

        if (!_animationSet)
        {
            /*
             * The flag is consumed only once the animation actually TOOK.
             *
             * It used to be set before the guard, so an entity whose FromModel had not
             * arrived yet - ModelCache attaches that through QueueMainThreadAction, so it
             * is routinely absent on the first behaving frame - burnt the one-shot and
             * never asked again. The NPC then stood in its bind pose, i.e. a T-pose, for
             * as long as it existed.
             *
             * The walking behaviours never showed this because they re-issue the animation
             * on every speed change, so they heal themselves. Standing ones have nothing
             * to heal them, which is exactly why the T-posed NPCs were the stationary ones.
             */
            if (entity.Has<GPUAnimationState>() && entity.Has<FromModel>())
            {
                ref var cGpuAnimationState = ref entity.Get<GPUAnimationState>();
                ref var cFromModel = ref entity.Get<FromModel>();
                ref var model = ref cFromModel.Model;
                _animationSet = true == cGpuAnimationState.AnimationState?.SetAnimation(
                    model, CharacterModelDescription.IdleAnimName, 0);
            }

            if (!_animationSet)
            {
                _stuckReporter.NoteFailure(entity, nameof(IdleBehavior),
                    CharacterModelDescription.IdleAnimName);
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
        _stuckReporter.Reset();

        ref engine.physics.components.Body cBody = ref entity0.Get<engine.physics.components.Body>();
        cBody.PhysicsObject?.MakeKinematic(ref cBody.Reference);
    }
}
