

namespace engine.joyce;

public class AnimationState
{
    public ModelAnimation? ModelAnimation;
    public ushort ModelAnimationFrame;

    public const ushort IsOneShot = 1;
    public ushort Flags;

    /**
     * Select an animation clip on this state.
     *
     * RETURNS FALSE IF NOTHING WAS SELECTED, and callers are expected to care.
     *
     * Every way of failing here ends in the same place: ModelAnimation is null, no bone
     * matrices are produced, and the character renders in its BIND POSE - the T-pose.
     * There are four ways to get there and none of them is exotic:
     *
     *   model == null                the entity has no FromModel yet. ModelCache attaches
     *                                that through QueueMainThreadAction, so it is NOT
     *                                available on the frame the entity starts behaving.
     *   strAnimation == null         the CharacterModelDescription did not name a clip.
     *   no AnimationCollection       the model is still the loading placeholder.
     *   name not in MapAnimations    the clip is not in the pack this model was baked
     *                                with, e.g. asking for Idle_Generic on a model that
     *                                only carries locomotion_hardday.
     *
     * The first and third are TRANSIENT - they resolve a frame or two later - which is
     * why a caller that treats "set the animation once" as a one-shot will silently strand
     * the character in a T-pose forever. Use the return value to decide whether to retry.
     */
    public bool SetAnimation(Model? model, string? strAnimation, ushort frame = 0, bool isOneShot = false)
    {
        ModelAnimation ma;

        if (null == model
            || null == strAnimation
            || null == model.AnimationCollection
            || null == model.AnimationCollection.MapAnimations
            || !model.AnimationCollection.MapAnimations.TryGetValue(strAnimation, out ma))
        {
            ModelAnimation = null;
            ModelAnimationFrame = 0;
            return false;
        }

        // Ensure frame is within bounds for the new animation before switching.
        // Prevents race condition where animation changes while AnimationSystem is
        // wrapping frame numbers, which could leave the frame counter in an invalid
        // state relative to the new animation.
        ushort validatedFrame = frame;
        if (ma.NFrames > 0 && validatedFrame >= ma.NFrames)
        {
            validatedFrame = (ushort)(ma.NFrames - 1);
        }

        Flags = (ushort)(((uint)Flags & ~(uint)AnimationState.IsOneShot) | (isOneShot?(uint)AnimationState.IsOneShot:0));
        ModelAnimation = ma;
        ModelAnimationFrame = validatedFrame;
        return true;
    }


    /**
     * Why SetAnimation would refuse, in words.
     *
     * A caller that has been retrying for a while needs to say WHICH of the four failures
     * it is stuck on, because they have completely different causes - a missing FromModel
     * resolves itself, a clip that is not in the pack never will. Every one of them
     * previously presented as the same thing: a character in a T-pose and not one line of
     * log anywhere.
     */
    public static string DescribeFailure(Model? model, string? strAnimation)
    {
        if (null == model)
        {
            return "the entity has no model yet (FromModel not attached)";
        }

        if (null == strAnimation)
        {
            return $"no clip was named for model '{model.Name}'";
        }

        if (null == model.AnimationCollection)
        {
            return $"model '{model.Name}' has no AnimationCollection - it was loaded without "
                   + "an AnimationUrls property, or is still a placeholder";
        }

        if (null == model.AnimationCollection.MapAnimations)
        {
            return $"model '{model.Name}' has an AnimationCollection but no MapAnimations";
        }

        if (!model.AnimationCollection.MapAnimations.ContainsKey(strAnimation))
        {
            return $"model '{model.Name}' has no clip '{strAnimation}'; it carries "
                   + $"[{string.Join(", ", model.AnimationCollection.MapAnimations.Keys)}]";
        }

        return "no failure - the clip is present";
    }
}
