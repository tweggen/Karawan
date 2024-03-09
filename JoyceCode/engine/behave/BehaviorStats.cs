using System;
using System.Collections.Generic;

namespace engine.behave;

public class BehaviorStats
{
    public Dictionary<Type, PerBehaviorStats> MapPerBehaviorStats = new();

    public PerBehaviorStats FindPerBehaviorStats(Type behavior)
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