using System.Collections.Generic;
using System.Runtime.CompilerServices;
using engine.joyce;

namespace engine.world;

public struct FragmentVisibility
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint PosKey()
    {
        return ((uint)I & 0xffffu) | ((uint)K) << 16;
    }

    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public engine.joyce.Index3 Pos() 
    {
        return new Index3(this.I, 0, this.K);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(FragmentVisibility a, FragmentVisibility b)
    {
        return a.I == b.I && a.K == b.K && a.How == b.How;
    }

    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(FragmentVisibility a, FragmentVisibility b)
    {
        return a.I != b.I || a.K != b.K || a.How != b.How;
    }
    
    
    private class _posComparer : IComparer<FragmentVisibility>
    {
        public int Compare(FragmentVisibility a, FragmentVisibility b)
        {
            return (int)a.PosKey() - (int)b.PosKey();
        }
    }
    
    
    public static readonly byte Visible3dNow = 1;
    public static readonly byte Visible3dPredicted = 2;
    public static readonly byte Visible3dAny = 3;
    public static readonly byte Visible2dNow = 8;
    public static readonly byte Visible2dPredicted = 16;
    public static readonly byte Visible2dAny = 24;
    public static readonly byte VisibleAny = 27;
    
    public static IComparer<FragmentVisibility> PosComparer = new _posComparer(); 
    
    public short I, K;
    public byte How;
    public byte res0, res1, res2;
    
    public override string ToString()
    {
        return $"FragmentVisibility {{ i: {I}, k: {K}, how: {How} }}";
    }
}


