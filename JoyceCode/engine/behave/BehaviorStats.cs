using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine.behave;


/**
 * This is the accumulator / state data structure of the SpawnModule.
 *
 * It constantly is udpated to keep track of the entities found in the various
 * segments.
 */
public class BehaviorStats
{
    public Dictionary<Type, PerBehaviorStats> MapPerBehaviorStats = new();

    public void ClearPerFrame()
    {
        foreach (var kvpFrag in MapPerBehaviorStats)
        {
            kvpFrag.Value.ClearPerFrame();
        }
    }

    
    public PerBehaviorStats? GetPerBehaviorStats(Type behaviorType)
    {
        PerBehaviorStats perBehaviorStats = null;
        if (MapPerBehaviorStats.TryGetValue(behaviorType, out perBehaviorStats))
        {
        }
        return perBehaviorStats;
    }
    
    
    public PerBehaviorStats FindPerBehaviorStats(ISpawnOperator spawnOperator, Type behaviorType)
    {
        PerBehaviorStats perBehaviorStats;
        if (MapPerBehaviorStats.TryGetValue(behaviorType, out perBehaviorStats))
        {
            // Trace($"Added behavior stats for type {behaviorType.FullName}");
        }
        else
        {
            perBehaviorStats = new PerBehaviorStats()
            {
                SpawnOperator = spawnOperator
            };
            MapPerBehaviorStats[behaviorType] = perBehaviorStats;
        }

        return perBehaviorStats;
    }
}