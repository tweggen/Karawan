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
        int sx = (int)(world.MetaGen.MaxWidth / world.MetaGen.FragmentSize);
        int sy = (int)(world.MetaGen.MaxHeight / world.MetaGen.FragmentSize);
        for (int i = 0; i < sx; ++i)
        {
            for (int k = 0; k < sy; ++k)
            {
                _arrayDensity[i, k] = -1f;
            }
        }

        var clusterList = ClusterList.Instance().GetClusterList();
        foreach (var cd in clusterList)
        {
            AABB aabbCluster = cd.AABB;
            Index3 fragMin = new(
                int.Clamp((int) (aabbCluster.AA.X / world.MetaGen.FragmentSize), -sx, sx),
                0,
                int.Clamp((int)(aabbCluster.AA.Z / world.MetaGen.FragmentSize),-sy, sy)
                );
            Index3 fragMax = new(
                int.Clamp((int)((aabbCluster.BB.X+world.MetaGen.FragmentSize-1f) / world.MetaGen.FragmentSize), -sx, sx),
                0,
                int.Clamp((int)((aabbCluster.BB.Z+world.MetaGen.FragmentSize-1f) / world.MetaGen.FragmentSize), -sy, sy)
            );

            for (int fi = fragMin.I; fi <= fragMax.I; ++fi)
            {
                for (int fk = fragMin.K; fk <= fragMax.K; ++fk)
                {
                    /*
                     * Compute fragment coverage if required. 
                     */
                    if (fi == fragMin.I || fi == fragMax.I || fk == fragMin.K || fk == fragMax.K)
                    {
                        
                    }
                    else
                    {
                        _arrayDensity[fi, fk] = 1f;
                    }
                }
            }
            
        }

    }

    public ClusterHeatMap() : base()
    {
    }
}