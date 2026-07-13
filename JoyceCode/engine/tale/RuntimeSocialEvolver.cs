using System;
using System.Collections.Generic;
using System.Linq;
using static engine.Logger;

namespace engine.tale;

/// <summary>
/// Runtime social evolution (TALE-SOCIAL Phase E3). Two responsibilities,
/// both driven by TaleModule's periodic social tick:
///
/// 1. TickEncounters — co-located NPCs probabilistically encounter each
///    other (same probability model as the bake's EncounterResolver:
///    P = 1 - (1 - p_loc)^(window / 15min)), and each encounter applies a
///    bidirectional trust delta through the InteractionTypeRegistry. This is
///    what the live game was missing entirely: before Phase E, trust was a
///    frozen bake-time snapshot.
///
/// 2. Redetect — re-runs CommunityDetector over the evolved trust graph and
///    reconciles the result with the cluster's existing group table:
///    communities overlapping an existing group's membership by half or more
///    keep that group's identity (id + name); genuinely new communities get
///    fresh ids from ClusterSocialState.NextGroupId; dissolved groups are
///    dropped (authored groups excepted — they only ever grow).
///
/// Determinism: encounter RNG is seeded from (clusterIndex, game day, tick
/// index) with a fixed integer mix — NOT HashCode.Combine, which is
/// per-process randomized. It never touches TaleManager's storylet RNG, so
/// schedule sequences stay reproducible.
///
/// Threading: all methods mutate NpcSchedule.Trust/GroupId and the group
/// table, so callers keep them on the logical-frame thread. Clusters
/// mid-populate are naturally excluded because TaleManager only marks a
/// cluster populated at the very end of PopulateCluster.
/// </summary>
public class RuntimeSocialEvolver
{
    private static readonly engine.Dc _dc = engine.Dc.TaleSocial;

    // Same constants as EncounterResolver (bake).
    public float P_Venue = 0.07f;
    public float P_Workplace = 0.04f;
    public float P_Street = 0.015f;
    public float TimeQuantumMinutes = 15f;

    /// <summary>Cap on sampled pairs per location per tick.</summary>
    public int MaxPairsPerLocation = 12;

    /// <summary>Longest co-presence window credited to one tick, so a long
    /// gap (e.g. cluster just repopulated) doesn't yield a guaranteed
    /// encounter for every pair.</summary>
    public float MaxWindowMinutes = 60f;

    public CommunityDetector Detector = new();


    public sealed class TickStats
    {
        public int NpcsPresent;
        public int LocationsWithCompany;
        public int PairsSampled;
        public int Encounters;
    }


    public sealed class RedetectStats
    {
        public int Communities;
        public int KeptIdentity;
        public int Created;
        public int Dissolved;
    }


    /// <summary>Per-cluster transient tick bookkeeping (dedup, cadence).
    /// Deliberately not part of ClusterSocialState: it is tick machinery,
    /// not social data, and need not survive anything.</summary>
    private class ClusterTickState
    {
        public HashSet<long> DailyDedup = new();
        public DateTime DedupDate = DateTime.MinValue;
        public DateTime LastTickTime = DateTime.MinValue;
        public int TickIndex;
    }

    private readonly Dictionary<int, ClusterTickState> _tickStates = new();


    /// <summary>
    /// Run one encounter tick for a cluster. Buckets the cluster's NPCs by
    /// current location (transit excluded), samples co-located pairs, and
    /// applies trust deltas for the pairs that "meet".
    /// </summary>
    public TickStats TickEncounters(
        int clusterIndex,
        IReadOnlyList<NpcSchedule> npcs,
        SpatialModel spatial,
        DateTime gameNow)
    {
        var stats = new TickStats();
        var st = _getTickState(clusterIndex);

        // Day rollover clears the pair dedup, mirroring the bake's
        // ClearDailyDedup at day boundaries.
        if (gameNow.Date != st.DedupDate)
        {
            st.DailyDedup.Clear();
            st.DedupDate = gameNow.Date;
        }

        float windowMinutes = st.LastTickTime == DateTime.MinValue
            ? 0f
            : Math.Clamp((float)(gameNow - st.LastTickTime).TotalMinutes, 0f, MaxWindowMinutes);
        st.LastTickTime = gameNow;
        st.TickIndex++;
        if (windowMinutes <= 0f) return stats;

        // Deterministic per-(cluster, day, tick) seed with a fixed mix —
        // HashCode.Combine is per-process randomized and would break
        // reproducibility.
        int day = (int)(gameNow.Date - new DateTime(2000, 1, 1)).TotalDays;
        int seed = unchecked(clusterIndex * 73856093 ^ day * 19349663 ^ st.TickIndex * 83492791);
        var rng = new Random(seed);

        // Bucket by current location; skip NPCs mid-transit.
        var byLocation = new Dictionary<int, List<NpcSchedule>>();
        foreach (var npc in npcs)
        {
            if (npc.IsInTransit && gameNow < npc.TransitEnd) continue;
            if (npc.CurrentLocationId < 0) continue;
            if (!byLocation.TryGetValue(npc.CurrentLocationId, out var list))
            {
                list = new List<NpcSchedule>();
                byLocation[npc.CurrentLocationId] = list;
            }
            list.Add(npc);
            stats.NpcsPresent++;
        }

        // Deterministic location order.
        var locationIds = byLocation.Keys.ToList();
        locationIds.Sort();

        foreach (int locId in locationIds)
        {
            var present = byLocation[locId];
            if (present.Count < 2) continue;

            string locType = spatial?.GetLocation(locId)?.Type ?? "street_segment";
            // Homes are private space — no random encounters there.
            if (locType == "home") continue;
            float pBase = locType switch
            {
                "social_venue" => P_Venue,
                "workplace" or "office" or "warehouse" or "shop" => P_Workplace,
                _ => P_Street
            };
            stats.LocationsWithCompany++;

            present.Sort((a, b) => a.NpcId.CompareTo(b.NpcId));
            float p = 1f - MathF.Pow(1f - pBase, windowMinutes / TimeQuantumMinutes);

            int allPairs = present.Count * (present.Count - 1) / 2;
            if (allPairs <= MaxPairsPerLocation)
            {
                for (int i = 0; i < present.Count; i++)
                {
                    for (int j = i + 1; j < present.Count; j++)
                    {
                        stats.PairsSampled++;
                        _tryEncounter(present[i], present[j], p, rng, st, stats);
                    }
                }
            }
            else
            {
                // Sample random pairs up to the cap (deterministic via rng).
                for (int k = 0; k < MaxPairsPerLocation; k++)
                {
                    int i = rng.Next(present.Count);
                    int j = rng.Next(present.Count);
                    if (i == j) continue;
                    stats.PairsSampled++;
                    _tryEncounter(present[i], present[j], p, rng, st, stats);
                }
            }
        }

        return stats;
    }


    private void _tryEncounter(NpcSchedule a, NpcSchedule b, float p, Random rng,
        ClusterTickState st, TickStats stats)
    {
        long pairKey = RelationshipTracker.PairKey(a.NpcId, b.NpcId);
        if (st.DailyDedup.Contains(pairKey)) return;
        if (rng.NextDouble() >= p) return;

        st.DailyDedup.Add(pairKey);
        stats.Encounters++;

        // Mutual trust before the encounter steers the interaction type.
        float trustAtoB = a.Trust != null && a.Trust.TryGetValue(b.NpcId, out float t1) ? t1 : 0f;
        float trustBtoA = b.Trust != null && b.Trust.TryGetValue(a.NpcId, out float t2) ? t2 : 0f;
        float avgTrust = (trustAtoB + trustBtoA) / 2f;

        string type;
        float delta;
        try
        {
            var registry = I.Get<InteractionTypeRegistry>();
            type = registry.EvaluateConditions(a, b, avgTrust, rng) ?? "greet";
            delta = registry.GetTrustDelta(type);
        }
        catch
        {
            // Registry unavailable (bare unit tests): neutral greeting.
            type = "greet";
            delta = 0.04f;
        }

        a.Trust ??= new Dictionary<int, float>();
        b.Trust ??= new Dictionary<int, float>();
        a.Trust[b.NpcId] = Math.Clamp(trustAtoB + delta, 0f, 1f);
        b.Trust[a.NpcId] = Math.Clamp(trustBtoA + delta, 0f, 1f);

        // Parity with DES ProcessEncounter: remember the partner for
        // conditional postconditions.
        a.LastEncounterPartnerId = b.NpcId;
        b.LastEncounterPartnerId = a.NpcId;

        Trace(_dc, $"Encounter: npc {a.NpcId} × npc {b.NpcId} [{type}] trust {avgTrust:F2} → " +
              $"{(a.Trust[b.NpcId] + b.Trust[a.NpcId]) / 2f:F2}");
    }


    /// <summary>
    /// Re-run community detection over the evolved trust graph and reconcile
    /// with the existing group table. Mutates npc.GroupId and
    /// social.Groups; returns stats for the caller's trace line.
    /// </summary>
    public RedetectStats Redetect(
        int clusterIndex,
        ClusterSocialState social,
        IReadOnlyDictionary<int, NpcSchedule> npcs,
        Func<int, int, string, string> nameProvider)
    {
        var stats = new RedetectStats();
        if (npcs.Count == 0) return stats;

        var detection = Detector.DetectFromSchedules(npcs);
        stats.Communities = detection.TotalGroups;

        var newTable = new Dictionary<int, RuntimeGroup>();
        var claimedExisting = new HashSet<int>();

        foreach (var community in detection.Groups)
        {
            // Find the existing group this community most plausibly continues:
            // best member intersection, claimed at most once, and the overlap
            // must cover at least half of the existing group's roster.
            RuntimeGroup bestExisting = null;
            int bestOverlap = 0;
            foreach (var existing in social.Groups.Values)
            {
                if (claimedExisting.Contains(existing.GroupId)) continue;
                int overlap = existing.MemberIds.Count(community.MemberIds.Contains);
                if (overlap > bestOverlap ||
                    (overlap == bestOverlap && bestExisting != null && existing.GroupId < bestExisting.GroupId))
                {
                    bestOverlap = overlap;
                    bestExisting = existing;
                }
            }

            RuntimeGroup group;
            if (bestExisting != null && bestOverlap * 2 >= bestExisting.MemberIds.Count)
            {
                claimedExisting.Add(bestExisting.GroupId);
                group = bestExisting;
                group.Type = community.Type;
                group.MemberIds = new List<int>(community.MemberIds);
                stats.KeptIdentity++;
            }
            else
            {
                int id = social.NextGroupId++;
                group = new RuntimeGroup
                {
                    GroupId = id,
                    Type = community.Type,
                    Name = nameProvider?.Invoke(clusterIndex, id, community.Type) ?? "",
                    MemberIds = new List<int>(community.MemberIds)
                };
                stats.Created++;
            }
            newTable[group.GroupId] = group;

            // The detector assigned its own internal ids; overwrite with the
            // reconciled table id.
            foreach (int memberId in group.MemberIds)
            {
                if (npcs.TryGetValue(memberId, out var npc))
                    npc.GroupId = group.GroupId;
            }
        }

        // Authored (storylet-formed) groups survive detection even when the
        // trust graph doesn't reproduce them; members not claimed by a
        // detected community stay in their authored group.
        foreach (var existing in social.Groups.Values)
        {
            if (newTable.ContainsKey(existing.GroupId)) continue;
            if (existing.Authored)
            {
                newTable[existing.GroupId] = existing;
                foreach (int memberId in existing.MemberIds)
                {
                    if (npcs.TryGetValue(memberId, out var npc) && npc.GroupId == -1)
                        npc.GroupId = existing.GroupId;
                }
            }
            else
            {
                stats.Dissolved++;
            }
        }

        social.Groups = newTable;
        return stats;
    }


    private ClusterTickState _getTickState(int clusterIndex)
    {
        if (!_tickStates.TryGetValue(clusterIndex, out var st))
        {
            st = new ClusterTickState();
            _tickStates[clusterIndex] = st;
        }
        return st;
    }
}
