using System.Xml;
using engine.behave;
using engine.geom;
using engine.joyce;
using engine.world;

namespace engine.behave;

public class ClusterHeatMap : AHeatMap
{
    /*
     * We compute the entire heat map as soon one element had been queried.
     */
    protected override float _computeDensity(in Index3 idxFragment)
    {
        /*
         * First clear the array. Then iterate through all clusters,
         * summing up the cluster partitions to a total of 10000 == 100%.
         */
        // _arraySpawnStatus.Fill<SpawnStatus>(emptySpawnStatus); not possible for 2d arrays
        
        for (int i = 0; i < _si; ++i)
        {
            for (int k = 0; k < _sk; ++k)
            {
                _arrayDensity[i, k] = -1f;
            }
        }

        var clusterList = ClusterList.Instance().GetClusterList();
        foreach (var cd in clusterList)
        {
            AABB aabbCluster = cd.AABB;
            Index3 fragMin = new(
                int.Clamp((int) (aabbCluster.AA.X / world.MetaGen.FragmentSize), -_si, _si),
                0,
                int.Clamp((int)(aabbCluster.AA.Z / world.MetaGen.FragmentSize),-_sk, _sk)
                );
            Index3 fragMax = new(
                int.Clamp((int)((aabbCluster.BB.X+world.MetaGen.FragmentSize-1f) / world.MetaGen.FragmentSize), -_si, _si),
                0,
                int.Clamp((int)((aabbCluster.BB.Z+world.MetaGen.FragmentSize-1f) / world.MetaGen.FragmentSize), -_sk, _sk)
                );

            Index3 idx = new(0,0,0);
            for (idx.I = fragMin.I; idx.I <= fragMax.I; ++idx.I)
            {
                for (idx.K = fragMin.K; idx.K <= fragMax.K; ++idx.K)
                {
                    int fi = idx.I + _si / 2;
                    int fk = idx.I + _sk / 2;
                    
                    /*
                     * Compute fragment coverage if required.
                     */
                    if (idx.I == fragMin.I || idx.I == fragMax.I || idx.K == fragMin.K || idx.K == fragMax.K)
                    {
                        AABB aabbIntersection;
                        if (aabbCluster.TryIntersect(Fragment.GetAABB(idx), out aabbIntersection))
                        {
                            float li = (aabbIntersection.BB.X - aabbIntersection.AA.X) / world.MetaGen.FragmentSize;
                            float lk = (aabbIntersection.BB.Z - aabbIntersection.AA.Z) / world.MetaGen.FragmentSize;

                            _arrayDensity[fi, fk] += li * lk;
                        }
                        else
                        {
                            // do nothing, no increment to density.
                        }
                    }
                    else
                    {
                        _arrayDensity[fi, fk] = 1f;
                    }
                }
            }
        }

        return _arrayDensity[_si + idxFragment.I, _si + idxFragment.K];
    }

    
    public ClusterHeatMap() : base()
    {
    }
}