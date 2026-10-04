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
    private async void _createIntercity(Vector3 caPos, Vector3 cbPos, float trackHeight)
    {
        /*
         * Both ends take the track's own height plus the vehicle clearance, so the
         * vehicle rides its own track instead of flying a chord between two cities'
         * average heights - which put it a median 44 m over the ribbon it runs on.
         */
        SegmentRoute sr = engine.world.IntercityLine.RouteBetween(caPos, cbPos, trackHeight);

        SegmentNavigator segnav = new SegmentNavigator()
        {
            SegmentRoute = sr,
            Speed = 60f
        };

        var mcp = new ModelCacheParams()
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
            }
        };
        Model model = await I.Get<ModelCache>().LoadModel(mcp); 
            

        var tSetupEntity = new Action<DefaultEcs.Entity>((DefaultEcs.Entity eTarget) =>
        {
            eTarget.Set(new engine.behave.components.Behavior(
                new builtin.tools.SimpleNavigationBehavior()
                {
                    Navigator = segnav
                }) {
                    /*
                     * This means, the behavior always is called.
                     */
                    MaxDistance = (short)MetaGen.MaxWidth
                }
            );
            
            eTarget.Set(new engine.audio.components.MovingSound(
                nogame.characters.tram.GenerateCharacterOperator.GetTramSound(), 
                450f));
            
            I.Get<ModelCache>().BuildPerInstance(eTarget, model, mcp);
        });

        _engine.QueueEntitySetupAction("nogame.characters.intercity", tSetupEntity);
    }


    public Func<Task> WorldOperatorApply() => new(async () =>
    {
        var network = I.Get<nogame.intercity.Network>();
        var lines = network.Lines;

        foreach (var line in lines)
        {
            _createIntercity(line.StationA.Position, line.StationB.Position, line.Height);
        }
    });

    
    public GenerateCharacterOperator()
    {
        _engine = I.Get<engine.Engine>();
    }
    
}