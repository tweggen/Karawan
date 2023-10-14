using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.elevation;
using engine.world;
using Joyce.builtin.tools;

namespace nogame.characters.intercity;

/**
 * This creates the intercity network.
 */
public class GenerateCharacterOperator : IWorldOperator
{
    private engine.Engine _engine;
    
    public string WorldOperatorGetPath()
    {
        return "nogame/intercity/GenerateCharacterOperator";
    }
    
    
    /*
     * Create the actual route of the intercity. This right now will follow
     * the direct connection from A to B, we only compute the height required.
     */
    private void _createIntercity(Vector3 caPos, Vector3 cbPos, float relpos)
    {
        /*
         * Read all data we need to scale our drawing.
         */
        terrain.GroundOperator groundOperator = terrain.GroundOperator.Instance();
        var skeleton = groundOperator.GetSkeleton();
        int skeletonWidth = groundOperator.SkeletonWidth;
        int skeletonHeight = groundOperator.SkeletonHeight;
        
        Vector3 vuAB = Vector3.Normalize(cbPos - caPos);
        Vector3 vuUp = new Vector3(0f, 1f, 0f);

        List<SegmentEnd> listSegments = new()
        {
            new()
            {
                Position = caPos,
                Up = vuUp,
                Right = Vector3.Cross(vuAB, vuUp)
            },
            new()
            {
                Position = cbPos,
                Up = vuUp,
                Right = Vector3.Cross(-vuAB, vuUp)
            }
        };
        
        SegmentNavigator segnav = new SegmentNavigator(listSegments)
        {
            LoopSegments = true,
            Speed = 60f
        };
        
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
#if true
            eTarget.Set(new engine.draw.components.OSDText(
                new Vector2(0, 30f),
                new Vector2(160f, 18f),
                "intercity",
                12,
                0x88226622,
                0x00000000,
                engine.draw.HAlign.Left)
                {
                    MaxDistance = 20000
                }
            );
#endif

        });

        _engine.QueueEntitySetupAction("nogame.characters.intercity", tSetupEntity);
    }

    
    public void WorldOperatorApply(MetaGen worldMetaGen)
    {
        var network = I.Get<nogame.intercity.Network>();
        var lines = network.Lines;

        /*
         * TXWTODO: We should put this code into an intercity module, which we previously load.
         */
        {
            foreach (var line in lines)
            {
                string newkey = line.ToString();
                {
                    var elevationCache = engine.elevation.Cache.Instance();
                    var intercityTrailOperator = new nogame.intercity.IntercityTrackElevationOperator(line, newkey);
                    elevationCache.ElevationCacheRegisterElevationOperator(
                        engine.elevation.Cache.LAYER_BASE + $"/000200/intercityTrails/{newkey}",
                        intercityTrailOperator
                    );
                }
            }
        }
        
        foreach (var line in lines)
        {
            Vector3 caPos = line.StationA.Position with { Y = line.ClusterA.AverageHeight + 20f };
            Vector3 cbPos = line.StationB.Position with { Y = line.ClusterB.AverageHeight + 20f };
            _createIntercity(caPos, cbPos, 0.5f);
        }
    }

    
    public GenerateCharacterOperator(engine.Engine engine0)
    {
        _engine = engine0;
    }
    
}