using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace engine.tale;

/// <summary>
/// One unordered NPC–NPC pair that has trust recorded in at least one direction
/// (TALE S0 W6). <c>A &lt; B</c>; a direction nobody recorded is null.
/// </summary>
public readonly record struct TrustPair(int A, int B, float? TrustAtoB, float? TrustBtoA)
{
    /// <summary>
    /// The pair's trust: the mean of both directions, or the one direction
    /// recorded. The same rule as CommunityDetector.DetectFromSchedules, which
    /// uses the known direction for a missing one.
    /// </summary>
    public float Average =>
        TrustAtoB.HasValue && TrustBtoA.HasValue
            ? (TrustAtoB.Value + TrustBtoA.Value) / 2f
            : TrustAtoB ?? TrustBtoA ?? 0f;


    /// <summary>
    /// Pairs from the per-NPC Trust dictionaries (the live game). Only ids of
    /// NPCs in the list count; the player (-1) and NPCs elsewhere are left out.
    /// </summary>
    public static List<TrustPair> FromSchedules(IEnumerable<NpcSchedule> npcs)
    {
        var byId = new Dictionary<int, NpcSchedule>();
        foreach (var npc in npcs) byId[npc.NpcId] = npc;

        var pairs = new Dictionary<long, TrustPair>();
        foreach (var npc in byId.Values)
        {
            if (npc.Trust == null) continue;
            foreach (var (otherId, trust) in npc.Trust)
            {
                if (otherId == npc.NpcId || !byId.ContainsKey(otherId)) continue;
                int a = Math.Min(npc.NpcId, otherId), b = Math.Max(npc.NpcId, otherId);
                long key = RelationshipTracker.PairKey(a, b);
                pairs.TryGetValue(key, out var pair);
                pair = npc.NpcId == a
                    ? new TrustPair(a, b, trust, pair.TrustBtoA)
                    : new TrustPair(a, b, pair.TrustAtoB, trust);
                pairs[key] = pair;
            }
        }

        return pairs.Values.OrderBy(p => p.A).ThenBy(p => p.B).ToList();
    }


    /// <summary>
    /// Pairs from a RelationshipTracker (DES and the bake), restricted to the
    /// given NPC ids. The tracker always holds both directions.
    /// </summary>
    public static List<TrustPair> FromTracker(RelationshipTracker tracker, IEnumerable<int> npcIds)
    {
        var ids = new HashSet<int>(npcIds);
        var pairs = new List<TrustPair>();
        foreach (var (key, state) in tracker.AllRelationships)
        {
            int a = (int)(key >> 32), b = (int)(key & 0xFFFFFFFF);
            if (!ids.Contains(a) || !ids.Contains(b)) continue;
            pairs.Add(new TrustPair(a, b, state.TrustAtoB, state.TrustBtoA));
        }

        return pairs.OrderBy(p => p.A).ThenBy(p => p.B).ToList();
    }
}


/// <summary>
/// What the scenario bake did to one cluster (ScenarioApplicator's result,
/// kept by TaleManager.PopulateCluster instead of only being logged).
/// </summary>
public sealed class SocialBakeInfo
{
    public string Category = "";
    public int Index;
    public int RealNpcCount;
    public int ScenarioNpcCount;
    public int MatchedNpcCount;
    public int RelationshipsApplied;
    public int RelationshipsSkipped;
    public int GroupsTouched;

    /// <summary>Share of the real NPCs that got a scenario NPC.</summary>
    public double MatchRatio => RealNpcCount > 0 ? (double)MatchedNpcCount / RealNpcCount : 0.0;

    /// <summary>Share of the scenario's NPCs that found a real NPC.</summary>
    public double Coverage => ScenarioNpcCount > 0 ? (double)MatchedNpcCount / ScenarioNpcCount : 0.0;
}


/// <summary>One day of group movement.</summary>
public readonly record struct SocialDaySample(
    DateTime Date, int Groups, int NpcsInGroups, int MembershipChanges, int GroupsCreated, int GroupsDissolved);


/// <summary>
/// Group membership over time for one cluster (or one DES run), so churn can be
/// counted although group ids are renumbered. Transient: it lives on
/// ClusterSocialState but is not part of its save JSON.
///
/// At each day roll the rosters at the start of the day are matched to today's
/// with <see cref="GroupMatching.Match"/>: a group that continues an old one is
/// the same group whatever its id. An NPC changed membership when its group
/// today is not the continuation of its group at the start of the day
/// (ungrouped counts as a group of its own).
/// </summary>
public sealed class SocialMetricsHistory
{
    public const int Capacity = 120;

    private Dictionary<int, List<int>> _startRosters;
    private Dictionary<int, int> _startGroupOf;
    private DateTime _startDate;
    private readonly Queue<SocialDaySample> _samples = new();

    public IReadOnlyCollection<SocialDaySample> Samples => _samples;
    public bool IsStarted => _startRosters != null;


    /// <summary>Group id -> sorted member ids, from NpcSchedule.GroupId.</summary>
    public static Dictionary<int, List<int>> RostersOf(IEnumerable<NpcSchedule> npcs)
    {
        var rosters = new Dictionary<int, List<int>>();
        foreach (var npc in npcs)
        {
            if (npc.GroupId < 0) continue;
            if (!rosters.TryGetValue(npc.GroupId, out var list))
            {
                list = new List<int>();
                rosters[npc.GroupId] = list;
            }
            list.Add(npc.NpcId);
        }

        foreach (var list in rosters.Values) list.Sort();
        return rosters;
    }


    /// <summary>Record the state at the start of a day without producing a sample.</summary>
    public void Start(DateTime date, IEnumerable<NpcSchedule> npcs)
    {
        var list = npcs.ToList();
        _startRosters = RostersOf(list);
        _startGroupOf = list.ToDictionary(n => n.NpcId, n => n.GroupId);
        _startDate = date.Date;
    }


    /// <summary>
    /// Close the day that started at the last Start/RollDay: compare today's
    /// groups with those at its start, store the sample, and start the next day.
    /// The first call only starts.
    /// </summary>
    public SocialDaySample? RollDay(DateTime date, IEnumerable<NpcSchedule> npcs)
    {
        var list = npcs.ToList();
        if (!IsStarted)
        {
            Start(date, list);
            return null;
        }

        var sample = Compare(_startDate, _startRosters, _startGroupOf, list);
        _samples.Enqueue(sample);
        while (_samples.Count > Capacity) _samples.Dequeue();

        Start(date, list);
        return sample;
    }


    /// <summary>The comparison itself, pure.</summary>
    public static SocialDaySample Compare(
        DateTime date,
        IReadOnlyDictionary<int, List<int>> startRosters,
        IReadOnlyDictionary<int, int> startGroupOf,
        IReadOnlyList<NpcSchedule> npcsNow)
    {
        var nowRosters = RostersOf(npcsNow);
        var nowIds = nowRosters.Keys.OrderBy(id => id).ToList();
        var continues = GroupMatching.Match(
            nowIds.Select(id => (IReadOnlyCollection<int>)nowRosters[id]).ToList(),
            startRosters.Keys.OrderBy(id => id)
                .Select(id => (id, (IReadOnlyCollection<int>)startRosters[id])));

        // old group id -> the id it carries today
        var oldToNow = new Dictionary<int, int>();
        int created = 0;
        for (int i = 0; i < nowIds.Count; i++)
        {
            if (continues[i] == -1) created++;
            else oldToNow[continues[i]] = nowIds[i];
        }

        int dissolved = startRosters.Keys.Count(id => !oldToNow.ContainsKey(id));

        int changes = 0;
        int inGroups = 0;
        foreach (var npc in npcsNow)
        {
            if (npc.GroupId >= 0) inGroups++;
            if (!startGroupOf.TryGetValue(npc.NpcId, out int oldGroup)) continue;

            int expected = oldGroup < 0 ? -1 : oldToNow.GetValueOrDefault(oldGroup, int.MinValue);
            if (npc.GroupId != expected) changes++;
        }

        return new SocialDaySample(date.Date, nowRosters.Count, inGroups, changes, created, dissolved);
    }
}


/// <summary>
/// The shared social metrics of one cluster or one DES run (TALE S0 W6). One
/// calculator feeds the tests, the ImGui panel, the Testbed's "social" block
/// and the in-game log line, so they cannot disagree.
/// </summary>
public sealed class SocialMetrics
{
    public int NpcCount;

    public int GroupCount;
    public Dictionary<string, int> GroupCountByType = new();
    public int GroupSizeMin, GroupSizeMax;
    public double GroupSizeMedian, GroupSizeMean;
    public int NpcsInGroups;
    public double GroupMembershipRatio;

    /// <summary>
    /// NPCs whose schedule and the group table disagree: the schedule names a
    /// group whose roster lacks it, or a roster lists it under another group.
    /// 0 when no table was given.
    /// </summary>
    public int GroupTableMismatch;

    public int KnownPairs;

    /// <summary>KnownPairs / (N·(N−1)/2): ScenarioStatistics.RelationshipDensity's definition.</summary>
    public double TrustDensity;

    /// <summary>Pairs whose trust is at least <see cref="SocialMetricsCalculator.StrongTrust"/>.</summary>
    public int StrongPairs;

    public double? MeanIntraGroupTrust;
    public double? MeanInterGroupTrust;

    /// <summary>Mean trust between members of two groups, keyed "g1-g2" with g1 &lt; g2.</summary>
    public Dictionary<string, double> InterGroupTrust = new();

    public int MetPlayer;
    public double MetPlayerShare;
    public double? MeanPlayerTrust;

    public SocialBakeInfo Bake;

    public int HistoryDays;
    public double? MembershipChurnPerDay;
    public double? GroupsCreatedPerDay;
    public double? GroupsDissolvedPerDay;


    private static object _r(double? v) => v.HasValue ? Math.Round(v.Value, 3) : null;


    public Dictionary<string, object> ToDictionary()
    {
        var d = new Dictionary<string, object>
        {
            ["npc_count"] = NpcCount,
            ["groups"] = GroupCount,
            ["groups_by_type"] = GroupCountByType.OrderBy(kv => kv.Key)
                .ToDictionary(kv => kv.Key, kv => (object)kv.Value),
            ["group_size"] = new Dictionary<string, object>
            {
                ["min"] = GroupSizeMin,
                ["median"] = _r(GroupSizeMedian),
                ["mean"] = _r(GroupSizeMean),
                ["max"] = GroupSizeMax
            },
            ["npcs_in_groups"] = NpcsInGroups,
            ["group_membership_ratio"] = _r(GroupMembershipRatio),
            ["group_table_mismatch"] = GroupTableMismatch,
            ["known_pairs"] = KnownPairs,
            ["trust_density"] = _r(TrustDensity),
            ["strong_pairs"] = StrongPairs,
            ["mean_intra_group_trust"] = _r(MeanIntraGroupTrust),
            ["mean_inter_group_trust"] = _r(MeanInterGroupTrust),
            ["inter_group_trust"] = InterGroupTrust.OrderBy(kv => kv.Key)
                .ToDictionary(kv => kv.Key, kv => _r(kv.Value)),
            ["met_player"] = MetPlayer,
            ["met_player_share"] = _r(MetPlayerShare),
            ["mean_player_trust"] = _r(MeanPlayerTrust),
            ["history_days"] = HistoryDays,
            ["membership_churn_per_day"] = _r(MembershipChurnPerDay),
            ["groups_created_per_day"] = _r(GroupsCreatedPerDay),
            ["groups_dissolved_per_day"] = _r(GroupsDissolvedPerDay)
        };

        if (Bake != null)
        {
            d["bake"] = new Dictionary<string, object>
            {
                ["scenario"] = $"{Bake.Category}/{Bake.Index}",
                ["match_ratio"] = _r(Bake.MatchRatio),
                ["coverage"] = _r(Bake.Coverage),
                ["relationships_applied"] = Bake.RelationshipsApplied,
                ["relationships_skipped"] = Bake.RelationshipsSkipped
            };
        }

        return d;
    }


    private static string _f(double? v) =>
        v.HasValue ? v.Value.ToString("F2", CultureInfo.InvariantCulture) : "-";


    public string ToLogLine()
    {
        string types = string.Join(",", GroupCountByType.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));
        string bake = Bake != null ? $" bake={_f(Bake.MatchRatio)}" : "";
        return $"npcs={NpcCount} groups={GroupCount}[{types}] in_groups={_f(GroupMembershipRatio)} " +
               $"size={GroupSizeMin}/{_f(GroupSizeMedian)}/{GroupSizeMax} mismatch={GroupTableMismatch} " +
               $"pairs={KnownPairs} density={_f(TrustDensity)} strong={StrongPairs} " +
               $"intra={_f(MeanIntraGroupTrust)} inter={_f(MeanInterGroupTrust)} " +
               $"met_player={MetPlayer} ({_f(MetPlayerShare)}) trust_player={_f(MeanPlayerTrust)} " +
               $"churn/day={_f(MembershipChurnPerDay)} over {HistoryDays}d{bake}";
    }
}


/// <summary>
/// Pure: no I.Get, no clock. Group membership always comes from
/// NpcSchedule.GroupId; the group table only supplies types and the mismatch
/// count. Player trust (key -1) never counts as an NPC–NPC pair.
/// </summary>
public static class SocialMetricsCalculator
{
    /// <summary>CommunityDetector.TrustThreshold: the trust at which a tie counts.</summary>
    public const float StrongTrust = 0.6f;


    public static SocialMetrics Compute(
        IReadOnlyCollection<NpcSchedule> npcs,
        IEnumerable<TrustPair> pairs,
        IReadOnlyDictionary<int, RuntimeGroup> groupTable = null,
        SocialBakeInfo bake = null,
        SocialMetricsHistory history = null)
    {
        var m = new SocialMetrics { NpcCount = npcs.Count, Bake = bake };
        var groupOf = new Dictionary<int, int>();
        foreach (var npc in npcs) groupOf[npc.NpcId] = npc.GroupId;

        // Groups, from the schedules.
        var rosters = SocialMetricsHistory.RostersOf(npcs);
        m.GroupCount = rosters.Count;
        m.NpcsInGroups = rosters.Values.Sum(r => r.Count);
        m.GroupMembershipRatio = npcs.Count > 0 ? (double)m.NpcsInGroups / npcs.Count : 0.0;
        if (rosters.Count > 0)
        {
            var sizes = rosters.Values.Select(r => r.Count).OrderBy(s => s).ToList();
            m.GroupSizeMin = sizes[0];
            m.GroupSizeMax = sizes[^1];
            m.GroupSizeMean = sizes.Average();
            m.GroupSizeMedian = sizes.Count % 2 == 1
                ? sizes[sizes.Count / 2]
                : (sizes[sizes.Count / 2 - 1] + sizes[sizes.Count / 2]) / 2.0;
        }

        foreach (var groupId in rosters.Keys)
        {
            string type = groupTable != null && groupTable.TryGetValue(groupId, out var g) && !string.IsNullOrEmpty(g.Type)
                ? g.Type
                : "unknown";
            m.GroupCountByType.TryGetValue(type, out int c);
            m.GroupCountByType[type] = c + 1;
        }

        // Schedules against the table.
        if (groupTable != null)
        {
            var mismatched = new HashSet<int>();
            foreach (var npc in npcs)
            {
                if (npc.GroupId < 0) continue;
                if (!groupTable.TryGetValue(npc.GroupId, out var g) || !g.MemberIds.Contains(npc.NpcId))
                    mismatched.Add(npc.NpcId);
            }

            foreach (var g in groupTable.Values)
            {
                foreach (int id in g.MemberIds)
                {
                    if (groupOf.TryGetValue(id, out int scheduleGroup) && scheduleGroup != g.GroupId)
                        mismatched.Add(id);
                }
            }

            m.GroupTableMismatch = mismatched.Count;
        }

        // Pairs.
        double intraSum = 0, interSum = 0;
        int intraCount = 0, interCount = 0;
        var matrix = new Dictionary<string, (double sum, int count)>();
        foreach (var p in pairs)
        {
            if (p.A < 0 || p.B < 0) continue;
            if (!groupOf.ContainsKey(p.A) || !groupOf.ContainsKey(p.B)) continue;

            m.KnownPairs++;
            float avg = p.Average;
            if (avg >= StrongTrust) m.StrongPairs++;

            int ga = groupOf[p.A], gb = groupOf[p.B];
            if (ga < 0 || gb < 0) continue;
            if (ga == gb)
            {
                intraSum += avg;
                intraCount++;
            }
            else
            {
                interSum += avg;
                interCount++;
                string key = $"{Math.Min(ga, gb)}-{Math.Max(ga, gb)}";
                matrix.TryGetValue(key, out var acc);
                matrix[key] = (acc.sum + avg, acc.count + 1);
            }
        }

        long maxPairs = (long)npcs.Count * (npcs.Count - 1) / 2;
        m.TrustDensity = maxPairs > 0 ? (double)m.KnownPairs / maxPairs : 0.0;
        m.MeanIntraGroupTrust = intraCount > 0 ? intraSum / intraCount : null;
        m.MeanInterGroupTrust = interCount > 0 ? interSum / interCount : null;
        foreach (var (key, acc) in matrix) m.InterGroupTrust[key] = acc.sum / acc.count;

        // The player.
        double playerSum = 0;
        foreach (var npc in npcs)
        {
            if (npc.Trust != null && npc.Trust.TryGetValue(PlayerTrustKey, out float t))
            {
                m.MetPlayer++;
                playerSum += t;
            }
        }

        m.MetPlayerShare = npcs.Count > 0 ? (double)m.MetPlayer / npcs.Count : 0.0;
        m.MeanPlayerTrust = m.MetPlayer > 0 ? playerSum / m.MetPlayer : null;

        // Churn.
        if (history != null && history.Samples.Count > 0)
        {
            m.HistoryDays = history.Samples.Count;
            m.MembershipChurnPerDay = history.Samples.Average(s => s.MembershipChanges);
            m.GroupsCreatedPerDay = history.Samples.Average(s => s.GroupsCreated);
            m.GroupsDissolvedPerDay = history.Samples.Average(s => s.GroupsDissolved);
        }

        return m;
    }


    /// <summary>The Trust key under which an NPC holds its trust in the player.</summary>
    public const int PlayerTrustKey = -1;
}


/// <summary>
/// How many NPCs are near a point at a game time, by where they are (S0 W9):
/// on the street (walking between locations or standing at a street
/// location), in a venue (shop, bar, any other non-home location) or at home
/// or work. Only the street NPCs are visible: an NPC at any non-street
/// location is indoors (Phase 7).
/// </summary>
public readonly record struct NpcDensity(int Street, int Venue, int HomeOrWork)
{
    public int Total => Street + Venue + HomeOrWork;

    public override string ToString() =>
        $"{Total} (street {Street}, venue {Venue}, home/work {HomeOrWork})";
}


public enum NpcWhereabouts { Street, Venue, HomeOrWork }


public static class NpcDensityCounter
{
    /// <summary>
    /// Count the NPCs whose schedule puts them within <paramref name="radius"/>
    /// (horizontal) of <paramref name="centre"/> at <paramref name="gameNow"/>.
    /// Uses NpcSchedule.PositionAt, where a spawned NPC would appear, not
    /// CurrentWorldPosition, which is the destination while travelling. The
    /// headless measurement and the in-game cross-check both use this.
    /// </summary>
    public static NpcDensity CountNear(
        IEnumerable<NpcSchedule> npcs, SpatialModel model, Vector3 centre, float radius, DateTime gameNow)
        => CountNear(Locate(npcs, model, gameNow), centre, radius);


    /// <summary>
    /// Where each NPC is at <paramref name="gameNow"/>; compute once and count
    /// around many centres.
    /// </summary>
    public static List<(Vector3 Position, NpcWhereabouts Where)> Locate(
        IEnumerable<NpcSchedule> npcs, SpatialModel model, DateTime gameNow)
    {
        var result = new List<(Vector3, NpcWhereabouts)>();
        foreach (var npc in npcs)
        {
            result.Add((npc.PositionAt(gameNow, model), _whereabouts(npc, model, gameNow)));
        }
        return result;
    }


    public static NpcDensity CountNear(
        IReadOnlyList<(Vector3 Position, NpcWhereabouts Where)> located, Vector3 centre, float radius)
    {
        int street = 0, venue = 0, homeOrWork = 0;
        float r2 = radius * radius;
        foreach (var (pos, where) in located)
        {
            float dx = pos.X - centre.X, dz = pos.Z - centre.Z;
            if (dx * dx + dz * dz > r2) continue;
            switch (where)
            {
                case NpcWhereabouts.Street: street++; break;
                case NpcWhereabouts.Venue: venue++; break;
                default: homeOrWork++; break;
            }
        }
        return new NpcDensity(street, venue, homeOrWork);
    }


    private static NpcWhereabouts _whereabouts(NpcSchedule npc, SpatialModel model, DateTime gameNow)
    {
        if (npc.IsInTransit && gameNow >= npc.TransitStart && gameNow < npc.TransitEnd)
            return NpcWhereabouts.Street;
        if (npc.CurrentLocationId == npc.HomeLocationId || npc.CurrentLocationId == npc.WorkplaceLocationId)
            return NpcWhereabouts.HomeOrWork;
        return model?.GetLocation(npc.CurrentLocationId)?.Type switch
        {
            "street_segment" => NpcWhereabouts.Street,
            "home" or "office" or "warehouse" => NpcWhereabouts.HomeOrWork,
            _ => NpcWhereabouts.Venue
        };
    }
}
