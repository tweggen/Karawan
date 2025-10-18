using System;
using engine.joyce;
using engine.joyce.components;

namespace Splash;

/**
 * Helper that is used in the mesh batch.
 * Defines the mergability of an animation entry.
 * Two animation renderings might be merged if they share the same
 * animation and frame number, depending on the underlying renderer.
 */
public struct AnimationsBatchKey : IEquatable<AnimationsBatchKey>
{
    public AAnimationsEntry AAnimationsEntry;
    public ModelAnimation? ModelAnimation;
    public uint FrameNumber;
    
    public bool Equals(AnimationsBatchKey other)
    {
        return AAnimationsEntry.Model == other.AAnimationsEntry.Model
               && ModelAnimation == other.ModelAnimation
               && FrameNumber == other.FrameNumber
            ;
    }

    public override bool Equals(object obj)
    {
        return obj is AnimationsBatchKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return 
            ((AAnimationsEntry != null) ? (AAnimationsEntry.GetHashCode()) : 0)
            + (int)FrameNumber 
            + ((ModelAnimation!=null)?ModelAnimation.GetHashCode():1);
    }

    public AnimationsBatchKey(
        AAnimationsEntry aAnimationsEntry, 
        in ModelAnimation modelAnimation, 
        uint frameno)
    {
        AAnimationsEntry = aAnimationsEntry;
        ModelAnimation = modelAnimation;
        FrameNumber = frameno;
    }
} 
