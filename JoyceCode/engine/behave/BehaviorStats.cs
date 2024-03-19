using System;
using System.Collections.Generic;

namespace engine.behave;


/**
 * This is the accumulator / state data structure of the SpawnSystem.
 */
public class BehaviorStats
{
    public Dictionary<Type, PerBehaviorStats> MapPerBehaviorStats = new();


    public PerBehaviorStats? GetPerBehaviorStats(Type behaviorType)
    {
        PerBehaviorStats perBehaviorStats = null;
        MapPerBehaviorStats.TryGetValue(behaviorType, out perBehaviorStats);
        return perBehaviorStats;
    }
    
    
    public PerBehaviorStats FindPerBehaviorStats(Type behaviorType)
    {
        PerBehaviorStats perBehaviorStats;
        if (MapPerBehaviorStats.TryGetValue(behaviorType, out perBehaviorStats))
        {
            
        }
        else
        {
            perBehaviorStats = new PerBehaviorStats();
            MapPerBehaviorStats[behaviorType] = perBehaviorStats;
        }

        return perBehaviorStats;
    }
}