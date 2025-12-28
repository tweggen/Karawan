

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

        Flags = (ushort)(((uint)Flags & ~(uint)AnimationState.IsOneShot) | (isOneShot?(uint)AnimationState.IsOneShot:0));
        ModelAnimation = ma;
        ModelAnimationFrame = frame;
    }
}
