namespace engine.joyce.components;

public struct AnimationState
{
    public ModelAnimation? ModelAnimation;
    public ushort ModelAnimationFrame;

    public const ushort IsOneShot = 1;
    public ushort Flags;
}