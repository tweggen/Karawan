using System.Numerics;

namespace engine.geom;

public class SerializableQuaternion
{
    static public SerializableQuaternion Identity = new (0f, 0f, 0f, 1f);
    public float X { get; set; } = 0f;
    public float Y { get; set; } = 0f;
    public float Z { get; set; } = 0f;
    public float W { get; set; } = 0f;
    
    public static implicit operator Quaternion(SerializableQuaternion sq) => new(sq.X, sq.Y, sq.Z, sq.W);
    public static explicit operator SerializableQuaternion(in Quaternion q) => new (q);
    
    public SerializableQuaternion(in Quaternion q)
    {
        X = q.X;
        Y = q.Y;
        Z = q.Z;
        W = q.W;
    }

    public SerializableQuaternion(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public SerializableQuaternion()
    {
    }
}