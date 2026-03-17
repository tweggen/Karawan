using System;
using System.Collections.Generic;

namespace engine.tale;

/// <summary>
/// Condition evaluator for a specific interaction type.
/// Determines if two NPCs should have this interaction based on their properties.
/// </summary>
public interface IInteractionCondition
{
    /// <summary>
    /// Initialize condition with parameters from configuration.
    /// </summary>
    void Initialize(Dictionary<string, float> parameters);

    /// <summary>
    /// Check if this interaction should occur given two NPCs and current trust.
    /// </summary>
    /// <param name="npcA">Initiating NPC.</param>
    /// <param name="npcB">Receiving NPC.</param>
    /// <param name="trust">Current trust level between them.</param>
    /// <param name="rng">Random number generator for probabilistic conditions.</param>
    /// <returns>True if condition is met and interaction should occur.</returns>
    bool Evaluate(NpcSchedule npcA, NpcSchedule npcB, float trust, Random rng);
}
