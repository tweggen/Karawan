using System;
using System.Collections.Generic;
using System.Linq;

namespace engine.tale;

/// <summary>
/// Detects social communities from the trust graph via deterministic label
/// propagation. Replaces GroupDetector's maximal-clique enumeration for the
/// TALE-SOCIAL pipeline: cliques produced hundreds of overlapping 3-member
/// groups with near-100% NPC membership ("clique soup"), while label
/// propagation yields a handful of DISJOINT communities — the shape a
/// narrative faction actually has.
///
/// Determinism: no RNG. Nodes are iterated in ascending NpcId order; a node
/// adopts the neighbor label with the highest summed edge weight, ties broken
/// by the lowest label id. Same edges in → byte-identical communities out,
/// which the scenario bake's seedability contract requires.
///
/// Reuses GroupDetectionResult / DetectedGroup so SimMetrics.OnGroupDetection
/// and ScenarioExporter.Build consume it unchanged.
/// </summary>
public class CommunityDetector
{
    /// <summary>
    /// Minimum average pair trust for an edge to enter the community graph.
    /// Lower than GroupDetector's 0.75 clique threshold: communities do not
    /// require complete subgraphs, so each edge carries less weight.
    /// </summary>
    public float TrustThreshold = 0.6f;

    public int MinCommunitySize = 3;

    /// <summary>
    /// Mutual top-K sparsification: an edge survives only if BOTH endpoints
    /// rank it among their K strongest ties. The 365-day bake produces a
    /// near-complete high-trust graph, on which raw label propagation
    /// collapses into one giant community; keeping only each NPC's closest
    /// handful of ties is both the standard dense-graph remedy and the
    /// socially realistic one. 0 disables sparsification.
    /// </summary>
    public int MaxEdgesPerNode = 6;

    /// <summary>
    /// Label propagation converges in a handful of sweeps on graphs this
    /// size; the cap is a safety net against oscillation.
    /// </summary>
    public int MaxIterations = 20;

    private int _nextGroupId = 1;


    /// <summary>Bake/DES entry point: edges come from the RelationshipTracker.</summary>
    public GroupDetectionResult Detect(
        RelationshipTracker relationships,
        IReadOnlyDictionary<int, NpcSchedule> npcs)
    {
        var edges = new List<(int a, int b, float w)>();
        foreach (var (key, state) in relationships.AllRelationships)
        {
            float avgTrust = (state.TrustAtoB + state.TrustBtoA) / 2f;
            if (avgTrust < TrustThreshold) continue;
            edges.Add(((int)(key >> 32), (int)(key & 0xFFFFFFFF), avgTrust));
        }
        return DetectFromEdges(edges, npcs);
    }


    /// <summary>
    /// Runtime entry point: edges come from the per-NPC Trust dictionaries
    /// (there is no RelationshipTracker in the live game). Trust ids not
    /// present in the npcs dict (e.g. -1 = player) are ignored.
    /// </summary>
    public GroupDetectionResult DetectFromSchedules(IReadOnlyDictionary<int, NpcSchedule> npcs)
    {
        var edges = new List<(int a, int b, float w)>();
        foreach (var npc in npcs.Values)
        {
            if (npc.Trust == null) continue;
            foreach (var (otherId, trustAtoB) in npc.Trust)
            {
                // Emit each pair once, from the lower id.
                if (otherId <= npc.NpcId) continue;
                if (!npcs.TryGetValue(otherId, out var other)) continue;

                float trustBtoA = other.Trust != null &&
                                  other.Trust.TryGetValue(npc.NpcId, out float t)
                    ? t : trustAtoB;
                float avgTrust = (trustAtoB + trustBtoA) / 2f;
                if (avgTrust < TrustThreshold) continue;
                edges.Add((npc.NpcId, otherId, avgTrust));
            }
        }
        return DetectFromEdges(edges, npcs);
    }


    private GroupDetectionResult DetectFromEdges(
        List<(int a, int b, float w)> edges,
        IReadOnlyDictionary<int, NpcSchedule> npcs)
    {
        var result = new GroupDetectionResult();

        // Communities are disjoint and authoritative: clear stale membership
        // so NPCs whose community dissolved don't keep a dangling GroupId.
        foreach (var npc in npcs.Values)
            npc.GroupId = -1;

        if (edges.Count == 0) return result;

        if (MaxEdgesPerNode > 0)
            edges = SparsifyMutualTopK(edges);
        if (edges.Count == 0) return result;

        // Weighted adjacency.
        var adj = new Dictionary<int, List<(int other, float w)>>();
        foreach (var (a, b, w) in edges)
        {
            if (!adj.TryGetValue(a, out var la)) { la = new List<(int, float)>(); adj[a] = la; }
            if (!adj.TryGetValue(b, out var lb)) { lb = new List<(int, float)>(); adj[b] = lb; }
            la.Add((b, w));
            lb.Add((a, w));
        }

        // Ascending-NpcId sweep order makes the whole run order-deterministic.
        var nodes = adj.Keys.ToList();
        nodes.Sort();

        var label = new Dictionary<int, int>(nodes.Count);
        foreach (int n in nodes) label[n] = n;

        var weightByLabel = new Dictionary<int, float>();
        for (int iter = 0; iter < MaxIterations; iter++)
        {
            bool changed = false;
            foreach (int n in nodes)
            {
                weightByLabel.Clear();
                foreach (var (other, w) in adj[n])
                {
                    int l = label[other];
                    weightByLabel.TryGetValue(l, out float acc);
                    weightByLabel[l] = acc + w;
                }

                // Adopt the strongest neighbor label; tie-break by lowest id.
                int bestLabel = label[n];
                float bestWeight = -1f;
                foreach (var (l, w) in weightByLabel)
                {
                    if (w > bestWeight || (w == bestWeight && l < bestLabel))
                    {
                        bestWeight = w;
                        bestLabel = l;
                    }
                }

                if (bestLabel != label[n])
                {
                    label[n] = bestLabel;
                    changed = true;
                }
            }
            if (!changed) break;
        }

        // Gather communities; order by lowest member id for stable GroupIds.
        var byLabel = new Dictionary<int, List<int>>();
        foreach (int n in nodes)
        {
            if (!byLabel.TryGetValue(label[n], out var members))
            {
                members = new List<int>();
                byLabel[label[n]] = members;
            }
            members.Add(n);
        }

        var communities = byLabel.Values
            .Where(m => m.Count >= MinCommunitySize)
            .OrderBy(m => m.Min())
            .ToList();

        foreach (var members in communities)
        {
            members.Sort();
            var group = new DetectedGroup
            {
                GroupId = _nextGroupId++,
                Type = ClassifyGroup(members, npcs),
                MemberIds = members
            };
            result.Groups.Add(group);

            foreach (int id in members)
            {
                if (npcs.TryGetValue(id, out var npc))
                    npc.GroupId = group.GroupId;
            }
        }

        result.TotalGroups = result.Groups.Count;
        foreach (var g in result.Groups)
        {
            result.GroupsByType.TryGetValue(g.Type, out int c);
            result.GroupsByType[g.Type] = c + 1;
            if (g.MemberIds.Count > result.LargestGroupSize)
                result.LargestGroupSize = g.MemberIds.Count;
            result.NpcsInGroups += g.MemberIds.Count;
        }

        return result;
    }


    /// <summary>
    /// Keep an edge only if both endpoints rank it among their
    /// MaxEdgesPerNode strongest. Deterministic: per-node ranking sorts by
    /// (weight desc, other id asc).
    /// </summary>
    private List<(int a, int b, float w)> SparsifyMutualTopK(List<(int a, int b, float w)> edges)
    {
        var perNode = new Dictionary<int, List<(int other, float w)>>();
        foreach (var (a, b, w) in edges)
        {
            if (!perNode.TryGetValue(a, out var la)) { la = new List<(int, float)>(); perNode[a] = la; }
            if (!perNode.TryGetValue(b, out var lb)) { lb = new List<(int, float)>(); perNode[b] = lb; }
            la.Add((b, w));
            lb.Add((a, w));
        }

        // An edge is "nominated" by an endpoint when it falls inside that
        // node's top K; it survives only with nominations from both ends.
        var nominations = new Dictionary<long, int>();
        foreach (var (node, neighbors) in perNode)
        {
            neighbors.Sort((x, y) =>
            {
                int c = y.w.CompareTo(x.w);
                return c != 0 ? c : x.other.CompareTo(y.other);
            });
            int k = Math.Min(MaxEdgesPerNode, neighbors.Count);
            for (int i = 0; i < k; i++)
            {
                int other = neighbors[i].other;
                long key = node < other
                    ? ((long)node << 32) | (uint)other
                    : ((long)other << 32) | (uint)node;
                nominations.TryGetValue(key, out int c);
                nominations[key] = c + 1;
            }
        }

        var kept = new List<(int a, int b, float w)>();
        foreach (var (a, b, w) in edges)
        {
            long key = a < b
                ? ((long)a << 32) | (uint)b
                : ((long)b << 32) | (uint)a;
            if (nominations.TryGetValue(key, out int c) && c >= 2)
                kept.Add((a, b, w));
        }
        return kept;
    }


    private static string ClassifyGroup(List<int> memberIds, IReadOnlyDictionary<int, NpcSchedule> npcs)
    {
        try
        {
            var registry = I.Get<GroupTypeRegistry>();
            if (registry != null)
                return registry.ClassifyGroup(memberIds, npcs);
        }
        catch
        {
            // Registry not available (bare unit tests): everything is social.
        }
        return "social";
    }
}
