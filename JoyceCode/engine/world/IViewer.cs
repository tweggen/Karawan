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



/**
 * A viewer is an abstraction that defines what a particular
 * loader is required to have loaded at a given instance.
 *
 * To be more precise:
 * The world is divided into fragment sized pieces. This
 * viewer emits a list of fragments and their visibility
 * properties.
 *
 * A loader then is responsible to make the data available
 * as described by the viewer.
 *
 * The "real" 3d world, the world maps and the local maps
 * have different viewers describing the respective requirements.
 *
 */
public interface IViewer
{
    public void GetVisibleFragments(ref IList<FragmentVisibility> lsVisib);
}