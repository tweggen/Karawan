using System;
using System.Numerics;
using engine.elevation;
using engine.world;
using Joyce.builtin.tools;

namespace nogame.characters.intercity;

public class GenerateCharacterOperator : IWorldOperator
{
    private engine.Engine _engine;
    
    public string WorldOperatorGetPath()
    {
        return "nogame/intercity/GenerateCharacterOperator";
    }
    
    
    private void _createIntercity(ClusterDesc ca, ClusterDesc cb, float relpos)
    {
        Vector3 vuAB = Vector3.Normalize(cb.Pos - ca.Pos);
        Vector3 vuUp = new Vector3(0f, 1f, 0f);
        SegmentEnd sa = new()
        {
            Position = ca.Pos,
            Up = vuUp,
            Right = Vector3.Cross(vuAB, vuUp)
        };
        SegmentEnd sb = new()
        {
            Position = cb.Pos,
            Up = vuUp,
            Right = Vector3.Cross(-vuAB, vuUp)
        };
        SegmentNavigator segnav = new SegmentNavigator(sa, sb) 
            { Speed = 60f };
        
        int tramIdx = 0;
        engine.joyce.InstanceDesc jInstanceDesc = 
            characters.tram.GenerateCharacterOperator.GetTramMesh(tramIdx);

        var tSetupEntity = new Action<DefaultEcs.Entity>((DefaultEcs.Entity eTarget) =>
        {
            eTarget.Set(new engine.joyce.components.Instance3(jInstanceDesc));
            eTarget.Set(new engine.behave.components.Behavior(
                new builtin.tools.SimpleNavigationBehavior(_engine, segnav))
                {
                    /*
                     * This means, the behavior always is called.
                     */
                    MaxDistance = MetaGen.MaxWidth
                }
            );
            eTarget.Set(new engine.audio.components.MovingSound(
                nogame.characters.tram.GenerateCharacterOperator.GetTramSound(), 
                300f));
        });

        _engine.QueueEntitySetupAction("nogame.characters.intercity", tSetupEntity);
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

    public GenerateCharacterOperator(engine.Engine engine0)
    {
        _engine = engine0;
    }
    
}