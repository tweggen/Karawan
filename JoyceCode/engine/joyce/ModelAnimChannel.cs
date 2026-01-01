using System;
using System.Collections.Generic;
using System.Numerics;

namespace engine.joyce;


public struct KeyFrame<T>
{
    public float Time;
    public double OrgTime;
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

    
    private KeyFrame<Vector3> _lerpVector3 /* Looping */(
        KeyFrame<Vector3>[] keyframes,
        uint frameNo)
    {
        float fps = 60f;
        int count = keyframes.Length;
        float animLength = ModelAnimation.Duration;

        // Convert frame number to time and wrap explicitly.
        float frametime = (frameNo / fps) % animLength;

        // Special case: exact zero → return first key
        if (frametime == 0f)
            return keyframes[0];

        // Create a search key
        var searchKey = new KeyFrame<Vector3>() { Time = frametime };

        // Binary search for the first key >= frametime
        int idx = Array.BinarySearch(keyframes, searchKey, new KeyFrameTimeComparer<Vector3>());

        if (idx < 0)
            idx = ~idx; // first key with time > frametime

        // Wrap around if we hit the end
        if (idx == count)
            idx = 0;

        int prevIdx = (idx - 1 + count) % count;

        ref KeyFrame<Vector3> prev = ref keyframes[prevIdx];
        ref KeyFrame<Vector3> next = ref keyframes[idx];

        float tPrev = prev.Time;
        float tNext = next.Time;

        // Handle wrap-around case: next is at time 0
        if (tNext <= tPrev)
            tNext += animLength;

        float t = (frametime - tPrev) / (tNext - tPrev);

        return new KeyFrame<Vector3>()
        {
            Time = frametime,
            Value = Vector3.Lerp(prev.Value, next.Value, t)
        };
    }


    private KeyFrame<Quaternion> _slerpQuaternion /* Looping */(
        KeyFrame<Quaternion>[] keyframes,
        uint frameNo)
    {
        float fps = 60f;
        int count = keyframes.Length;
        float animLength = ModelAnimation.Duration;

        // Convert frame number to time and wrap explicitly.
        float frametime = (frameNo / fps) % animLength;

        // Special case: exact zero → return first key
        if (frametime == 0f)
            return keyframes[0];

        // Create a search key
        var searchKey = new KeyFrame<Quaternion>() { Time = frametime };

        // Binary search for the first key >= frametime
        int idx = Array.BinarySearch(keyframes, searchKey, new KeyFrameTimeComparer<Quaternion>());

        if (idx < 0)
            idx = ~idx; // first key with time > frametime

        // Wrap around if we hit the end
        if (idx == count)
            idx = 0;

        int prevIdx = (idx - 1 + count) % count;

        ref KeyFrame<Quaternion> prev = ref keyframes[prevIdx];
        ref KeyFrame<Quaternion> next = ref keyframes[idx];

        float tPrev = prev.Time;
        float tNext = next.Time;

        // Handle wrap-around case: next is at time 0
        if (tNext <= tPrev)
            tNext += animLength;

        float t = (frametime - tPrev) / (tNext - tPrev);

        Quaternion q1 = prev.Value;
        Quaternion q2 = next.Value;
        if (Quaternion.Dot(q1, q2) < 0)
        {
            q2 = -q2;
        }

        return new KeyFrame<Quaternion>()
        {
            Time = frametime,
            Value = Quaternion.Slerp(q1, q2, t)
        };
    }
    

    public KeyFrame<Vector3> LerpPosition(uint frameno) => _lerpVector3(Positions, frameno);

    
    public KeyFrame<Quaternion> SlerpRotation(uint frameno) => _slerpQuaternion(Rotations, frameno);


    public KeyFrame<Vector3> LerpScaling(uint frameno) => _lerpVector3(Scalings, frameno);
}