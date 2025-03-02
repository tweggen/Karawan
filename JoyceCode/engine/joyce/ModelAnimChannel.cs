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


public class ModelAnimChannel
{
    public ModelNode Target;
    public PositionKey[]? Positions;
    public ScalingKey[]? Scalings;
    public RotationKey[]? Rotations;
}