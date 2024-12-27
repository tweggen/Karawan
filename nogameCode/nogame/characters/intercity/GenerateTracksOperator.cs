using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using engine;
using engine.elevation;
using engine.world;
using builtin.tools;

namespace nogame.characters.intercity;

public class GenerateTracksOperator : IWorldOperator
{
    private engine.Engine _engine;
    
    public string WorldOperatorGetPath()
    {
        return "nogame/intercity/GenerateTracksOperator";
    }
    
    
    public Func<Task> WorldOperatorApply() => new(async () =>
    {
        var network = I.Get<nogame.intercity.Network>();
        var lines = network.Lines;

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
    });

    
    public GenerateTracksOperator()
    {
        _engine = I.Get<engine.Engine>();
    }
}