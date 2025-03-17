using System.Numerics;

namespace builtin.extensions;

public class MatrixInversion
{
    static public Matrix4x4 Invert(in Matrix4x4 m)
    {
        Matrix4x4.Decompose(m, out var v3Scale, out var q4Rotation, out var v3Translation);
        if (v3Scale.X == 0f || v3Scale.Y == 0f || v3Scale.Z == 0f) v3Scale = Vector3.One;
                    
        return
            Matrix4x4.CreateScale(new Vector3(1f / v3Scale.X, 1f / v3Scale.Y, 1f / v3Scale.Z))
            * Matrix4x4.CreateFromQuaternion(Quaternion.Inverse(q4Rotation))
            * Matrix4x4.CreateTranslation(-v3Translation);
    }
}