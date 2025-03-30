using System;
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
    public AnimationState CAnimationState;
    
    public bool Equals(AnimationsBatchKey other)
    {
        return AAnimationsEntry.Model == other.AAnimationsEntry.Model
               && CAnimationState.ModelAnimation == other.CAnimationState.ModelAnimation
               && CAnimationState.ModelAnimationFrame == other.CAnimationState.ModelAnimationFrame
            ;
    }

    public override bool Equals(object obj)
    {
        return obj is AnimationsBatchKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return (AAnimationsEntry != null ? (AAnimationsEntry.GetHashCode() * CAnimationState.GetHashCode()) : 0);
    }

    public AnimationsBatchKey(AAnimationsEntry aAnimationsEntry, in AnimationState cAnimationState)
    {
        AAnimationsEntry = aAnimationsEntry;
        CAnimationState = cAnimationState;
    }
} 
