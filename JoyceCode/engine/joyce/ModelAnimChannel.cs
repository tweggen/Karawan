using System;
using System.Collections.Generic;
using System.Numerics;

namespace engine.joyce;


public struct KeyFrame<T>
{
    public float Time;
    public T Value;
}

public class KeyFrameTimeComparer<T> : IComparer<KeyFrame<T>>
{
    public int Compare(KeyFrame<T> x, KeyFrame<T> y)
    {
        return x.Time.CompareTo(y.Time);
    }
}


public class ModelAnimChannel
{
    public required ModelAnimation ModelAnimation;
    public required ModelNode Target;
    public KeyFrame<Vector3>[]? Positions;
    public KeyFrame<Quaternion>[]? Rotations;
    public KeyFrame<Vector3>[]? Scalings;


    private KeyFrame<Vector3> _lerpVector3(ref KeyFrame<Vector3>[] keyframes, uint frameno)
    {
        int l = keyframes.Length;
        float animLength = ModelAnimation.Duration;
        float frametime = Single.Min(frameno / 60f, ModelAnimation.Duration);
        var key = new KeyFrame<Vector3>() { Time = frametime };

        var idx = Array.BinarySearch<KeyFrame<Vector3>>(keyframes, key, new KeyFrameTimeComparer<Vector3>());
        if (idx <= 0)
        {
            if (idx == 0)
            {
                return keyframes[0];
            }

            /*
             * If we do not have an exact match, idx is the index of the first element that is larger
             * than the key.
             */
            idx = ~idx;
            
            if (idx >= l)
            {
                return keyframes[l - 1];
            }
        }
        else if (idx >= l)
        {
            return keyframes[l-1];
        }

        ref KeyFrame<Vector3> prevKey = ref keyframes[(idx+l - 1) % l];
        ref KeyFrame<Vector3> nextKey = ref keyframes[idx];
        float tPrev = prevKey.Time;
        float tNext = nextKey.Time;
        if (tPrev > tNext)
        {
            tPrev -= animLength;
        }
        float t = (frametime - tPrev) / (tNext - tPrev);
        return new KeyFrame<Vector3>()
            {
                Time = frametime,
                Value = Vector3.Lerp(prevKey.Value, nextKey.Value, t)
            };
    }


    private KeyFrame<Quaternion> _slerpQuaternion(ref KeyFrame<Quaternion>[] keyframes, uint frameno)
    {
        int l = keyframes.Length;
        float animLength = ModelAnimation.Duration;
        float frametime = Single.Min(frameno / 60f, ModelAnimation.Duration);
        var key = new KeyFrame<Quaternion>() { Time = frametime };

        var idx = Array.BinarySearch<KeyFrame<Quaternion>>(keyframes, key, new KeyFrameTimeComparer<Quaternion>());
        if (idx <= 0)
        {
            if (idx == 0)
            {
                return keyframes[0];
            }

            /*
             * If we do not have an exact match, idx is the index of the first element that is larger
             * than the key.
             */
            idx = ~idx;
            
            if (idx >= l)
            {
                return keyframes[l - 1];
            }
        }
        else if (idx >= l)
        {
            return keyframes[l-1];
        }

        ref KeyFrame<Quaternion> prevKey = ref keyframes[(idx+l - 1) % l];
        ref KeyFrame<Quaternion> nextKey = ref keyframes[idx];
        float tPrev = prevKey.Time;
        float tNext = nextKey.Time;
        if (tPrev > tNext)
        {
            tPrev -= animLength;
        }
        float t = (frametime - tPrev) / (tNext - tPrev);
        return new KeyFrame<Quaternion>()
        {
            Time = frametime,
            Value = Quaternion.Slerp(prevKey.Value, nextKey.Value, t)
        };

    }


    public KeyFrame<Vector3> LerpPosition(uint frameno) => _lerpVector3(ref Positions, frameno);

    
    public KeyFrame<Quaternion> SlerpRotation(uint frameno) => _slerpQuaternion(ref Rotations, frameno);


    public KeyFrame<Vector3> LerpScaling(uint frameno) => _lerpVector3(ref Scalings, frameno);
}