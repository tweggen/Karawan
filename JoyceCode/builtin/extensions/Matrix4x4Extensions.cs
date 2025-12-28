using System.Numerics;

namespace builtin.extensions;

public class Matrix4x4Extensions
{
    public static Matrix4x4 CreateFromUnitAxis(in Vector3 vuX, in Vector3 vuY, in Vector3 vuZ)
    {
        return new Matrix4x4(
            vuX.X, vuX.Y, vuX.Z, 0f, 
            vuY.X, vuY.Y, vuY.Z, 0f, 
            vuZ.X, vuZ.Y, vuZ.Z, 0f, 
            0f, 0f, 0f, 1f);
    }
}