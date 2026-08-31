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

    /**
     * The retry that heals a stationary character.
     *
     * The walking behaviours re-issue their clip on every speed change, so they heal
     * themselves; standing ones have nothing to heal them, which is exactly why the
     * reported T-posed NPCs were the stationary ones. The retry itself moved into
     * AnimationDriver so the other two standing sites can have it too - see there for why
     * "the site names a driver" is not the property that matters.
     */
    private AnimationDriver _driver;

    public override void Behave(in Entity entity, float dt)
    {
        if (!entity.IsAlive) return;

        _driver.Drive(entity, nameof(IdleBehavior),
            CharacterModelDescription.IdleAnimName);
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
        _driver.Reset();

        ref engine.physics.components.Body cBody = ref entity0.Get<engine.physics.components.Body>();
        cBody.PhysicsObject?.MakeKinematic(ref cBody.Reference);
    }
}
