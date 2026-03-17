using System;
using System.Collections.Generic;

namespace engine.tale.conditions;

/// <summary>
/// Anger-based interaction condition: arguments and intimidation.
/// Matches if NPC is angry + low morality and trust is low.
/// Used for: argue, intimidate.
/// </summary>
public class AngerBasedCondition : IInteractionCondition
{
    private float _minAnger;
    private float _maxMorality;
    private float _maxTrust;
    private float _probability;

    public void Initialize(Dictionary<string, float> parameters)
    {
        _minAnger = parameters.GetValueOrDefault("minAnger", 0.5f);
        _maxMorality = parameters.GetValueOrDefault("maxMorality", 0.4f);
        _maxTrust = parameters.GetValueOrDefault("maxTrust", 0.3f);
        _probability = parameters.GetValueOrDefault("probability", 0.2f);
    }

    public bool Evaluate(NpcSchedule npcA, NpcSchedule npcB, float trust, Random rng)
    {
        float angerA = npcA.Properties.GetValueOrDefault("anger", 0f);
        float moralityA = npcA.Properties.GetValueOrDefault("morality", 0.7f);

        if (angerA < _minAnger) return false;
        if (moralityA > _maxMorality) return false;
        if (trust >= _maxTrust) return false;
        if (rng.NextDouble() > _probability) return false;
        return true;
    }
}
