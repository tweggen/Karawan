using System;
using System.Collections.Generic;

namespace engine.tale;

public class RelationshipState
{
    public float TrustAtoB;
    public float TrustBtoA;
    public int TotalInteractions;
    public Dictionary<string, int> InteractionsByType = new();
    public int FirstInteractionDay;
    public int LastInteractionDay;
}

public class RelationshipTracker
{
    private readonly Dictionary<long, RelationshipState> _relationships = new();

    public IReadOnlyDictionary<long, RelationshipState> AllRelationships => _relationships;


    public static long PairKey(int a, int b)
    {
        int min = Math.Min(a, b);
        int max = Math.Max(a, b);
        return ((long)min << 32) | (uint)max;
    }


    public RelationshipState GetOrCreate(int a, int b)
    {
        long key = PairKey(a, b);
        if (!_relationships.TryGetValue(key, out var state))
        {
            state = new RelationshipState();
            _relationships[key] = state;
        }
        return state;
    }


    public float GetTrust(int from, int to)
    {
        long key = PairKey(from, to);
        if (!_relationships.TryGetValue(key, out var state)) return 0f;
        return from < to ? state.TrustAtoB : state.TrustBtoA;
    }


    /// <summary>
    /// Determine interaction type based on trust, NPC properties, and roles.
    /// Evaluates registered interaction conditions in priority order.
    /// Falls back to "greet" if no condition matches.
    /// </summary>
    public string DetermineInteractionType(float trust, NpcSchedule npcA, NpcSchedule npcB, Random rng)
    {
        var registry = I.Get<InteractionTypeRegistry>();
        var matched = registry.EvaluateConditions(npcA, npcB, trust, rng);
        return matched ?? "greet";
    }


    /// <summary>
    /// Legacy overload for backward compatibility.
    /// </summary>
    public string DetermineInteractionType(float trust, float angerA, float angerB, Random rng)
    {
        if ((angerA > 0.5f || angerB > 0.5f) && trust < 0.3f && rng.NextDouble() < 0.3)
            return "argue";

        double roll = rng.NextDouble();
        if (trust < 0.2f) return "greet";
        if (trust < 0.5f) return roll < 0.4 ? "greet" : "chat";
        if (trust < 0.8f) return roll < 0.3 ? "chat" : roll < 0.6 ? "trade" : "help";
        return roll < 0.3 ? "trade" : roll < 0.8 ? "help" : "chat";
    }


    public float TrustDelta(string interactionType)
    {
        var registry = I.Get<InteractionTypeRegistry>();
        return registry.GetTrustDelta(interactionType);
    }


    /// <summary>
    /// Record an interaction and update trust. Returns (oldTrust, newTrust) for the initiating NPC.
    /// </summary>
    public (float oldTrust, float newTrust, string oldTier, string newTier) RecordInteraction(
        int npcA, int npcB, string type, int day)
    {
        var state = GetOrCreate(npcA, npcB);
        float delta = TrustDelta(type);

        // Use average trust for tier tracking
        float avgOld = (state.TrustAtoB + state.TrustBtoA) / 2f;
        string oldTier = TierFromTrust(avgOld);

        // Update trust bidirectionally
        state.TrustAtoB = Math.Clamp(state.TrustAtoB + delta, 0f, 1f);
        state.TrustBtoA = Math.Clamp(state.TrustBtoA + delta, 0f, 1f);

        state.TotalInteractions++;
        state.InteractionsByType.TryGetValue(type, out int count);
        state.InteractionsByType[type] = count + 1;
        if (state.TotalInteractions == 1)
            state.FirstInteractionDay = day;
        state.LastInteractionDay = day;

        float avgNew = (state.TrustAtoB + state.TrustBtoA) / 2f;
        string newTier = TierFromTrust(avgNew);

        return (avgOld, avgNew, oldTier, newTier);
    }


    public static string TierFromTrust(float trust)
    {
        // Lowered thresholds for testing: 0.15, 0.4, 0.7 (was: 0.2, 0.5, 0.8)
        // This makes relationship tier changes more frequent in generic simulations
        if (trust < 0.15f) return "stranger";
        if (trust < 0.4f) return "acquaintance";
        if (trust < 0.7f) return "friend";
        return "ally";
    }


    /// <summary>
    /// Get top N relationships by trust for a given NPC.
    /// </summary>
    public Dictionary<int, float> GetTopRelationships(int npcId, int maxCount = 10)
    {
        var result = new Dictionary<int, float>();
        foreach (var (key, state) in _relationships)
        {
            int a = (int)(key >> 32);
            int b = (int)(key & 0xFFFFFFFF);
            if (a == npcId)
                result[b] = state.TrustAtoB;
            else if (b == npcId)
                result[a] = state.TrustBtoA;
        }

        if (result.Count <= maxCount) return result;

        // Keep only top N by trust
        var sorted = new List<KeyValuePair<int, float>>(result);
        sorted.Sort((x, y) => y.Value.CompareTo(x.Value));
        var top = new Dictionary<int, float>();
        for (int i = 0; i < maxCount && i < sorted.Count; i++)
            top[sorted[i].Key] = sorted[i].Value;
        return top;
    }
}
