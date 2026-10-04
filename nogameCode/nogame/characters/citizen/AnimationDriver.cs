using DefaultEcs;
using engine;
using engine.behave;
using engine.joyce.components;

namespace nogame.characters.citizen;


/**
 * Keeps asking for a character's animation until the request takes.
 *
 * A character's clip is set through GPUAnimationState.SetAnimation, and that call can fail
 * for a while after the entity exists: ModelCache attaches FromModel through a queued main
 * thread action, so it is routinely absent on the first behaving frame. **A one-shot at
 * creation time is therefore not a driver, it is a coin toss** - and that is exactly what
 * made the first T-pose fix (#106) a no-op twice, and what left the niceday NPCs and the
 * taxi passenger animated by nothing but EntityCreator.InitialAnimName.
 *
 * The rule this exists to make checkable is not "the creation site names a behaviour or a
 * strategy". Every site does, and one of them still had no animation at all: the niceday
 * NPCs start in RestStrategy, whose NearbyBehavior drives the "E to Talk" prompt and never
 * touched SetAnimation. The rule is that something SETS AN ANIMATION, every frame, until it
 * works - which is what this is, and what
 * tests/.../engine/joyce/CharacterAnimationDriverTests.cs checks for.
 *
 * The flag is consumed only once the animation actually took: setting it before the guard
 * burns the one attempt on a frame that could never have succeeded.
 */
public struct AnimationDriver
{
    private bool _isSet;
    private StuckAnimationReporter _stuckReporter;


    public void Reset()
    {
        _isSet = false;
        _stuckReporter.Reset();
    }


    /**
     * Call once per behaving frame. Cheap and branchless once the clip has taken.
     *
     * @param what
     *     The behaviour's own name, for the report - so a stuck character names the thing
     *     that was supposed to be animating it.
     */
    public void Drive(in Entity entity, string what, string strAnimation, ushort nStartFrame = 0)
    {
        if (_isSet) return;
        if (null == strAnimation) return;

        if (entity.Has<GPUAnimationState>() && entity.Has<FromModel>())
        {
            ref var cGpuAnimationState = ref entity.Get<GPUAnimationState>();
            ref var cFromModel = ref entity.Get<FromModel>();
            ref var model = ref cFromModel.Model;

            _isSet = true == cGpuAnimationState.AnimationState?.SetAnimation(
                model, strAnimation, nStartFrame);
        }

        if (!_isSet)
        {
            _stuckReporter.NoteFailure(entity, what, strAnimation);
        }
    }
}


/**
 * A behaviour whose entire job is to animate its character.
 *
 * For the one creation site that has neither a strategy nor a behaviour of its own to hang
 * an animation on: the taxi passenger, which has no physics body either (no
 * CollisionPropertiesFactory), so IdleBehavior is not usable - its OnAttach takes a ref to
 * the Body component and DefaultEcs would hand it a reference into unused storage.
 *
 * Touches nothing but the animation, so it is safe on a character with no body, no
 * collision properties and no position of its own.
 */
public class AnimationOnlyBehavior : ABehavior
{
    public required string AnimName { get; init; }

    private AnimationDriver _driver;


    public override void Behave(in Entity entity, float dt)
    {
        if (!entity.IsAlive) return;

        _driver.Drive(entity, nameof(AnimationOnlyBehavior), AnimName);
    }


    public override void OnAttach(in Engine engine0, in Entity entity0)
    {
        base.OnAttach(engine0, entity0);
        _driver.Reset();
    }
}
