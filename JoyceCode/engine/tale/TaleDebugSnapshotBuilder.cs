using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace engine.tale;

/// <summary>
/// Builds the TALE debug panel's snapshot (TALE S0 W8). Pure apart from the
/// manager it reads: no I.Get, no clock. Logical thread, like
/// TaleManager.ComputeSocialMetrics, whose result it embeds so the panel and
/// the metrics log line cannot disagree.
/// </summary>
public static class TaleDebugSnapshotBuilder
{
    public static TaleDebugSnapshot Build(
        TaleManager manager,
        int clusterIndex,
        DateTime gameNow,
        Vector3 playerPos,
        int selectedNpcId,
        bool isPlayersCluster = false)
    {
        var npcs = manager.GetNpcsInCluster(clusterIndex);
        var model = manager.GetSpatialModel(clusterIndex);
        var table = manager.CopyGroupTable(clusterIndex);
        var metrics = manager.ComputeSocialMetrics(clusterIndex);

        int materialized = 0, tier2 = 0, known = 0, inTransit = 0;
        var standing = new List<TaleDebugPlayerTrust>();
        foreach (var npc in npcs)
        {
            if (manager.IsMaterialized(npc.NpcId)) materialized++;
            if (manager.IsTier2(npc.NpcId)) tier2++;
            if (npc.IsKnownToPlayer) known++;
            if (npc.IsInTransit) inTransit++;
            if (npc.Trust != null && npc.Trust.TryGetValue(SocialMetricsCalculator.PlayerTrustKey, out float t))
                standing.Add(new TaleDebugPlayerTrust(npc.NpcId, npc.Role, npc.GroupId, t));
        }
        standing.Sort((a, b) => a.Trust != b.Trust ? b.Trust.CompareTo(a.Trust) : a.NpcId.CompareTo(b.NpcId));

        var groups = _groups(manager, clusterIndex, npcs, table, model, playerPos);
        var matrixIds = groups.Take(TaleDebugSnapshot.MatrixSize).Select(g => g.GroupId).ToList();

        return new TaleDebugSnapshot(
            ClusterIndex: clusterIndex,
            IsPlayersCluster: isPlayersCluster,
            PopulatedClusters: manager.GetPopulatedClusters().OrderBy(i => i).ToList(),
            GameNow: gameNow,
            PlayerPosition: playerPos,
            NpcCount: npcs.Count,
            Materialized: materialized,
            Tier2: tier2,
            KnownToPlayer: known,
            InTransit: inTransit,
            Metrics: metrics,
            ChurnPerDay: manager.GetSocialMetricsSamples(clusterIndex)
                .Select(s => (float)s.MembershipChanges).ToList(),
            Groups: groups,
            MatrixGroupIds: matrixIds,
            InterGroupMatrix: Matrix(matrixIds, metrics),
            PlayerStanding: standing,
            Selected: selectedNpcId >= 0 ? _npc(manager, selectedNpcId) : null);
    }


    /// <summary>
    /// The inter-group matrix over the given groups, read from
    /// SocialMetrics.InterGroupTrust. Null where the groups share no known
    /// pair, and on the diagonal.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<double?>> Matrix(IReadOnlyList<int> groupIds, SocialMetrics metrics)
    {
        var rows = new List<IReadOnlyList<double?>>(groupIds.Count);
        foreach (int a in groupIds)
        {
            var row = new List<double?>(groupIds.Count);
            foreach (int b in groupIds)
            {
                if (a == b) row.Add(null);
                else row.Add(metrics.InterGroupTrust.TryGetValue($"{Math.Min(a, b)}-{Math.Max(a, b)}", out double v)
                    ? v : null);
            }
            rows.Add(row);
        }
        return rows;
    }


    /// <summary>Groups by size, largest first; ties by id.</summary>
    private static List<TaleDebugGroup> _groups(
        TaleManager manager, int clusterIndex, List<NpcSchedule> npcs,
        Dictionary<int, RuntimeGroup> table, SpatialModel model, Vector3 playerPos)
    {
        var byId = npcs.ToDictionary(n => n.NpcId);
        var groups = new List<TaleDebugGroup>();
        foreach (var (groupId, members) in SocialMetricsHistory.RostersOf(npcs))
        {
            table.TryGetValue(groupId, out var g);

            int met = 0;
            double sum = 0;
            foreach (int id in members)
            {
                var trust = byId[id].Trust;
                if (trust != null && trust.TryGetValue(SocialMetricsCalculator.PlayerTrustKey, out float t))
                {
                    met++;
                    sum += t;
                }
            }

            int hangout = g != null ? manager.GetGroupHangout(clusterIndex, groupId) : -1;
            var hangoutLoc = hangout >= 0 ? model?.GetLocation(hangout) : null;

            groups.Add(new TaleDebugGroup(
                GroupId: groupId,
                Name: g?.Name ?? "",
                Type: g?.Type ?? "unknown",
                MemberIds: members,
                HangoutLocationId: hangout,
                HangoutDistance: hangoutLoc != null ? Vector3.Distance(playerPos, hangoutLoc.EntryPosition) : null,
                MembersWhoMetPlayer: met,
                MeanPlayerTrust: met > 0 ? sum / met : null,
                Standing: PlayerStandingCalculator.ForGroup(members.Select(id => byId[id]).ToList()).Standing));
        }

        groups.Sort((a, b) => a.MemberIds.Count != b.MemberIds.Count
            ? b.MemberIds.Count.CompareTo(a.MemberIds.Count)
            : a.GroupId.CompareTo(b.GroupId));
        return groups;
    }


    private static TaleDebugNpc _npc(TaleManager manager, int npcId)
    {
        var npc = manager.GetSchedule(npcId);
        if (npc == null) return null;

        var group = manager.GetGroup(npc.ClusterIndex, npc.GroupId);
        var location = manager.GetSpatialModel(npc.ClusterIndex)?.GetLocation(npc.CurrentLocationId);

        float? playerTrust = null;
        var strongest = new List<KeyValuePair<int, float>>();
        if (npc.Trust != null)
        {
            foreach (var (otherId, trust) in npc.Trust)
            {
                if (otherId == SocialMetricsCalculator.PlayerTrustKey) playerTrust = trust;
                else strongest.Add(new KeyValuePair<int, float>(otherId, trust));
            }
        }
        strongest.Sort((a, b) => a.Value != b.Value ? b.Value.CompareTo(a.Value) : a.Key.CompareTo(b.Key));
        if (strongest.Count > TaleDebugSnapshot.StrongestTrustCount)
            strongest.RemoveRange(TaleDebugSnapshot.StrongestTrustCount, strongest.Count - TaleDebugSnapshot.StrongestTrustCount);

        var properties = npc.Properties != null
            ? npc.Properties.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList()
            : new List<KeyValuePair<string, float>>();

        return new TaleDebugNpc(
            NpcId: npc.NpcId,
            ClusterIndex: npc.ClusterIndex,
            Role: npc.Role,
            CurrentStorylet: npc.CurrentStorylet,
            CurrentStart: npc.CurrentStart,
            CurrentEnd: npc.CurrentEnd,
            CurrentLocationId: npc.CurrentLocationId,
            CurrentLocationType: location?.Type,
            HomeLocationId: npc.HomeLocationId,
            WorkplaceLocationId: npc.WorkplaceLocationId,
            IsInTransit: npc.IsInTransit,
            TransitToLocationId: npc.TransitToLocationId,
            NextForcedStorylet: npc.NextForcedStorylet,
            GroupId: npc.GroupId,
            GroupName: group?.Name,
            GroupType: group?.Type,
            PlayerTrust: playerTrust,
            IsMaterialized: manager.IsMaterialized(npcId),
            IsTier2: manager.IsTier2(npcId),
            HasPlayerDeviation: npc.HasPlayerDeviation,
            IsKnownToPlayer: npc.IsKnownToPlayer,
            Properties: properties,
            StrongestTrust: strongest);
    }
}
