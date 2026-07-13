using System;
using System.Collections.Generic;
using System.Linq;
using static engine.Logger;

namespace engine.tale;

/// <summary>
/// Storylet group verbs (TALE-SOCIAL Phase E4): the authored counterpart to
/// detection-driven faction emergence. A storylet declares
/// <c>"postconditions": { "group": "form" | "join" | "leave" }</c> and the
/// verb executes when the storylet completes.
///
/// Shared by both call sites: TaleManager.AdvanceNpc (live game, table =
/// the cluster's ClusterSocialState) and DesSimulation.ProcessNodeArrival
/// (bake/tests, table = a sim-local ClusterSocialState). Fully
/// deterministic: candidate ordering is (trust desc, NpcId asc), no RNG.
///
/// This is what breaks the old circularity where form_gang REQUIRED being
/// in a group: form now creates one from the NPC's highest-trust ungrouped
/// neighbors, then the in_group-gated escalation chain can fire.
/// </summary>
public static class GroupActions
{
    private static readonly engine.Dc _dc = engine.Dc.TaleSocial;

    /// <summary>Minimum mutual trust to be recruited into a forming group.</summary>
    public const float FormTrustThreshold = 0.35f;

    /// <summary>Minimum mutual trust toward a member to join their group.</summary>
    public const float JoinTrustThreshold = 0.3f;

    /// <summary>Founder + up to this many partners.</summary>
    public const int MaxFormPartners = 4;

    /// <summary>Form needs at least this many partners besides the founder.</summary>
    public const int MinFormPartners = 2;


    /// <summary>
    /// Execute a group verb for <paramref name="self"/>. Returns the affected
    /// group id, or -1 when the verb no-ops (already/not grouped, not enough
    /// trusted partners...). Mutates NpcSchedule.GroupId of everyone involved
    /// and the group table; the caller owns any locking.
    /// </summary>
    public static int Apply(
        string action,
        NpcSchedule self,
        IReadOnlyDictionary<int, NpcSchedule> clusterNpcs,
        ClusterSocialState social,
        Func<int, int, string, string> nameProvider,
        IEventLogger logger, int day, DateTime gameTime)
    {
        if (self == null || social == null) return -1;
        return action switch
        {
            "form" => Form(self, clusterNpcs, social, nameProvider, logger, day, gameTime),
            "join" => Join(self, clusterNpcs, social),
            "leave" => Leave(self, clusterNpcs, social),
            _ => -1
        };
    }


    private static int Form(
        NpcSchedule self,
        IReadOnlyDictionary<int, NpcSchedule> clusterNpcs,
        ClusterSocialState social,
        Func<int, int, string, string> nameProvider,
        IEventLogger logger, int day, DateTime gameTime)
    {
        if (self.GroupId != -1) return -1;

        // Recruit the founder's most-trusted ungrouped neighbors.
        var candidates = new List<(int id, float trust)>();
        foreach (var (id, npc) in clusterNpcs)
        {
            if (id == self.NpcId || npc.GroupId != -1) continue;
            float trust = MutualTrust(self, npc);
            if (trust >= FormTrustThreshold)
                candidates.Add((id, trust));
        }
        candidates.Sort((a, b) =>
        {
            int c = b.trust.CompareTo(a.trust);
            return c != 0 ? c : a.id.CompareTo(b.id);
        });

        if (candidates.Count < MinFormPartners) return -1;
        var partners = candidates.Take(MaxFormPartners).Select(c => c.id).ToList();

        var memberIds = new List<int>(partners) { self.NpcId };
        memberIds.Sort();

        string type = Classify(memberIds, clusterNpcs);
        int groupId = social.NextGroupId++;
        social.Groups[groupId] = new RuntimeGroup
        {
            GroupId = groupId,
            Type = type,
            Name = nameProvider?.Invoke(self.ClusterIndex, groupId, type) ?? "",
            MemberIds = memberIds,
            Authored = true
        };

        foreach (int id in memberIds)
        {
            if (clusterNpcs.TryGetValue(id, out var npc))
                npc.GroupId = groupId;
        }

        logger?.LogGangFormed(groupId, memberIds, type, day, gameTime);
        Trace(_dc, $"Group formed: npc {self.NpcId} founded group {groupId} [{type}] " +
              $"with members [{string.Join(",", memberIds)}]");
        return groupId;
    }


    private static int Join(
        NpcSchedule self,
        IReadOnlyDictionary<int, NpcSchedule> clusterNpcs,
        ClusterSocialState social)
    {
        if (self.GroupId != -1) return -1;

        // Adopt the group of the most-trusted grouped neighbor.
        int bestId = -1;
        float bestTrust = -1f;
        foreach (var (id, npc) in clusterNpcs)
        {
            if (id == self.NpcId || npc.GroupId == -1) continue;
            if (!social.Groups.ContainsKey(npc.GroupId)) continue;
            float trust = MutualTrust(self, npc);
            if (trust < JoinTrustThreshold) continue;
            if (trust > bestTrust || (trust == bestTrust && id < bestId))
            {
                bestTrust = trust;
                bestId = id;
            }
        }
        if (bestId == -1) return -1;

        var group = social.Groups[clusterNpcs[bestId].GroupId];
        self.GroupId = group.GroupId;
        if (!group.MemberIds.Contains(self.NpcId))
        {
            group.MemberIds.Add(self.NpcId);
            group.MemberIds.Sort();
        }
        Trace(_dc, $"Group join: npc {self.NpcId} joined group {group.GroupId} " +
              $"[{group.Type}] via npc {bestId} (trust {bestTrust:F2})");
        return group.GroupId;
    }


    private static int Leave(
        NpcSchedule self,
        IReadOnlyDictionary<int, NpcSchedule> clusterNpcs,
        ClusterSocialState social)
    {
        if (self.GroupId == -1) return -1;
        int groupId = self.GroupId;
        self.GroupId = -1;

        if (!social.Groups.TryGetValue(groupId, out var group)) return groupId;
        group.MemberIds.Remove(self.NpcId);

        // A "group" of one is no group: dissolve.
        if (group.MemberIds.Count < 2)
        {
            foreach (int id in group.MemberIds)
            {
                if (clusterNpcs.TryGetValue(id, out var npc) && npc.GroupId == groupId)
                    npc.GroupId = -1;
            }
            social.Groups.Remove(groupId);
            Trace(_dc, $"Group leave: npc {self.NpcId} left group {groupId}; group dissolved.");
        }
        else
        {
            Trace(_dc, $"Group leave: npc {self.NpcId} left group {groupId} " +
                  $"({group.MemberIds.Count} members remain).");
        }
        return groupId;
    }


    private static float MutualTrust(NpcSchedule a, NpcSchedule b)
    {
        float ab = a.Trust != null && a.Trust.TryGetValue(b.NpcId, out float t1) ? t1 : 0f;
        float ba = b.Trust != null && b.Trust.TryGetValue(a.NpcId, out float t2) ? t2 : 0f;
        return (ab + ba) / 2f;
    }


    private static string Classify(List<int> memberIds, IReadOnlyDictionary<int, NpcSchedule> npcs)
    {
        try
        {
            var registry = I.Get<GroupTypeRegistry>();
            if (registry != null)
                return registry.ClassifyGroup(memberIds, npcs);
        }
        catch
        {
            // Registry not available (bare unit tests).
        }
        return "social";
    }
}
