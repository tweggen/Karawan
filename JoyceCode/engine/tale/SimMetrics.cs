using System;
using System.Collections.Generic;
using System.Linq;

namespace engine.tale;

/// <summary>
/// Where a DES storylet selection came from (S0 W6). Routine and Resumed are
/// the NPC's own routine; the rest were forced on it or interrupted it.
/// </summary>
public enum SelectionSource
{
    /// <summary>Normal selection, including after a Cancel-scope interrupt.</summary>
    Routine,
    /// <summary>A paused storylet popped off the arc stack.</summary>
    Resumed,
    /// <summary>A conditional postcondition forced the next storylet.</summary>
    Forced,
    InterruptNest,
    InterruptReplace
}


/// <summary>
/// Accumulates metrics during DES simulation. After simulation completes,
/// call ComputeFinalMetrics() to get the result dictionary.
/// </summary>
public class MetricsCollector
{
    // Per-NPC daily counters (reset each day)
    private readonly Dictionary<int, int> _dailyStorylets = new();
    private readonly Dictionary<int, int> _dailyEncounters = new();

    // Per-NPC per-property daily min/max
    private readonly Dictionary<int, Dictionary<string, (float min, float max)>> _dailyPropertyRanges = new();

    // Daily range accumulators (sum of ranges, count)
    private readonly Dictionary<string, (double sum, int count)> _dailyRangeAccumulators = new();

    // Totals
    public int TotalStorylets;
    public readonly Dictionary<string, int> InteractionsByType = new();
    public readonly Dictionary<int, int> EncountersByLocation = new();
    public int? FirstConflictDay;
    public int? FirstGangFormationDay;
    public int NpcsInGroupsAtEnd;
    public int TotalGroupsAtEnd;
    public Dictionary<string, int> GroupsByTypeAtEnd = new();

    // Per-role
    public readonly Dictionary<string, int> StoryletsByRole = new();
    public readonly Dictionary<string, int> NpcCountByRole = new();

    // Interrupt tracking
    public int TotalInterrupts;
    private readonly Dictionary<int, int> _dailyInterruptsByNpc = new();
    private readonly List<int> _dailyInterruptCounts = new();

    // Selection sources (S0 W6): what the completion rates are computed from.
    private readonly Dictionary<SelectionSource, int> _selectionsBySource = new();
    private readonly Dictionary<string, (int own, int total)> _selectionsByRole = new();
    private readonly Dictionary<string, int> _interruptsByRole = new();

    // Requests by id: abstract resolution signals the same request every day,
    // so fulfilment is a set, not a counter.
    private readonly HashSet<int> _requestsEmitted = new();
    private readonly HashSet<int> _requestsFulfilled = new();

    public IReadOnlyDictionary<SelectionSource, int> SelectionsBySource => _selectionsBySource;
    public int RequestsEmitted => _requestsEmitted.Count;
    public int RequestsFulfilled => _requestsFulfilled.Count;


    public void RegisterNpc(string role)
    {
        NpcCountByRole.TryGetValue(role, out int c);
        NpcCountByRole[role] = c + 1;
    }


    public void OnStoryletCompleted(int npcId, string role)
    {
        TotalStorylets++;
        _dailyStorylets.TryGetValue(npcId, out int c);
        _dailyStorylets[npcId] = c + 1;
        StoryletsByRole.TryGetValue(role, out int rc);
        StoryletsByRole[role] = rc + 1;
    }


    public void OnEncounter(int npcA, int npcB, string interactionType, int locationId, int day)
    {
        InteractionsByType.TryGetValue(interactionType, out int c);
        InteractionsByType[interactionType] = c + 1;
        EncountersByLocation.TryGetValue(locationId, out int lc);
        EncountersByLocation[locationId] = lc + 1;

        _dailyEncounters.TryGetValue(npcA, out int ca);
        _dailyEncounters[npcA] = ca + 1;
        _dailyEncounters.TryGetValue(npcB, out int cb);
        _dailyEncounters[npcB] = cb + 1;

        if (interactionType == "argue" && FirstConflictDay == null)
            FirstConflictDay = day;
        if (interactionType is "rob" or "intimidate" or "blackmail" && FirstConflictDay == null)
            FirstConflictDay = day;
    }


    public void OnPropertyChanged(int npcId, string prop, float value)
    {
        if (!_dailyPropertyRanges.TryGetValue(npcId, out var props))
        {
            props = new Dictionary<string, (float min, float max)>();
            _dailyPropertyRanges[npcId] = props;
        }
        if (props.TryGetValue(prop, out var range))
            props[prop] = (Math.Min(range.min, value), Math.Max(range.max, value));
        else
            props[prop] = (value, value);
    }


    public void InitializePropertyRanges(int npcId, Dictionary<string, float> props)
    {
        var ranges = new Dictionary<string, (float min, float max)>();
        foreach (var kvp in props)
            ranges[kvp.Key] = (kvp.Value, kvp.Value);
        _dailyPropertyRanges[npcId] = ranges;
    }


    public int GetDailyStorylets(int npcId) =>
        _dailyStorylets.GetValueOrDefault(npcId, 0);

    public int GetDailyEncounters(int npcId) =>
        _dailyEncounters.GetValueOrDefault(npcId, 0);


    public static bool IsOwnRoutine(SelectionSource source) =>
        source is SelectionSource.Routine or SelectionSource.Resumed;


    public void OnSelection(string role, SelectionSource source)
    {
        _selectionsBySource.TryGetValue(source, out int c);
        _selectionsBySource[source] = c + 1;

        string key = role.ToLowerInvariant();
        _selectionsByRole.TryGetValue(key, out var acc);
        _selectionsByRole[key] = (acc.own + (IsOwnRoutine(source) ? 1 : 0), acc.total + 1);
    }


    /// <summary>An interrupt was set on this NPC.</summary>
    public void OnInterrupt(int npcId, string role)
    {
        TotalInterrupts++;
        _dailyInterruptsByNpc.TryGetValue(npcId, out int c);
        _dailyInterruptsByNpc[npcId] = c + 1;

        string key = role.ToLowerInvariant();
        _interruptsByRole.TryGetValue(key, out int rc);
        _interruptsByRole[key] = rc + 1;
    }


    public void OnRequestEmitted(int requestId) => _requestsEmitted.Add(requestId);

    public void OnRequestFulfilled(int requestId)
    {
        if (_requestsEmitted.Contains(requestId)) _requestsFulfilled.Add(requestId);
    }


    /// <summary>
    /// Share of all selections that were the NPC's own routine (owner decision
    /// 2026-10-18); null before the first selection.
    /// </summary>
    public double? RoutineCompletionRate
    {
        get
        {
            int total = _selectionsBySource.Values.Sum();
            if (total == 0) return null;
            int own = _selectionsBySource.Where(kv => IsOwnRoutine(kv.Key)).Sum(kv => kv.Value);
            return (double)own / total;
        }
    }


    /// <summary>Share of emitted requests that were fulfilled; null when none were emitted.</summary>
    public double? RequestFulfillmentRate =>
        _requestsEmitted.Count > 0 ? (double)_requestsFulfilled.Count / _requestsEmitted.Count : null;


    public void OnDayEnd()
    {
        foreach (var (_, props) in _dailyPropertyRanges)
        {
            foreach (var (prop, range) in props)
            {
                float dailyRange = range.max - range.min;
                if (!_dailyRangeAccumulators.TryGetValue(prop, out var acc))
                    acc = (0, 0);
                _dailyRangeAccumulators[prop] = (acc.sum + dailyRange, acc.count + 1);
            }
        }

        // Accumulate daily interrupt count
        int dailyInterruptCount = _dailyInterruptsByNpc.Values.Sum();
        _dailyInterruptCounts.Add(dailyInterruptCount);

        _dailyStorylets.Clear();
        _dailyEncounters.Clear();
        _dailyPropertyRanges.Clear();
        _dailyInterruptsByNpc.Clear();
    }


    public void OnGroupDetection(GroupDetectionResult result, int day)
    {
        NpcsInGroupsAtEnd = result.NpcsInGroups;
        TotalGroupsAtEnd = result.TotalGroups;
        GroupsByTypeAtEnd = new Dictionary<string, int>(result.GroupsByType);

        if (result.GroupsByType.ContainsKey("criminal") && FirstGangFormationDay == null)
            FirstGangFormationDay = day;
    }


    public Dictionary<string, object> ComputeFinalMetrics(
        IReadOnlyDictionary<int, NpcSchedule> npcs,
        RelationshipTracker relationships,
        int totalDays, int totalEncounters, EncounterResolver encounterResolver,
        SpatialModel spatial,
        SocialMetrics social = null)
    {
        int npcCount = npcs.Count;

        // Property stats
        var propertyStats = new Dictionary<string, object>();
        var allProps = new[] { "hunger", "fatigue", "wealth", "health", "anger", "fear", "happiness", "reputation", "morality" };
        foreach (var prop in allProps)
        {
            var values = npcs.Values
                .Where(n => n.Properties.ContainsKey(prop))
                .Select(n => n.Properties[prop])
                .ToList();
            if (values.Count == 0) continue;
            float mean = values.Average();
            float std = MathF.Sqrt(values.Average(v => (v - mean) * (v - mean)));
            var stats = new Dictionary<string, object>
            {
                ["mean"] = Math.Round(mean, 3),
                ["std"] = Math.Round(std, 3)
            };
            if (_dailyRangeAccumulators.TryGetValue(prop, out var acc) && acc.count > 0)
                stats["mean_daily_range"] = Math.Round(acc.sum / acc.count, 3);
            propertyStats[prop] = stats;
        }

        // Role breakdown
        var roleBreakdown = new Dictionary<string, object>();
        foreach (var role in NpcCountByRole.Keys)
        {
            int count = NpcCountByRole.GetValueOrDefault(role, 0);
            string key = role.ToLowerInvariant();
            // completion_rate: share of this role's selections that were its own
            // routine; mean_interrupts: interrupts set per NPC of the role over the run.
            var (own, total) = _selectionsByRole.GetValueOrDefault(key);
            roleBreakdown[key] = new Dictionary<string, object>
            {
                ["count"] = count,
                ["completion_rate"] = total > 0 ? Math.Round((double)own / total, 3) : null,
                ["mean_interrupts"] = count > 0
                    ? Math.Round((double)_interruptsByRole.GetValueOrDefault(key) / count, 3)
                    : 0.0
            };
        }

        // Graph metrics
        var adj = BuildAdjacency(relationships, npcCount);
        int edgeCount = relationships.AllRelationships.Count;
        int largestComponent = LargestComponentSize(adj, npcCount);
        double clusteringCoeff = ClusteringCoefficient(adj);
        double gini = DegreeGini(adj, npcCount);

        // Location hotspots (top 10)
        var hotspots = EncountersByLocation
            .OrderByDescending(kv => kv.Value)
            .Take(10)
            .Select(kv =>
            {
                var loc = spatial.GetLocation(kv.Key);
                return new Dictionary<string, object>
                {
                    ["location_id"] = kv.Key,
                    ["type"] = loc?.Type ?? "unknown",
                    ["total_encounters"] = kv.Value
                };
            })
            .ToList();

        int totalInteractions = InteractionsByType.Values.Sum();

        // Interrupt statistics per NPC-day, the unit of the design target (1-3
        // per NPC per day, TESTBED.md); cluster_mean is the whole cluster's
        // daily count before dividing.
        var interruptsPerDay = new Dictionary<string, object>();
        if (_dailyInterruptCounts.Count > 0 && npcCount > 0)
        {
            var sorted = _dailyInterruptCounts.Select(x => (double)x / npcCount).OrderBy(x => x).ToList();
            double meanInterrupts = sorted.Average();
            double std = Math.Sqrt(sorted.Average(x => (x - meanInterrupts) * (x - meanInterrupts)));
            interruptsPerDay["mean"] = Math.Round(meanInterrupts, 3);
            interruptsPerDay["median"] = Math.Round(sorted[sorted.Count / 2], 3);
            interruptsPerDay["std"] = Math.Round(std, 3);
            interruptsPerDay["p5"] = Math.Round(sorted[(int)(sorted.Count * 0.05)], 3);
            interruptsPerDay["p95"] = Math.Round(sorted[(int)(sorted.Count * 0.95)], 3);
            interruptsPerDay["cluster_mean"] = Math.Round(_dailyInterruptCounts.Average(), 2);
        }
        else
        {
            interruptsPerDay["mean"] = 0.0;
            interruptsPerDay["median"] = 0.0;
            interruptsPerDay["std"] = 0.0;
            interruptsPerDay["p5"] = 0.0;
            interruptsPerDay["p95"] = 0.0;
            interruptsPerDay["cluster_mean"] = 0.0;
        }

        var result = new Dictionary<string, object>
        {
            ["routine_completion_rate"] = RoutineCompletionRate.HasValue
                ? Math.Round(RoutineCompletionRate.Value, 3) : null,
            ["selections_by_source"] = Enum.GetValues<SelectionSource>()
                .ToDictionary(src => src.ToString().ToLowerInvariant(),
                    src => (object)_selectionsBySource.GetValueOrDefault(src)),
            ["interrupts_per_day"] = interruptsPerDay,
            ["interactions_total"] = totalInteractions,
            ["interactions_by_type"] = InteractionsByType.ToDictionary(kv => kv.Key, kv => (object)kv.Value),
            ["request_fulfillment_rate"] = RequestFulfillmentRate.HasValue
                ? Math.Round(RequestFulfillmentRate.Value, 3) : null,
            ["requests_emitted"] = _requestsEmitted.Count,
            ["requests_fulfilled"] = _requestsFulfilled.Count,
            ["graph"] = new Dictionary<string, object>
            {
                ["nodes"] = npcCount,
                ["edges"] = edgeCount,
                ["largest_component_fraction"] = npcCount > 0 ? Math.Round((double)largestComponent / npcCount, 3) : 0,
                ["clustering_coefficient"] = Math.Round(clusteringCoeff, 3),
                ["degree_distribution_gini"] = Math.Round(gini, 3)
            },
            ["properties"] = propertyStats,
            ["role_breakdown"] = roleBreakdown,
            ["escalation"] = new Dictionary<string, object>
            {
                ["first_conflict_day"] = FirstConflictDay.HasValue ? (object)FirstConflictDay.Value : null,
                ["first_gang_formation_day"] = FirstGangFormationDay.HasValue ? (object)FirstGangFormationDay.Value : null,
                ["npcs_in_groups_at_end"] = NpcsInGroupsAtEnd,
                ["total_groups_at_end"] = TotalGroupsAtEnd,
                ["groups_by_type"] = GroupsByTypeAtEnd.ToDictionary(kv => kv.Key, kv => (object)kv.Value)
            },
            ["location_hotspots"] = hotspots
        };

        if (social != null)
            result["social"] = social.ToDictionary();

        return result;
    }


    private static Dictionary<int, HashSet<int>> BuildAdjacency(
        RelationshipTracker relationships, int npcCount)
    {
        var adj = new Dictionary<int, HashSet<int>>();
        foreach (var (key, _) in relationships.AllRelationships)
        {
            int a = (int)(key >> 32);
            int b = (int)(key & 0xFFFFFFFF);
            if (!adj.TryGetValue(a, out var sa)) { sa = new HashSet<int>(); adj[a] = sa; }
            if (!adj.TryGetValue(b, out var sb)) { sb = new HashSet<int>(); adj[b] = sb; }
            sa.Add(b);
            sb.Add(a);
        }
        return adj;
    }


    public static int LargestComponentSize(Dictionary<int, HashSet<int>> adj, int npcCount)
    {
        var visited = new HashSet<int>();
        int largest = 0;
        var queue = new Queue<int>();

        foreach (int node in adj.Keys)
        {
            if (visited.Contains(node)) continue;
            int size = 0;
            queue.Enqueue(node);
            visited.Add(node);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                size++;
                if (adj.TryGetValue(current, out var neighbors))
                {
                    foreach (int n in neighbors)
                    {
                        if (visited.Add(n))
                            queue.Enqueue(n);
                    }
                }
            }
            if (size > largest) largest = size;
        }
        return largest;
    }


    public static double ClusteringCoefficient(Dictionary<int, HashSet<int>> adj)
    {
        double sum = 0;
        int count = 0;
        foreach (var (node, neighbors) in adj)
        {
            int deg = neighbors.Count;
            if (deg < 2) continue;
            int triangles = 0;
            var neighborList = neighbors.ToArray();
            for (int i = 0; i < neighborList.Length; i++)
            {
                for (int j = i + 1; j < neighborList.Length; j++)
                {
                    if (adj.TryGetValue(neighborList[i], out var ni) && ni.Contains(neighborList[j]))
                        triangles++;
                }
            }
            sum += 2.0 * triangles / (deg * (deg - 1));
            count++;
        }
        return count > 0 ? sum / count : 0;
    }


    public static double DegreeGini(Dictionary<int, HashSet<int>> adj, int npcCount)
    {
        var degrees = new int[npcCount];
        foreach (var (node, neighbors) in adj)
        {
            if (node < npcCount) degrees[node] = neighbors.Count;
        }
        Array.Sort(degrees);

        double sum = 0;
        double totalDegree = 0;
        for (int i = 0; i < degrees.Length; i++)
        {
            sum += (i + 1) * degrees[i];
            totalDegree += degrees[i];
        }
        if (totalDegree == 0) return 0;
        return (2.0 * sum) / (npcCount * totalDegree) - (npcCount + 1.0) / npcCount;
    }
}
