using System;
using System.Collections.Generic;

namespace engine.tale.conditions;

/// <summary>
/// Trust-based interaction condition.
/// Matches if NPCs' trust level falls within [minTrust, maxTrust) range and random roll succeeds.
/// Used for: greet, chat, trade, help.
/// </summary>
public class TrustBasedCondition : IInteractionCondition
{
    private float _minTrust;
    private float _maxTrust;
    private float _probability;

    public void Initialize(Dictionary<string, float> parameters)
    {
        _minTrust = parameters.GetValueOrDefault("minTrust", 0f);
        _maxTrust = parameters.GetValueOrDefault("maxTrust", 1f);
        _probability = parameters.GetValueOrDefault("probability", 1f);
    }

    public bool Evaluate(NpcSchedule npcA, NpcSchedule npcB, float trust, Random rng)
    {
        if (trust < _minTrust || trust >= _maxTrust) return false;
        if (rng.NextDouble() > _probability) return false;
        return true;
    }
}
