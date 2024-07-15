using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace builtin;

public class Workarounds
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float V2Dot(in Vector2 a, in Vector2 b)
    {
        return a.X * b.X + a.Y * b.Y;
    }
    

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 V2Normalize(in Vector2 a)
    {
        return a / Single.Sqrt(a.X * a.X + a.Y * a.Y);
    }
        
}