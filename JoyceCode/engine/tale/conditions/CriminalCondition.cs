using System;
using System.Collections.Generic;

namespace engine.tale.conditions;

/// <summary>
/// Criminal interaction condition: robbery and intimidation.
/// Matches if attacker is desperate + low morality, victim is wealthy, and trust is low.
/// Used for: rob, intimidate (partially).
/// </summary>
public class CriminalCondition : IInteractionCondition
{
    private float _minDesperation;
    private float _maxMorality;
    private float _minWealthTarget;
    private float _maxTrust;
    private float _probability;

    public void Initialize(Dictionary<string, float> parameters)
    {
        _minDesperation = parameters.GetValueOrDefault("minDesperation", 0.6f);
        _maxMorality = parameters.GetValueOrDefault("maxMorality", 0.3f);
        _minWealthTarget = parameters.GetValueOrDefault("minWealthTarget", 0.4f);
        _maxTrust = parameters.GetValueOrDefault("maxTrust", 0.2f);
        _probability = parameters.GetValueOrDefault("probability", 0.15f);
    }

    public bool Evaluate(NpcSchedule npcA, NpcSchedule npcB, float trust, Random rng)
    {
        float desperation = StoryletSelector.ComputeDesperation(npcA);
        float morality = npcA.Properties.GetValueOrDefault("morality", 0.7f);
        float wealthB = npcB.Properties.GetValueOrDefault("wealth", 0.5f);

        if (desperation < _minDesperation) return false;
        if (morality > _maxMorality) return false;
        if (wealthB < _minWealthTarget) return false;
        if (trust >= _maxTrust) return false;
        if (rng.NextDouble() > _probability * desperation) return false;
        return true;
    }
}
