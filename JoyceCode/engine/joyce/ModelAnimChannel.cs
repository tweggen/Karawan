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

#if false
    private KeyFrame<Vector3> _lerpVector3(ref KeyFrame<Vector3>[] keyframes, uint frameno)
    {
        if (ModelAnimation.Name.StartsWith("Walk_Male") && (frameno==1 || frameno==ModelAnimation.NFrames-1))
        {
            int a = 1;
        }
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
#endif


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
    
#if false
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
        Quaternion q1 = prevKey.Value;
        Quaternion q2 = nextKey.Value;
        if (Quaternion.Dot(q1, q2) < 0)
        {
            q2 = -q2;
        }
        if (tPrev > tNext)
        {
            tPrev -= animLength;
        }
        float t = (frametime - tPrev) / (tNext - tPrev);
        return new KeyFrame<Quaternion>()
        {
            Time = frametime,
            Value = Quaternion.Slerp(q1, q2, t)
        };

    }
#endif

    public KeyFrame<Vector3> LerpPosition(uint frameno) => _lerpVector3(Positions, frameno);

    
    public KeyFrame<Quaternion> SlerpRotation(uint frameno) => _slerpQuaternion(Rotations, frameno);


    public KeyFrame<Vector3> LerpScaling(uint frameno) => _lerpVector3(Scalings, frameno);
}