using System.Collections.Generic;
using System.Numerics;
using MessagePack;

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
[MessagePackObject(AllowPrivate=true)]
public class ModelAnimation
{
    [Key(0)]
    public int Index;
    [ Key(1)]
    public string Name;
    [Key(2)]
    public float Duration;
    [Key(3)]
    public float TicksPerSecond;
    [Key(4)]
    public uint FirstFrame;
    [Key(5)]
    public uint NTicks;
    [Key(6)]
    public uint NFrames;
    
    /**
     * This animation might have an animation specific rest pose
     * different to the model we are associated with
     */
    [IgnoreMember]
    public ModelNode? RestPose;
    [IgnoreMember]
    public Dictionary<ModelNode, ModelAnimChannel> MapChannels;
    
    /**
     * Contains all the frames for skin transformation on GPU. Note: This data
     * structure is not used, we use allBakedFrames from the model.
     */
    [IgnoreMember]
    public ModelBakedFrame[] BakedFrames;
    
    /**
     * Contains the baked frames for nodes that should reside on CPU.
     */
    [Key(7)]
    public SortedDictionary<string, Matrix4x4[]> CpuFrames;

    public ModelAnimChannel CreateChannel(
        ModelNode mnChannel,
        KeyFrame<Vector3>[]? positions,
        KeyFrame<Quaternion>[]? rotations,
        KeyFrame<Vector3>[]? scalings
        )
    {
        return new ModelAnimChannel()
        {
            ModelAnimation = this,
            Target = mnChannel,
            Positions = positions,
            Rotations = rotations,
            Scalings = scalings,
        };
    }
}