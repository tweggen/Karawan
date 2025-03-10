using System;
using System.Collections.Generic;
using System.Numerics;

namespace engine.joyce;


public struct PositionKey
{
    public float Time; 
    public Vector3 Position;
}


public struct ScalingKey
{
    public float Time; 
    public Vector3 Scaling;
}


public struct RotationKey
{
    public float Time;
    public Quaternion Rotation;
}


public class PositionKeyTimeComparer : IComparer<PositionKey>
{
    public int Compare(PositionKey x, PositionKey y)
    {
        return x.Time.CompareTo(y.Time);
    }
}


public class ScalingKeyTimeComparer : IComparer<ScalingKey>
{
    public int Compare(ScalingKey x, ScalingKey y)
    {
        return x.Time.CompareTo(y.Time);
    }
}


public class RotationKeyTimeComparer : IComparer<RotationKey>
{
    public int Compare(RotationKey x, RotationKey y)
    {
        return x.Time.CompareTo(y.Time);
    }
}


public class ModelAnimChannel
{
    public ModelNode Target;
    public PositionKey[]? Positions;
    public ScalingKey[]? Scalings;
    public RotationKey[]? Rotations;
    
    
    public Vector3 LerpPosition(uint frameno)
    {
        float frametime = 0f;
        PositionKey key = new PositionKey() { Time = frametime };

        var idx = Array.BinarySearch<PositionKey>(
            Positions!, key, new PositionKeyTimeComparer());
        
        return Vector3.Zero;
    }

    
    public Quaternion SlerpRotation(uint frameno)
    {
        return Quaternion.Identity;
    }

    
    public Vector3 LerpScaling(uint frameno)
    {
        return Vector3.One;
    }
    
}