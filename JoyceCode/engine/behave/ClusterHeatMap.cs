using System.Xml;
using engine.behave;
using engine.geom;
using engine.joyce;
using engine.world;

namespace engine.behave;

public class ClusterHeatMap : AHeatMap
{
    private ClusterDesc[,] _arrayClusters;
    private bool _isComputed = false;

    public ClusterDesc GetClusterDesc(in Index3 idxFragment)
    {
        if (!_isComputed)
        {
            _computeDensity(new Index3(0, 0, 0));
        }
        return _arrayClusters[_si/2 + idxFragment.I, _sk/2 + idxFragment.K];
    }
    
    
    /*
     * We compute the entire heat map as soon one element had been queried.
     */
    protected override float _computeDensity(in Index3 idxFragment)
    {
        if (_isComputed) return 0f;
        
        /*
         * First, clear out the entire array, we are only setting the values for the clusters
         * later.
         */
        for (int i = 0; i < _si; ++i)
        {
            for (int k = 0; k < _sk; ++k)
            {
                _arrayDensity[i, k] = 0f;
            }
        }
        
        /*
         * Now add the clusters to the list.
         */
        var clusterList = I.Get<ClusterList>().GetClusterList();
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
                    int fk = idx.K + _sk / 2;
                    
                    /*
                     * Remember the cluster. Yes, the last cluster wins.
                     */
                    _arrayClusters[fi, fk] = cd;

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

                            float oldDensity = _arrayDensity[fi, fk];
                            _arrayDensity[fi, fk] = oldDensity + li * lk;
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

        _isComputed = true;

        return _arrayDensity[_si/2 + idxFragment.I, _si/2 + idxFragment.K];
    }

    public ClusterHeatMap() : base()
    {
        _arrayClusters = new ClusterDesc[_si + 1, _sk + 1];
    }
}