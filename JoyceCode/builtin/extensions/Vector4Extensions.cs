using System.Numerics;

namespace builtin.extensions;

public static class Vector4Extensions
{
    public static uint ToRGBA32(this Vector4 v4)
    {
        return
            (((uint)((v4.X) * 255f)) << 0)
            |
            (((uint)((v4.Y) * 255f)) << 8)
            |
            (((uint)((v4.Z) * 255f)) << 16)
            |
            (((uint)((v4.W) * 255f)) << 24)
            ;
    }
}