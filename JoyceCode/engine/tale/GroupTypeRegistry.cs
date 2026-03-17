using System;
using System.Collections.Generic;
using System.Linq;

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

            count++;
        }

        if (count > 0)
        {
            stats.AverageWealth /= count;
            stats.AverageMorality /= count;
            stats.AverageAnger /= count;
            stats.AverageReputation /= count;
            stats.AuthorityRatio = (float)stats.AuthorityCount / count;
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
            "property_threshold" => EvaluatePropertyThreshold(rule, stats),
            "combined_threshold" => EvaluateCombinedThreshold(rule, stats),
            _ => false
        };
    }

    private bool EvaluateAuthorityThreshold(GroupClassificationRule rule, GroupStatistics stats)
    {
        if (!rule.Parameters.TryGetValue("minimum_ratio", out float minRatio))
            return false;
        return stats.AuthorityRatio >= minRatio;
    }

    private bool EvaluatePropertyThreshold(GroupClassificationRule rule, GroupStatistics stats)
    {
        if (!rule.Parameters.TryGetValue("property", out float _))
            return false; // Should be string, but Parameters is float dict - see below

        var property = rule.Parameters.Keys.FirstOrDefault(); // Hack for string property name
        if (property == null || !rule.Parameters.TryGetValue("value", out float threshold))
            return false;

        var @operator = rule.Parameters.ContainsKey("operator") ? (int)rule.Parameters["operator"] : 0;
        // Operators: 0=less_than, 1=greater_than, 2=equal

        float value = property switch
        {
            "wealth" => stats.AverageWealth,
            "morality" => stats.AverageMorality,
            "anger" => stats.AverageAnger,
            "reputation" => stats.AverageReputation,
            _ => 0f
        };

        return @operator switch
        {
            0 => value < threshold,    // less_than
            1 => value > threshold,    // greater_than
            2 => Math.Abs(value - threshold) < 0.01f, // equal
            _ => false
        };
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
        public float AverageWealth;
        public float AverageMorality;
        public float AverageAnger;
        public float AverageReputation;
    }
}
