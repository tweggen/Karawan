using System;
using System.Collections.Generic;

namespace engine.tale.conditions;

/// <summary>
/// Authority patrol condition.
/// Matches if NPC has Authority role, target has low reputation, and trust is low.
/// Used for: patrol_check.
/// </summary>
public class AuthorityCondition : IInteractionCondition
{
    private float _maxReputation;
    private float _maxTrust;
    private float _probability;

    public void Initialize(Dictionary<string, float> parameters)
    {
        _maxReputation = parameters.GetValueOrDefault("maxReputation", 0.2f);
        _maxTrust = parameters.GetValueOrDefault("maxTrust", 0.2f);
        _probability = parameters.GetValueOrDefault("probability", 0.4f);
    }

    public bool Evaluate(NpcSchedule npcA, NpcSchedule npcB, float trust, Random rng)
    {
        var registry = I.Get<RoleRegistry>();
        var roleDefA = registry.Get(npcA.Role.ToLowerInvariant());
        if (roleDefA?.SpecialInteractionType != "patrol_check") return false;

        float reputationB = npcB.Properties.GetValueOrDefault("reputation", 0.5f);
        if (reputationB >= _maxReputation) return false;
        if (trust >= _maxTrust) return false;
        if (rng.NextDouble() > _probability) return false;
        return true;
    }
}
