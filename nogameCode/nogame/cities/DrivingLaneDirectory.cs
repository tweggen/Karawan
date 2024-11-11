using System.Collections.Generic;
using System.Linq;
using engine;
using engine.world.components;

namespace nogame.cities;

public class DrivingLaneDirectory : ObjectFactory<int, DrivingStrokeProperties>
{
    /**
     * Remove all driving information but for the given cluster.
     */
    public void Cleanup(int clusterKeepId) => RemoveIf((strokeId, _) => (strokeId >> 16) != clusterKeepId);
}