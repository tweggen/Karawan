using System.Collections.Generic;
using engine.behave;
using engine.joyce;

namespace engine.behave;

public class BehaviorStats
{
    public SortedDictionary<IBehavior, PerBehaviorStats> MapPerBehaviorStats = new();

    public PerBehaviorStats FindPerBehaviorStats(IBehavior behavior)
    {
        PerBehaviorStats perBehaviorStats;
        if (MapPerBehaviorStats.TryGetValue(behavior, out perBehaviorStats))
        {
            
        }
        else
        {
            perBehaviorStats = new PerBehaviorStats();
            MapPerBehaviorStats[behavior] = perBehaviorStats;
        }

        return perBehaviorStats;
    }
}