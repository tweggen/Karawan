using System;
using System.Collections.Generic;

namespace engine.tale;

/// <summary>
/// Central registry of all interaction types. Inherits from ObjectRegistry<InteractionTypeDefinition>.
/// Provides condition evaluation and trust delta lookup.
/// </summary>
public class InteractionTypeRegistry : ObjectRegistry<InteractionTypeDefinition>
{
    private Dictionary<string, IInteractionCondition> _conditionCache = new();
    private List<(string Id, InteractionTypeDefinition Def)> _typesOrderedByPriority;

    /// <summary>
    /// Rebuild the priority-ordered list after loading all types.
    /// Called once during initialization.
    /// </summary>
    public void FinalizeOrder()
    {
        var keys = GetKeys();
        _typesOrderedByPriority = new List<(string, InteractionTypeDefinition)>(keys.Count);
        foreach (var id in keys)
        {
            var def = Get(id);
            if (def != null)
                _typesOrderedByPriority.Add((id, def));
        }
        // Sort by priority descending (higher priority first)
        _typesOrderedByPriority.Sort((a, b) => b.Item2.SelectionPriority.CompareTo(a.Item2.SelectionPriority));
    }

    /// <summary>
    /// Get or create a condition evaluator for an interaction type.
    /// Reflection-based instantiation from ConditionClassName.
    /// </summary>
    private IInteractionCondition GetCondition(InteractionTypeDefinition def)
    {
        if (string.IsNullOrEmpty(def.ConditionClassName)) return null;
        if (_conditionCache.TryGetValue(def.Id, out var cached)) return cached;

        var type = Type.GetType(def.ConditionClassName);
        if (type == null) return null;

        var instance = Activator.CreateInstance(type) as IInteractionCondition;
        if (instance != null)
        {
            instance.Initialize(def.ConditionParameters ?? new Dictionary<string, float>());
            _conditionCache[def.Id] = instance;
        }
        return instance;
    }

    /// <summary>
    /// Get trust delta for an interaction type.
    /// </summary>
    public float GetTrustDelta(string interactionTypeId)
    {
        var def = Get(interactionTypeId);
        return def?.TrustDelta ?? 0f;
    }

    /// <summary>
    /// Evaluate all conditions in priority order until one matches.
    /// Returns the ID of the first matching interaction type, or null.
    /// </summary>
    public string EvaluateConditions(NpcSchedule npcA, NpcSchedule npcB, float trust, Random rng)
    {
        if (_typesOrderedByPriority == null || _typesOrderedByPriority.Count == 0)
            return null;

        foreach (var (id, def) in _typesOrderedByPriority)
        {
            var condition = GetCondition(def);
            if (condition != null && condition.Evaluate(npcA, npcB, trust, rng))
                return id;
        }
        return null;
    }
}
