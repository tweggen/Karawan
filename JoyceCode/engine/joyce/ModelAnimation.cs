using System;
using System.Collections.Generic;
using System.Numerics;
using MessagePack;
using static engine.Logger;

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


    /**
     * Translate a frame number counted within this animation into the frame number
     * used by the model's flat AllBakedMatrices array, where every animation's
     * frames are laid out end to end.
     *
     * localFrameno is clamped into this animation's own range: callers snapshot the
     * animation and the frame number as two separate reads, so a frame belonging to
     * a previously played, longer clip can arrive here.
     *
     * Everything downstream of the batch - the SSBO vertex attribute, the UBO slice,
     * the uniform slice - expects this global number. Handing any of them a local
     * one makes the clip read from the start of the array, i.e. play whichever
     * animation sorts first by name.
     */
    public uint GetGlobalFrame(uint localFrameno)
    {
        if (0 == NFrames)
        {
            return FirstFrame;
        }

        if (localFrameno >= NFrames)
        {
            localFrameno = NFrames - 1;
        }

        return FirstFrame + localFrameno;
    }


    /**
     * Compute the index of the first bone matrix of the given global frame, returning
     * false if it does not fit inside the supplied matrix array.
     */
    public static bool TryGetBakedFrameOffset(
        uint globalFrameno, int nBones, int bufferLength, out int offset)
    {
        offset = 0;
        if (nBones <= 0)
        {
            return false;
        }

        long index = (long)globalFrameno * nBones;
        if (index < 0 || index + nBones > bufferLength)
        {
            return false;
        }

        offset = (int)index;
        return true;
    }


    public void UseBakedAnimationsFrom(ModelAnimation o)
    {
        if (Index != o.Index || Duration != o.Duration || TicksPerSecond != o.TicksPerSecond ||
            FirstFrame != o.FirstFrame || NTicks != o.NTicks || NFrames != o.NFrames)
        {
            ErrorThrow<ArgumentException>("Trying to assign from incomatible animation.");
        }
        CpuFrames = o.CpuFrames; 
    }
        
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