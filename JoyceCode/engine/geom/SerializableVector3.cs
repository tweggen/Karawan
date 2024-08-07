using System.Numerics;

namespace engine.geom;

public class SerializableVector3
{
    public static SerializableVector3 Zero = new (0f, 0f, 0f);
    public float X { get; set; } = 0f;
    public float Y { get; set; } = 0f;
    public float Z { get; set; } = 0f;

    
    public static implicit operator Vector3(SerializableVector3 sv3) => new(sv3.X, sv3.Y, sv3.Z);
    public static explicit operator SerializableVector3(in Vector3 v3) => new (v3);
    
    public SerializableVector3(in Vector3 v3)
    {
        X = v3.X;
        Y = v3.Y;
        Z = v3.Z;
    }

    public SerializableVector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public SerializableVector3()
    {
    }
}