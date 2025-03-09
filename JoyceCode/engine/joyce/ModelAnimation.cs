using System.Collections.Generic;
using System.Numerics;

namespace engine.joyce;


/*
 * A model baked frame contains matrices for the
 * transformations of all bones in an animation frame.
 */
public class ModelBakedFrame
{
    /**
     * Array of actual transformations. This contains model->NBones members
     */
    public Matrix4x4[] BoneTransformations;
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