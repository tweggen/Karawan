using System;
using System.Collections.Generic;
using System.Linq;
using static engine.Logger;

namespace engine.tale;

/// <summary>
/// Registry for group type definitions.
/// Provides group classification based on member properties and rules.
/// </summary>
public class GroupTypeRegistry : ObjectRegistry<GroupTypeDefinition>
{
    /// <summary>
    /// Classify a group based on member properties and rule matching.
    /// Returns the ID of the first matching group type (by priority, then registration order).
    /// </summary>
    public string ClassifyGroup(List<int> memberIds, IReadOnlyDictionary<int, NpcSchedule> npcs)
    {
        if (memberIds.Count == 0) return "unknown";

        // Compute aggregate statistics for the group
        var stats = ComputeGroupStatistics(memberIds, npcs);

        // Get all types sorted by priority (descending)
        var keys = GetKeys();
        var types = new List<(string id, GroupTypeDefinition def)>();
        foreach (var key in keys)
        {
            var def = Get(key);
            if (def != null)
                types.Add((key, def));
        }
        types.Sort((a, b) => b.def.Priority.CompareTo(a.def.Priority));

        // Find first matching type
        foreach (var (id, typeDef) in types)
        {
            if (typeDef.Rules.Count == 0)
                continue;

            // All rules must match (AND logic)
            bool allMatch = true;
            foreach (var rule in typeDef.Rules)
            {
                if (!EvaluateRule(rule, stats))
                {
                    allMatch = false;
                    break;
                }
            }

            if (allMatch)
                return id;
        }

        return "social"; // Default fallback
    }

    /// <summary>
    /// Compute aggregate statistics for a group of NPCs.
    /// </summary>
    private GroupStatistics ComputeGroupStatistics(List<int> memberIds, IReadOnlyDictionary<int, NpcSchedule> npcs)
    {
        var stats = new GroupStatistics();
        int count = 0;

        foreach (int id in memberIds)
        {
            if (!npcs.TryGetValue(id, out var npc))
                continue;

            stats.TotalMembers++;
            stats.AverageWealth += npc.Properties.GetValueOrDefault("wealth", 0.5f);
            stats.AverageMorality += npc.Properties.GetValueOrDefault("morality", 0.7f);
            stats.AverageAnger += npc.Properties.GetValueOrDefault("anger", 0f);
            stats.AverageReputation += npc.Properties.GetValueOrDefault("reputation", 0.5f);

            if (npc.Role == "authority")
                stats.AuthorityCount++;
            if (npc.Role is "merchant" or "hustler")
                stats.TraderCount++;

            count++;
        }

        if (count > 0)
        {
            stats.AverageWealth /= count;
            stats.AverageMorality /= count;
            stats.AverageAnger /= count;
            stats.AverageReputation /= count;
            stats.AuthorityRatio = (float)stats.AuthorityCount / count;
            stats.TraderRatio = (float)stats.TraderCount / count;
        }

        return stats;
    }

    /// <summary>
    /// Evaluate a single classification rule against group statistics.
    /// </summary>
    private bool EvaluateRule(GroupClassificationRule rule, GroupStatistics stats)
    {
        return rule.Type switch
        {
            "authority_threshold" => EvaluateAuthorityThreshold(rule, stats),
            "combined_threshold" => EvaluateCombinedThreshold(rule, stats),
            "trader_threshold" => EvaluateTraderThreshold(rule, stats),
            _ => _unknownRuleType(rule.Type)
        };
    }


    private static readonly HashSet<string> _warnedRuleTypes = new();

    /**
     * An unknown rule never matches, so its group type can never be chosen.
     * Say so once per type instead of silently classifying everything else.
     * "property_threshold" was removed (KAR-436): it needed a string parameter
     * in the float-only Parameters dictionary and read the property name from
     * the first key; no shipped type used it. Use combined_threshold.
     */
    private static bool _unknownRuleType(string type)
    {
        lock (_warnedRuleTypes)
        {
            if (_warnedRuleTypes.Add(type ?? ""))
                Warning($"GroupTypeRegistry: unknown classification rule type \"{type}\"; the rule never matches. " +
                        "Known types: authority_threshold, combined_threshold, trader_threshold.");
        }
        return false;
    }

    private bool EvaluateAuthorityThreshold(GroupClassificationRule rule, GroupStatistics stats)
    {
        if (!rule.Parameters.TryGetValue("minimum_ratio", out float minRatio))
            return false;
        return stats.AuthorityRatio >= minRatio;
    }

    /**
     * The share of the group that trades - merchants and hustlers (KAR-477). A trading
     * partnership used to be any group with mean wealth and reputation of 0.5 or more,
     * which is the average of a bake's population: 32 of 45 groups over four medium bakes,
     * office workers and socialites included. With at least 40 % traders: 10 of 45.
     */
    private bool EvaluateTraderThreshold(GroupClassificationRule rule, GroupStatistics stats)
    {
        if (!rule.Parameters.TryGetValue("minimum_ratio", out float minRatio))
            return false;
        return stats.TraderRatio >= minRatio;
    }

    private bool EvaluateCombinedThreshold(GroupClassificationRule rule, GroupStatistics stats)
    {
        // Example: wealth < 0.3 AND morality < 0.4
        float wealthThreshold = rule.Parameters.GetValueOrDefault("wealth_max", float.MaxValue);
        float moralityThreshold = rule.Parameters.GetValueOrDefault("morality_max", float.MaxValue);
        float wealthMinThreshold = rule.Parameters.GetValueOrDefault("wealth_min", float.MinValue);
        float reputationMinThreshold = rule.Parameters.GetValueOrDefault("reputation_min", float.MinValue);

        bool wealthCheck = stats.AverageWealth < wealthThreshold && stats.AverageWealth >= wealthMinThreshold;
        bool moralityCheck = stats.AverageMorality < moralityThreshold;
        bool reputationCheck = stats.AverageReputation >= reputationMinThreshold;

        return wealthCheck && moralityCheck && reputationCheck;
    }

    /// <summary>
    /// Helper class for aggregate group statistics.
    /// </summary>
    private class GroupStatistics
    {
        public int TotalMembers;
        public int AuthorityCount;
        public float AuthorityRatio;
        public int TraderCount;
        public float TraderRatio;
        public float AverageWealth;
        public float AverageMorality;
        public float AverageAnger;
        public float AverageReputation;
    }
}
