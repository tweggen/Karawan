namespace engine.joyce.components;

public struct AnimationState
{
    public ModelAnimation? ModelAnimation;
    public ushort ModelAnimationFrame;

    public const uint IsOneShot = 1;
    public ushort Flags;
}