using DefaultEcs;
using engine.joyce;
using engine.joyce.components;
using static engine.Logger;

namespace nogame.characters.citizen;

/**
 * Says out loud when a behaviour has been unable to set its animation for a long time.
 *
 * A behaviour that retries until it succeeds (see IdleBehavior) is right, and it is also
 * indistinguishable from one that will retry forever. Both leave the character in its bind
 * pose - a T-pose - and neither logs anything. That is how the reported T-posed NPCs
 * survived a first fix: the retry was added, it changed nothing, and there was no evidence
 * either way.
 *
 * So: after a couple of seconds of failing, report once, with the reason. Once, because a
 * per-frame log from every NPC in a cluster is not a diagnostic, it is a denial of service.
 */
public struct StuckAnimationReporter
{
    /**
     * ~2 seconds at 60 Hz. Long enough that an ordinary asynchronous model load has
     * finished - that is the failure that is SUPPOSED to resolve itself - and short enough
     * that a human notices the log in the same session as the T-pose.
     */
    private const int ReportAfterAttempts = 120;

    private int _nFailures;
    private bool _hasReported;


    public void Reset()
    {
        _nFailures = 0;
        _hasReported = false;
    }


    /**
     * Call on every failed attempt. Returns true when it has just reported.
     */
    public bool NoteFailure(in Entity entity, string what, string? strAnimation)
    {
        if (_hasReported)
        {
            return false;
        }

        if (++_nFailures < ReportAfterAttempts)
        {
            return false;
        }

        _hasReported = true;

        string reason;
        if (!entity.Has<GPUAnimationState>())
        {
            reason = "the entity has no GPUAnimationState component";
        }
        else if (null == entity.Get<GPUAnimationState>().AnimationState)
        {
            reason = "the entity's GPUAnimationState carries a null AnimationState";
        }
        else if (!entity.Has<FromModel>())
        {
            reason = "the entity has no FromModel component";
        }
        else
        {
            reason = AnimationState.DescribeFailure(entity.Get<FromModel>().Model, strAnimation);
        }

        Error($"{what}: still cannot select animation '{strAnimation ?? "(null)"}' after "
              + $"{ReportAfterAttempts} attempts - {reason}. This character is rendering in "
              + "its bind pose (a T-pose).");
        return true;
    }
}
