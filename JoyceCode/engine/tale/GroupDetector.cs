using System;
using System.Collections.Generic;
using System.Linq;

namespace engine.tale;

public class DetectedGroup
{
    public int GroupId;
    public string Type; // "criminal", "trade", "social", "patrol_unit"
    public List<int> MemberIds;
}

public class GroupDetectionResult
{
    public int TotalGroups;
    public Dictionary<string, int> GroupsByType = new();
    public int LargestGroupSize;
    public int NpcsInGroups;
    public List<DetectedGroup> Groups = new();
}

/// <summary>
/// Detects emergent groups from the trust graph.
/// Finds cliques of 3+ NPCs with mutual trust > threshold,
/// then classifies by shared property profile.
/// </summary>
public class GroupDetector
{
    public float TrustThreshold = 0.75f;
    public int MinCliqueSize = 3;
    public int MaxCliques = 500;

    private int _nextGroupId = 1;


    public GroupDetectionResult Detect(
        RelationshipTracker relationships,
        IReadOnlyDictionary<int, NpcSchedule> npcs)
    {
        var result = new GroupDetectionResult();

        // Build adjacency for high-trust edges
        var adj = BuildHighTrustAdjacency(relationships);
        if (adj.Count == 0) return result;

        // Find maximal cliques using simplified Bron-Kerbosch
        var cliques = new List<List<int>>();
        BronKerbosch(new List<int>(), new List<int>(adj.Keys), new List<int>(), adj, cliques);

        // Filter to min size and classify
        foreach (var clique in cliques)
        {
            if (clique.Count < MinCliqueSize) continue;

            string type = ClassifyGroup(clique, npcs);
            var group = new DetectedGroup
            {
                GroupId = _nextGroupId++,
                Type = type,
                MemberIds = clique
            };
            result.Groups.Add(group);

            // Assign group IDs to NPCs
            foreach (int id in clique)
            {
                if (npcs.TryGetValue(id, out var npc))
                    npc.GroupId = group.GroupId;
            }
        }

        // Compute summary
        result.TotalGroups = result.Groups.Count;
        var inGroup = new HashSet<int>();
        foreach (var g in result.Groups)
        {
            result.GroupsByType.TryGetValue(g.Type, out int c);
            result.GroupsByType[g.Type] = c + 1;
            if (g.MemberIds.Count > result.LargestGroupSize)
                result.LargestGroupSize = g.MemberIds.Count;
            foreach (int id in g.MemberIds)
                inGroup.Add(id);
        }
        result.NpcsInGroups = inGroup.Count;

        return result;
    }


    private Dictionary<int, HashSet<int>> BuildHighTrustAdjacency(RelationshipTracker relationships)
    {
        var adj = new Dictionary<int, HashSet<int>>();
        foreach (var (key, state) in relationships.AllRelationships)
        {
            float avgTrust = (state.TrustAtoB + state.TrustBtoA) / 2f;
            if (avgTrust < TrustThreshold) continue;

            int a = (int)(key >> 32);
            int b = (int)(key & 0xFFFFFFFF);

            if (!adj.TryGetValue(a, out var sa)) { sa = new HashSet<int>(); adj[a] = sa; }
            if (!adj.TryGetValue(b, out var sb)) { sb = new HashSet<int>(); adj[b] = sb; }
            sa.Add(b);
            sb.Add(a);
        }
        return adj;
    }


    private void BronKerbosch(List<int> R, List<int> P, List<int> X,
        Dictionary<int, HashSet<int>> adj, List<List<int>> results)
    {
        if (results.Count >= MaxCliques) return;

        if (P.Count == 0 && X.Count == 0)
        {
            if (R.Count >= MinCliqueSize)
                results.Add(new List<int>(R));
            return;
        }

        // Pivot: choose vertex with most connections in P∪X
        int pivot = -1;
        int maxDeg = -1;
        foreach (int v in P)
        {
            int deg = adj.TryGetValue(v, out var n) ? n.Count : 0;
            if (deg > maxDeg) { maxDeg = deg; pivot = v; }
        }
        foreach (int v in X)
        {
            int deg = adj.TryGetValue(v, out var n) ? n.Count : 0;
            if (deg > maxDeg) { maxDeg = deg; pivot = v; }
        }

        var pivotNeighbors = pivot >= 0 && adj.TryGetValue(pivot, out var pn) ? pn : new HashSet<int>();

        var candidates = new List<int>();
        foreach (int v in P)
        {
            if (!pivotNeighbors.Contains(v))
                candidates.Add(v);
        }

        foreach (int v in candidates)
        {
            var neighbors = adj.TryGetValue(v, out var vn) ? vn : new HashSet<int>();

            R.Add(v);
            var newP = new List<int>();
            foreach (int p in P) { if (neighbors.Contains(p)) newP.Add(p); }
            var newX = new List<int>();
            foreach (int x in X) { if (neighbors.Contains(x)) newX.Add(x); }

            BronKerbosch(R, newP, newX, adj, results);

            R.RemoveAt(R.Count - 1);
            P.Remove(v);
            X.Add(v);
        }
    }


    private static string ClassifyGroup(List<int> memberIds, IReadOnlyDictionary<int, NpcSchedule> npcs)
    {
        float avgWealth = 0, avgMorality = 0, avgAnger = 0, avgReputation = 0;
        int authorityCount = 0;
        int count = 0;

        foreach (int id in memberIds)
        {
            if (!npcs.TryGetValue(id, out var npc)) continue;
            avgWealth += npc.Properties.GetValueOrDefault("wealth", 0.5f);
            avgMorality += npc.Properties.GetValueOrDefault("morality", 0.7f);
            avgAnger += npc.Properties.GetValueOrDefault("anger", 0f);
            avgReputation += npc.Properties.GetValueOrDefault("reputation", 0.5f);
            if (npc.Role == "Authority") authorityCount++;
            count++;
        }

        if (count == 0) return "social";
        avgWealth /= count;
        avgMorality /= count;
        avgAnger /= count;
        avgReputation /= count;

        if (authorityCount > count / 2) return "patrol_unit";
        if (avgWealth < 0.3f && avgMorality < 0.4f) return "criminal";
        if (avgWealth > 0.5f && avgReputation > 0.5f) return "trade";
        return "social";
    }
}
