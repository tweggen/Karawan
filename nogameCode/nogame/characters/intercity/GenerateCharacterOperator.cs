using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using engine;
using engine.elevation;
using engine.joyce;
using engine.world;
using builtin.tools;

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
    private async void _createIntercity(Vector3 caPos, Vector3 cbPos, float relpos)
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
        
        Model model = await I.Get<ModelCache>().LoadModel( 
            new ModelCacheParams()
            {
                
            
            Url = "tram1.obj", 
            Params = new InstantiateModelParams()
            {
                GeomFlags = 0
                            | InstantiateModelParams.CENTER_X
                            | InstantiateModelParams.CENTER_Z
                            | InstantiateModelParams.ROTATE_Y180
                            | InstantiateModelParams.REQUIRE_ROOT_INSTANCEDESC,
                MaxDistance = 1000f,
            }});
        engine.joyce.InstanceDesc jInstanceDesc = model.RootNode.InstanceDesc;


        var tSetupEntity = new Action<DefaultEcs.Entity>((DefaultEcs.Entity eTarget) =>
        {
            eTarget.Set(new engine.joyce.components.Instance3(jInstanceDesc));
            eTarget.Set(new engine.behave.components.Behavior(
                new builtin.tools.SimpleNavigationBehavior(_engine, segnav))
                {
                    /*
                     * This means, the behavior always is called.
                     */
                    MaxDistance = (short)MetaGen.MaxWidth
                }
            );
            eTarget.Set(new engine.audio.components.MovingSound(
                nogame.characters.tram.GenerateCharacterOperator.GetTramSound(), 
                450f));

        });

        _engine.QueueEntitySetupAction("nogame.characters.intercity", tSetupEntity);
    }


    public Func<Task> WorldOperatorApply() => new(async () =>
    {
        var network = I.Get<nogame.intercity.Network>();
        var lines = network.Lines;

        foreach (var line in lines)
        {
            Vector3 caPos = line.StationA.Position with { Y = line.ClusterA.AverageHeight + 20f };
            Vector3 cbPos = line.StationB.Position with { Y = line.ClusterB.AverageHeight + 20f };
            _createIntercity(caPos, cbPos, 0.5f);
        }
    });

    
    public GenerateCharacterOperator()
    {
        _engine = I.Get<engine.Engine>();
    }
    
}