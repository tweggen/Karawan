using System;
using System.Collections.Generic;
using System.Linq;
using static engine.Logger;

namespace engine.tale.bake;

/// <summary>
/// Re-attaches a baked <see cref="Scenario"/>'s social structure (group
/// memberships, relationship trust edges, role-tuned property snapshots) onto a
/// freshly populated cluster's real <see cref="NpcSchedule"/>s.
///
/// The bake stored everything by stable rank (0..N-1, sorted by NpcId at export
/// time), so reattachment is a two-step process:
///
/// 1. Build a rank-to-real-NpcId map by bucketing both populations by role and
///    pairing positionally inside each bucket. The within-role sort key is
///    (wealth desc, morality desc, NpcId asc) — that means the richest worker in
///    the scenario lands on the richest worker in the real cluster, regardless of
///    population size mismatch. The wealth/morality used for sorting comes from
///    the values TalePopulationGenerator produced, since the applicator is the
///    thing that *replaces* them.
///
/// 2. Walk scenario.Npcs / scenario.Groups / scenario.Relationships and rewrite
///    real NPC state via the map. Unmatched ranks (e.g. scenario has 10 drifters
///    but real cluster has only 6) are silently dropped on the scenario side, and
///    unmatched real NPCs (e.g. real cluster has more workers than the scenario)
///    keep their generator-assigned properties and an unset GroupId. Trust edges
///    that touch any unmatched rank are skipped.
///
/// There is no global RelationshipTracker at runtime — TaleManager stores trust
/// per-NPC inside <c>NpcSchedule.Trust</c>, so the applicator writes both
/// directions of each scenario edge directly into the matched NPCs' Trust dicts.
/// </summary>
public class ScenarioApplicator
{
    /// <summary>
    /// Property keys overwritten by scenario application. Aligned with what
    /// <see cref="ScenarioExporter"/> emits and what TalePopulationGenerator
    /// initializes per role. Other properties (hunger, health, fatigue, trust,
    /// happiness) are left untouched so the warmup day cycle's state survives.
    /// </summary>
    private static readonly string[] OverwrittenProperties =
        { "morality", "wealth", "fear", "anger", "reputation" };


    public sealed class ApplyResult
    {
        public int MatchedNpcCount;
        public int UnmatchedScenarioRanks;
        public int UnmatchedRealNpcs;
        public int RelationshipsApplied;
        public int RelationshipsSkipped;
        public int GroupsTouched;
        public Dictionary<string, int> MatchedByRole = new();
    }


    /// <summary>
    /// Apply <paramref name="scenario"/> to the <paramref name="realNpcs"/>
    /// list. Mutates the schedules in place: overwrites a small subset of
    /// properties, sets <c>GroupId</c>, and populates each NPC's
    /// <c>Trust</c> dictionary. Returns a stats object so the caller can log
    /// what happened. No-op (with stats) when <paramref name="scenario"/> or
    /// <paramref name="realNpcs"/> is null/empty.
    /// </summary>
    public ApplyResult Apply(Scenario scenario, IReadOnlyList<NpcSchedule> realNpcs)
    {
        var result = new ApplyResult();
        if (scenario == null || realNpcs == null || realNpcs.Count == 0)
        {
            return result;
        }

        // ----- Step 1: build the rank → real NpcId map -----

        // Bucket scenario NPCs by role, sorted within each bucket.
        var scenarioByRole = new Dictionary<string, List<ScenarioNpc>>();
        foreach (var sn in scenario.Npcs)
        {
            if (string.IsNullOrEmpty(sn.Role)) continue;
            if (!scenarioByRole.TryGetValue(sn.Role, out var list))
            {
                list = new List<ScenarioNpc>();
                scenarioByRole[sn.Role] = list;
            }
            list.Add(sn);
        }
        foreach (var kvp in scenarioByRole)
        {
            kvp.Value.Sort(CompareScenarioNpcByPropertyAndRank);
        }

        // Bucket real NPCs by role, sorted with the same comparator semantics.
        var realByRole = new Dictionary<string, List<NpcSchedule>>();
        foreach (var rn in realNpcs)
        {
            if (string.IsNullOrEmpty(rn.Role)) continue;
            if (!realByRole.TryGetValue(rn.Role, out var list))
            {
                list = new List<NpcSchedule>();
                realByRole[rn.Role] = list;
            }
            list.Add(rn);
        }
        foreach (var kvp in realByRole)
        {
            kvp.Value.Sort(CompareRealNpcByPropertyAndId);
        }

        // Pair positionally inside each role bucket.
        var rankToRealId = new Dictionary<int, int>();
        var realIdToRank = new Dictionary<int, int>();
        foreach (var role in scenarioByRole.Keys)
        {
            var scenarioList = scenarioByRole[role];
            if (!realByRole.TryGetValue(role, out var realList) || realList.Count == 0)
            {
                // No real NPCs of this role at all — entire scenario bucket
                // overflows.
                result.UnmatchedScenarioRanks += scenarioList.Count;
                continue;
            }

            int paired = Math.Min(scenarioList.Count, realList.Count);
            for (int i = 0; i < paired; i++)
            {
                int rank = scenarioList[i].Rank;
                int realId = realList[i].NpcId;
                rankToRealId[rank] = realId;
                realIdToRank[realId] = rank;
            }
            result.UnmatchedScenarioRanks += Math.Max(0, scenarioList.Count - paired);
            result.MatchedNpcCount += paired;
            result.MatchedByRole[role] = paired;
        }

        // Count real NPCs that did not get paired with any scenario rank.
        foreach (var rn in realNpcs)
        {
            if (!realIdToRank.ContainsKey(rn.NpcId))
                result.UnmatchedRealNpcs++;
        }

        // Build a fast lookup of scenarioRank → ScenarioNpc for the property
        // overwrite phase.
        var scenarioByRank = new Dictionary<int, ScenarioNpc>(scenario.Npcs.Count);
        foreach (var sn in scenario.Npcs)
            scenarioByRank[sn.Rank] = sn;

        // Build a fast lookup of NpcId → NpcSchedule for the relationship phase.
        var realById = new Dictionary<int, NpcSchedule>(realNpcs.Count);
        foreach (var rn in realNpcs)
            realById[rn.NpcId] = rn;

        // ----- Step 2: rewrite real NPC state from the matched ranks -----

        var groupsTouched = new HashSet<int>();
        foreach (var (rank, realId) in rankToRealId)
        {
            if (!scenarioByRank.TryGetValue(rank, out var sn)) continue;
            if (!realById.TryGetValue(realId, out var rn)) continue;

            // Properties: overwrite the social-meaningful subset.
            rn.Properties ??= new Dictionary<string, float>();
            if (sn.Properties != null)
            {
                foreach (var key in OverwrittenProperties)
                {
                    if (sn.Properties.TryGetValue(key, out float v))
                    {
                        rn.Properties[key] = Math.Clamp(v, 0f, 1f);
                    }
                }
            }

            // Group: stash the scenario's group rank as the runtime GroupId.
            // GroupRank == -1 means the NPC was not in any clique at bake time;
            // leave the runtime GroupId at its default (-1) in that case.
            if (sn.GroupRank >= 0)
            {
                rn.GroupId = sn.GroupRank;
                groupsTouched.Add(sn.GroupRank);
            }
        }
        result.GroupsTouched = groupsTouched.Count;

        // Relationships: rewrite both directions of each scenario edge into the
        // per-NPC Trust dictionaries.
        foreach (var rel in scenario.Relationships)
        {
            if (!rankToRealId.TryGetValue(rel.FromRank, out int realA) ||
                !rankToRealId.TryGetValue(rel.ToRank, out int realB))
            {
                result.RelationshipsSkipped++;
                continue;
            }
            if (!realById.TryGetValue(realA, out var npcA) ||
                !realById.TryGetValue(realB, out var npcB))
            {
                result.RelationshipsSkipped++;
                continue;
            }

            npcA.Trust ??= new Dictionary<int, float>();
            npcB.Trust ??= new Dictionary<int, float>();
            npcA.Trust[realB] = Math.Clamp(rel.TrustAtoB, 0f, 1f);
            npcB.Trust[realA] = Math.Clamp(rel.TrustBtoA, 0f, 1f);
            result.RelationshipsApplied++;
        }

        return result;
    }


    /// <summary>
    /// Within a role bucket, prefer high-wealth, high-morality, low-rank NPCs.
    /// The wealth/morality keys come from the bake's post-365-day snapshot, so
    /// the ordering is "this NPC was rich and lawful at the end of the
    /// simulation".
    /// </summary>
    private static int CompareScenarioNpcByPropertyAndRank(ScenarioNpc a, ScenarioNpc b)
    {
        float wa = PropOr(a.Properties, "wealth", 0f);
        float wb = PropOr(b.Properties, "wealth", 0f);
        int c = wb.CompareTo(wa);
        if (c != 0) return c;

        float ma = PropOr(a.Properties, "morality", 0f);
        float mb = PropOr(b.Properties, "morality", 0f);
        c = mb.CompareTo(ma);
        if (c != 0) return c;

        return a.Rank.CompareTo(b.Rank);
    }


    /// <summary>
    /// Same idea on the real-NPC side, with NpcId as the final tiebreaker.
    /// </summary>
    private static int CompareRealNpcByPropertyAndId(NpcSchedule a, NpcSchedule b)
    {
        float wa = PropOr(a.Properties, "wealth", 0f);
        float wb = PropOr(b.Properties, "wealth", 0f);
        int c = wb.CompareTo(wa);
        if (c != 0) return c;

        float ma = PropOr(a.Properties, "morality", 0f);
        float mb = PropOr(b.Properties, "morality", 0f);
        c = mb.CompareTo(ma);
        if (c != 0) return c;

        return a.NpcId.CompareTo(b.NpcId);
    }


    private static float PropOr(Dictionary<string, float> props, string key, float fallback)
    {
        if (props == null) return fallback;
        return props.TryGetValue(key, out float v) ? v : fallback;
    }
}
