using System.Collections.Generic;
using System.Numerics;

namespace engine.joyce;

public class ModelBakedFrame
{
    public Matrix4x4[] BonePositions;
}

/**
 * Represents one specific animation that can be applied to a skeleton.
 */
public class ModelAnimation
{
    public int Index;
    public string Name;
    public float Duration;
    public float TicksPerSecond;
    public uint NTicks;
    public uint NFrames;
    public Dictionary<ModelNode, ModelAnimChannel> MapChannels;
    public ModelAnimChannel[] Channels;

    public ModelBakedFrame[] BakedFrames;
}