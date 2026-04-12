using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace engine.tale.bake;

/// <summary>
/// Per-scenario quantitative summary. Built once per baked sc-{hash} file
/// by <see cref="ScenarioStatisticsBuilder.From"/>, then aggregated per
/// category in <see cref="StatisticsReport"/>.
///
/// All fields are pure functions of the source <see cref="Scenario"/>, so
/// rebuilding from the same scenario produces identical numbers — that's
/// the determinism contract D4 locks down.
/// </summary>
public class PerScenarioStats
{
    public string Category { get; set; }
    public int Index { get; set; }
    public int Seed { get; set; }
    public int NpcCount { get; set; }

    public int GroupCount { get; set; }
    public Dictionary<string, int> GroupCountByType { get; set; } = new();
    public int LargestGroupSize { get; set; }
    public int SmallestGroupSize { get; set; }
    public double MeanGroupSize { get; set; }
    public int NpcsInAnyGroup { get; set; }
    public double GroupMembershipRatio { get; set; }

    public int RelationshipCount { get; set; }
    /// <summary>
    /// Density = edges / max possible directed edge pairs (= N * (N-1) / 2).
    /// 1.0 means every NPC pair has a recorded relationship; 0.0 means none do.
    /// </summary>
    public double RelationshipDensity { get; set; }
    public double MeanTrustAtoB { get; set; }
    public double MeanTrustBtoA { get; set; }
    public double MeanInteractionCount { get; set; }

    /// <summary>Histogram of role assignments inside this scenario.</summary>
    public Dictionary<string, int> RoleDistribution { get; set; } = new();

    /// <summary>
    /// Per-property summary statistics for the social-meaningful subset
    /// (morality, wealth, fear, anger, reputation). The keys match what
    /// <see cref="ScenarioExporter"/> emits.
    /// </summary>
    public Dictionary<string, PropertySummary> PropertyStats { get; set; } = new();
}


public class PropertySummary
{
    public double Mean { get; set; }
    public double Stdev { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    /// <summary>
    /// Fraction of NPCs whose value lies in [0, 0.05]. The bake's 365-day
    /// simulation tends to drive properties to the extremes; tracking this
    /// number explicitly makes the bimodal-saturation pattern visible at a
    /// glance instead of buried in a histogram.
    /// </summary>
    public double FractionAtFloor { get; set; }
    public double FractionAtCeiling { get; set; }
}


/// <summary>
/// Aggregate of all baked scenarios in a single category (e.g. "small").
/// Computed by <see cref="StatisticsReport.AggregateByCategory"/>.
/// </summary>
public class CategoryStats
{
    public string Category { get; set; }
    public int ScenarioCount { get; set; }

    public double MeanGroupCount { get; set; }
    public double StdevGroupCount { get; set; }
    public double MeanRelationshipDensity { get; set; }
    public double MeanGroupMembershipRatio { get; set; }

    /// <summary>
    /// Aggregated property stats across all scenarios in the category. The
    /// mean here is the unweighted average of the per-scenario means; that
    /// makes outlier scenarios visible without letting a single huge
    /// scenario dominate the average.
    /// </summary>
    public Dictionary<string, PropertySummary> PropertyStats { get; set; } = new();
}


/// <summary>
/// Top-level scenario-statistics.json shape. Lives next to the bake
/// artifacts in nogame/generated/scenario-statistics.json.
/// </summary>
public class StatisticsReport
{
    public int Version { get; set; } = 1;
    public string GeneratedAt { get; set; }
    public int TotalScenarios { get; set; }
    public List<PerScenarioStats> Scenarios { get; set; } = new();
    public Dictionary<string, CategoryStats> Categories { get; set; } = new();
}


/// <summary>
/// Pure-data builder. Build per-scenario stats from a Scenario, build a
/// full StatisticsReport from a list of scenarios, write the report as
/// indented JSON.
/// </summary>
public static class ScenarioStatisticsBuilder
{
    private static readonly string[] StatProperties =
        { "morality", "wealth", "fear", "anger", "reputation" };

    private static readonly JsonSerializerOptions _writeOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };


    public static PerScenarioStats From(Scenario scenario)
    {
        var stats = new PerScenarioStats
        {
            Category = scenario.Category,
            Index = scenario.Index,
            Seed = scenario.Seed,
            NpcCount = scenario.NpcCount
        };

        // ----- Group statistics -----
        stats.GroupCount = scenario.Groups?.Count ?? 0;
        if (scenario.Groups != null && scenario.Groups.Count > 0)
        {
            int largest = 0;
            int smallest = int.MaxValue;
            long total = 0;
            foreach (var g in scenario.Groups)
            {
                int size = g.MemberRanks?.Count ?? 0;
                if (size > largest) largest = size;
                if (size < smallest) smallest = size;
                total += size;

                if (!string.IsNullOrEmpty(g.Type))
                {
                    stats.GroupCountByType.TryGetValue(g.Type, out int n);
                    stats.GroupCountByType[g.Type] = n + 1;
                }
            }
            stats.LargestGroupSize = largest;
            stats.SmallestGroupSize = smallest == int.MaxValue ? 0 : smallest;
            stats.MeanGroupSize = (double)total / scenario.Groups.Count;
        }

        // Group-membership counts: distinct NPCs that have GroupRank >= 0.
        // (npcs in multiple cliques are counted once.)
        if (scenario.Npcs != null)
        {
            int inAny = 0;
            foreach (var n in scenario.Npcs)
            {
                if (n.GroupRank >= 0) inAny++;
            }
            stats.NpcsInAnyGroup = inAny;
            stats.GroupMembershipRatio = scenario.Npcs.Count > 0
                ? (double)inAny / scenario.Npcs.Count
                : 0.0;
        }

        // ----- Relationship statistics -----
        stats.RelationshipCount = scenario.Relationships?.Count ?? 0;
        if (scenario.NpcCount >= 2)
        {
            // Max possible undirected pairs = N * (N-1) / 2.
            long maxPairs = (long)scenario.NpcCount * (scenario.NpcCount - 1) / 2;
            stats.RelationshipDensity = maxPairs > 0
                ? (double)stats.RelationshipCount / maxPairs
                : 0.0;
        }
        if (scenario.Relationships != null && scenario.Relationships.Count > 0)
        {
            double sumA = 0, sumB = 0;
            long sumInteractions = 0;
            foreach (var r in scenario.Relationships)
            {
                sumA += r.TrustAtoB;
                sumB += r.TrustBtoA;
                sumInteractions += r.InteractionCount;
            }
            int n = scenario.Relationships.Count;
            stats.MeanTrustAtoB = sumA / n;
            stats.MeanTrustBtoA = sumB / n;
            stats.MeanInteractionCount = (double)sumInteractions / n;
        }

        // ----- Role distribution -----
        if (scenario.Npcs != null)
        {
            foreach (var n in scenario.Npcs)
            {
                if (string.IsNullOrEmpty(n.Role)) continue;
                stats.RoleDistribution.TryGetValue(n.Role, out int c);
                stats.RoleDistribution[n.Role] = c + 1;
            }
        }

        // ----- Per-property summary stats -----
        if (scenario.Npcs != null)
        {
            foreach (var key in StatProperties)
            {
                stats.PropertyStats[key] = SummarizeProperty(scenario.Npcs, key);
            }
        }

        return stats;
    }


    private static PropertySummary SummarizeProperty(IReadOnlyList<ScenarioNpc> npcs, string key)
    {
        var summary = new PropertySummary { Min = double.MaxValue, Max = double.MinValue };
        int count = 0;
        double sum = 0;
        int atFloor = 0;
        int atCeiling = 0;
        foreach (var n in npcs)
        {
            if (n.Properties == null) continue;
            if (!n.Properties.TryGetValue(key, out float v)) continue;
            sum += v;
            count++;
            if (v < summary.Min) summary.Min = v;
            if (v > summary.Max) summary.Max = v;
            if (v <= 0.05f) atFloor++;
            if (v >= 0.95f) atCeiling++;
        }
        if (count == 0)
        {
            summary.Min = 0;
            summary.Max = 0;
            return summary;
        }
        summary.Mean = sum / count;

        double sumSqDiff = 0;
        foreach (var n in npcs)
        {
            if (n.Properties == null) continue;
            if (!n.Properties.TryGetValue(key, out float v)) continue;
            double d = v - summary.Mean;
            sumSqDiff += d * d;
        }
        summary.Stdev = count > 1 ? Math.Sqrt(sumSqDiff / count) : 0;
        summary.FractionAtFloor = (double)atFloor / count;
        summary.FractionAtCeiling = (double)atCeiling / count;
        return summary;
    }


    /// <summary>
    /// Build a full StatisticsReport from a list of per-scenario stats and
    /// aggregate by category. Pure function.
    /// </summary>
    public static StatisticsReport BuildReport(IEnumerable<PerScenarioStats> perScenario)
    {
        var report = new StatisticsReport
        {
            GeneratedAt = DateTime.UtcNow.ToString("u")
        };
        var list = perScenario.ToList();
        report.TotalScenarios = list.Count;
        report.Scenarios = list
            .OrderBy(s => s.Category, StringComparer.Ordinal)
            .ThenBy(s => s.Index)
            .ToList();

        // Group by category
        foreach (var grp in list.GroupBy(s => s.Category ?? ""))
        {
            var category = new CategoryStats
            {
                Category = grp.Key,
                ScenarioCount = grp.Count()
            };

            // Mean / stdev of group counts across scenarios in this category
            var groupCounts = grp.Select(s => (double)s.GroupCount).ToList();
            category.MeanGroupCount = groupCounts.Average();
            category.StdevGroupCount = StdevOf(groupCounts, category.MeanGroupCount);

            category.MeanRelationshipDensity = grp.Select(s => s.RelationshipDensity).Average();
            category.MeanGroupMembershipRatio = grp.Select(s => s.GroupMembershipRatio).Average();

            // Aggregated property stats (unweighted average of per-scenario means)
            foreach (var key in StatProperties)
            {
                var summaries = grp
                    .Select(s => s.PropertyStats.TryGetValue(key, out var p) ? p : null)
                    .Where(p => p != null)
                    .ToList();
                if (summaries.Count == 0) continue;
                category.PropertyStats[key] = new PropertySummary
                {
                    Mean = summaries.Average(p => p.Mean),
                    Stdev = summaries.Average(p => p.Stdev),
                    Min = summaries.Min(p => p.Min),
                    Max = summaries.Max(p => p.Max),
                    FractionAtFloor = summaries.Average(p => p.FractionAtFloor),
                    FractionAtCeiling = summaries.Average(p => p.FractionAtCeiling)
                };
            }

            report.Categories[grp.Key] = category;
        }

        return report;
    }


    private static double StdevOf(IReadOnlyList<double> values, double mean)
    {
        if (values.Count <= 1) return 0;
        double sumSq = 0;
        foreach (var v in values)
        {
            double d = v - mean;
            sumSq += d * d;
        }
        return Math.Sqrt(sumSq / values.Count);
    }


    public static void WriteReport(StatisticsReport report, string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        JsonSerializer.Serialize(fs, report, _writeOptions);
    }
}
