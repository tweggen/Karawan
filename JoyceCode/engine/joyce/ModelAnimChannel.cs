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


    private Vector3 _lerpVector3(ref KeyFrame<Vector3>[] keyframes, uint frameno)
    {
        float frametime = Single.Max(frameno / 60f, ModelAnimation.Duration);
        var key = new KeyFrame<Vector3>() { Time = frametime };

        var idx = Array.BinarySearch<KeyFrame<Vector3>>(keyframes, key, new KeyFrameTimeComparer<Vector3>());
        if (idx <= 0)
        {
            return keyframes[0].Value;
        }
        else if (idx == keyframes.Length)
        {
            return keyframes[idx - 1].Value;
        }
        else
        {
            ref KeyFrame<Vector3> prevKey = ref keyframes[idx - 1];
            ref KeyFrame<Vector3> nextKey = ref keyframes[idx];
            float t = (frametime - prevKey.Time) / (nextKey.Time - prevKey.Time);
            return Vector3.Lerp(prevKey.Value, nextKey.Value, t);
        }
    }


    private Quaternion _slerpQuaternion(ref KeyFrame<Quaternion>[] keyframes, uint frameno)
    {
        float frametime = Single.Max(frameno / 60f, ModelAnimation.Duration);
        var key = new KeyFrame<Quaternion>() { Time = frametime };

        var idx = Array.BinarySearch<KeyFrame<Quaternion>>(keyframes, key, new KeyFrameTimeComparer<Quaternion>());
        if (idx <= 0)
        {
            return keyframes[0].Value;
        }
        else if (idx == keyframes.Length)
        {
            return keyframes[idx - 1].Value;
        }
        else
        {
            ref KeyFrame<Quaternion> prevKey = ref keyframes[idx - 1];
            ref KeyFrame<Quaternion> nextKey = ref keyframes[idx];
            float t = (frametime - prevKey.Time) / (nextKey.Time - prevKey.Time);
            return Quaternion.Slerp(prevKey.Value, nextKey.Value, t);
        }
    }


    public Vector3 LerpPosition(uint frameno) => _lerpVector3(ref Positions, frameno);

    
    public Quaternion SlerpRotation(uint frameno) => _slerpQuaternion(ref Rotations, frameno);


    public Vector3 LerpScaling(uint frameno) => _lerpVector3(ref Scalings, frameno);

}