using System;
using System.Collections.Generic;
using engine.world;

namespace Testbed;

public class ClusterViewer : IViewer
{
    private ClusterDesc _cluster;

    public void GetVisibleFragments(ref IList<FragmentVisibility> lsVisib)
    {
        var aabb = _cluster.AABB;
        int iMin = (int)MathF.Floor(aabb.AA.X / MetaGen.FragmentSize);
        int iMax = (int)MathF.Ceiling(aabb.BB.X / MetaGen.FragmentSize);
        int kMin = (int)MathF.Floor(aabb.AA.Z / MetaGen.FragmentSize);
        int kMax = (int)MathF.Ceiling(aabb.BB.Z / MetaGen.FragmentSize);

        for (int k = kMin; k <= kMax; k++)
        {
            for (int i = iMin; i <= iMax; i++)
            {
                lsVisib.Add(new()
                {
                    How = (byte)(FragmentVisibility.Visible3dNow),
                    I = (short)i,
                    K = (short)k
                });
            }
        }
    }

    public ClusterViewer(ClusterDesc cluster)
    {
        _cluster = cluster;
    }
}
