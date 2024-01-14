
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace engine.joyce;

public struct Index3
{
    public int I, J, K;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Index3(int i0, int j0, int k0)
    {
        I = i0;
        J = j0;
        K = k0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 AsVector3()
    {
        return new Vector3((float)I, (float)J, (float)K);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Index3(in Vector3 v3)
    {
        I = (int)Single.Floor(v3.X + 0.5f);
        J = (int)Single.Floor(v3.Y + 0.5f);
        K = (int)Single.Floor(v3.Z + 0.5f);
    }
}