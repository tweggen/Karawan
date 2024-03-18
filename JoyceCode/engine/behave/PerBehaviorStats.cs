using System.Collections.Generic;
using engine.behave;
using engine.joyce;

namespace engine.behave;

public class PerBehaviorStats
{
    public Dictionary<Index3, PerFragmentStats> MapPerFragmentStats = new();
    
    
    public PerFragmentStats FindPerFragmentStats(in Index3 idxFragment)
    {
        PerFragmentStats perFragmentStats;
        if (MapPerFragmentStats.TryGetValue(idxFragment, out perFragmentStats))
        {
            
        }
        else
        {
            perFragmentStats = new PerFragmentStats();
            MapPerFragmentStats[idxFragment] = perFragmentStats;
        }

        return perFragmentStats;
    }
}