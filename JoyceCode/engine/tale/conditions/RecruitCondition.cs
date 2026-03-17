using System;
using System.Collections.Generic;

namespace engine.tale.conditions;

/// <summary>
/// Recruitment condition: gang recruitment and group formation.
/// Matches if both NPCs are desperate, both have low morality, and trust is high.
/// Used for: recruit.
/// </summary>
public class RecruitCondition : IInteractionCondition
{
    private float _minDesperation;
    private float _maxMorality;
    private float _minTrust;
    private float _probability;

    public void Initialize(Dictionary<string, float> parameters)
    {
        _minDesperation = parameters.GetValueOrDefault("minDesperation", 0.5f);
        _maxMorality = parameters.GetValueOrDefault("maxMorality", 0.4f);
        _minTrust = parameters.GetValueOrDefault("minTrust", 0.5f);
        _probability = parameters.GetValueOrDefault("probability", 0.15f);
    }

    public bool Evaluate(NpcSchedule npcA, NpcSchedule npcB, float trust, Random rng)
    {
        float desperationA = StoryletSelector.ComputeDesperation(npcA);
        float desperationB = StoryletSelector.ComputeDesperation(npcB);
        float moralityA = npcA.Properties.GetValueOrDefault("morality", 0.7f);
        float moralityB = npcB.Properties.GetValueOrDefault("morality", 0.7f);

        if (desperationA < _minDesperation || desperationB < _minDesperation) return false;
        if (moralityA > _maxMorality || moralityB > _maxMorality) return false;
        if (trust < _minTrust) return false;
        if (rng.NextDouble() > _probability) return false;
        return true;
    }
}
