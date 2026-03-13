using System;
using System.Collections.Generic;

namespace engine.tale;

public interface IEventLogger
{
    bool WantsDaySummary => true;
    void LogNpcCreated(int npcId, int seed, string role, int homeLocationId,
        int workplaceLocationId, List<int> socialVenues,
        Dictionary<string, float> props, DateTime gameTime);

    void LogNodeArrival(int npcId, string storylet, int locationId, string locationType,
        DateTime gameTime, int day, Dictionary<string, float> props,
        Dictionary<string, float> deltas);

    void LogEncounter(int npcA, int npcB, string interactionType, int locationId,
        string locationType, DateTime gameTime, int day,
        float trustBefore, float trustAfter);

    void LogRelationshipChanged(int npcA, int npcB, string oldTier, string newTier,
        float trust, int interactionCount, DateTime gameTime, int day);

    void LogDaySummary(int npcId, int day, int storyletsCompleted, int encounters,
        Dictionary<string, float> props, Dictionary<int, float> topRelationships);

    void Flush();
}


public class NullEventLogger : IEventLogger
{
    public bool WantsDaySummary => false;
    public void LogNpcCreated(int npcId, int seed, string role, int homeLocationId,
        int workplaceLocationId, List<int> socialVenues,
        Dictionary<string, float> props, DateTime gameTime) { }

    public void LogNodeArrival(int npcId, string storylet, int locationId, string locationType,
        DateTime gameTime, int day, Dictionary<string, float> props,
        Dictionary<string, float> deltas) { }

    public void LogEncounter(int npcA, int npcB, string interactionType, int locationId,
        string locationType, DateTime gameTime, int day,
        float trustBefore, float trustAfter) { }

    public void LogRelationshipChanged(int npcA, int npcB, string oldTier, string newTier,
        float trust, int interactionCount, DateTime gameTime, int day) { }

    public void LogDaySummary(int npcId, int day, int storyletsCompleted, int encounters,
        Dictionary<string, float> props, Dictionary<int, float> topRelationships) { }

    public void Flush() { }
}
