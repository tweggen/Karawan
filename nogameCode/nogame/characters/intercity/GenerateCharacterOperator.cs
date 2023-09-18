using System;
using System.Numerics;
using engine.world;

namespace nogame.characters.intercity;

public class GenerateCharacterOperator : IWorldOperator
{
    public string WorldOperatorGetPath()
    {
        return "nogame/intercity/GenerateCharacterOperator";
    }


    private void _createIntercity(ClusterDesc ca, ClusterDesc cb, float relpos)
    {
        
    }

    public void WorldOperatorApply(MetaGen worldMetaGen)
    {
        /*
         * For every cluster larger than X (600 threshold),
         * Create trams to the closest cities 
         */
        var clusterList = ClusterList.Instance().GetClusterList();
        foreach (ClusterDesc clusterDesc in clusterList)
        {
            int maxNTrams = 1;
            //    Int32.Clamp(0, 2999, (int)clusterDesc.Size - 800)
            //    / (3000/5) + 1;


            var closestClusters = clusterDesc.GetClosest();
            // maxNTrams = Int32.Min(closestClusters.Length, maxNTrams);

            for (int i = 0; i < maxNTrams; ++i)
            {
                _createIntercity(clusterDesc, closestClusters[0], 0.5f);
            }
        }
    }
}