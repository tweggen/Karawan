using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace engine.joyce;

public struct Index2
{
    public int I, J;

    public override string ToString() => $"({I}, {J})";

    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator!= (Index2 obj1, Index2 obj2)
    {
        return !(
            obj1.I == obj2.I 
            && obj1.J == obj2.J 
        );
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator== (Index2 obj1, Index2 obj2)
    {
        return !(
            obj1.I == obj2.I 
            && obj1.J == obj2.J 
        );
    }
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 AsVector3()
    {
        return new Vector2((float)I, (float)J);
    }
    
    
    public uint AsKey()
    {
        return ((uint)I & 0xffffu) | ((uint)J) << 16;
    }
    

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Index2(in Vector2 v2)
    {
        I = (int)Single.Floor(v2.X + 0.5f);
        J = (int)Single.Floor(v2.Y + 0.5f);
    }
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Index2(int i0, int j0)
    {
        I = i0;
        J = j0;
    }
}