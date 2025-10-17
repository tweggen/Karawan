namespace engine.joyce.components;

public struct GPUAnimationState
{
    public ModelAnimation? ModelAnimation;
    public ushort ModelAnimationFrame;

    public const ushort IsOneShot = 1;
    public ushort Flags;
}