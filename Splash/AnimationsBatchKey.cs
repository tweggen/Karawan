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
    public GPUAnimationState CGpuAnimationState;
    
    public bool Equals(AnimationsBatchKey other)
    {
        return AAnimationsEntry.Model == other.AAnimationsEntry.Model
               && CGpuAnimationState.ModelAnimation == other.CGpuAnimationState.ModelAnimation
               && CGpuAnimationState.ModelAnimationFrame == other.CGpuAnimationState.ModelAnimationFrame
            ;
    }

    public override bool Equals(object obj)
    {
        return obj is AnimationsBatchKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return (AAnimationsEntry != null ? (AAnimationsEntry.GetHashCode() * CGpuAnimationState.GetHashCode()) : 0);
    }

    public AnimationsBatchKey(AAnimationsEntry aAnimationsEntry, in GPUAnimationState cGpuAnimationState)
    {
        AAnimationsEntry = aAnimationsEntry;
        CGpuAnimationState = cGpuAnimationState;
    }
} 
