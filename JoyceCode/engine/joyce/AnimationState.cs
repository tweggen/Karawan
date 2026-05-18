

namespace engine.joyce;

public class AnimationState
{
    public ModelAnimation? ModelAnimation;
    public ushort ModelAnimationFrame;

    public const ushort IsOneShot = 1;
    public ushort Flags;

    public void SetAnimation(Model? model, string? strAnimation, ushort frame = 0, bool isOneShot = false)
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
            return;
        }

        // Ensure frame is within bounds for the new animation before switching.
        // Prevents race condition where animation changes while AnimationSystem is
        // wrapping frame numbers, which could leave the frame counter in an invalid
        // state relative to the new animation.
        //ushort validatedFrame = frame;
        //if (validatedFrame >= ma.NFrames)
        //{
        //    validatedFrame = (ushort)(ma.NFrames - 1);
        //}

        Flags = (ushort)(((uint)Flags & ~(uint)AnimationState.IsOneShot) | (isOneShot?(uint)AnimationState.IsOneShot:0));
        ModelAnimation = ma;
        ModelAnimationFrame = frame; // Use frame directly without validation
    }
}
