using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace engine.tale.bake;

/// <summary>
/// Data-only representation of a baked TALE scenario. Captures the social
/// structure that emerges from a 365-day DES run, in a form that is independent
/// of the spatial cluster the simulation actually used.
///
/// Serialized as JSON to {generated}/sc-{hash}. Read back at runtime by Phase
/// D2's ScenarioLibrary, and re-attached to real cluster NPCs by Phase D3.
///
/// The schema is rank-based: every NPC in the scenario has a stable integer
/// "rank" (0..N-1, sorted by NpcId at export time). Groups and relationships
/// reference NPCs by rank, never by raw simulation NpcId — that lets the
/// runtime applicator remap them onto a fresh population without preserving the
/// internal cluster encoding.
/// </summary>
public class Scenario
{
    public int Version { get; set; } = 1;
    public string Category { get; set; }
    public int Index { get; set; }
    public int Seed { get; set; }
    public int NpcCount { get; set; }
    public int SimulationDays { get; set; }
    public List<ScenarioNpc> Npcs { get; set; } = new();
    public List<ScenarioGroup> Groups { get; set; } = new();
    public List<ScenarioRelationship> Relationships { get; set; } = new();
    public Dictionary<string, ScenarioHistogram> Histograms { get; set; } = new();
}

public class ScenarioNpc
{
    /// <summary>Stable rank within the scenario (0..NpcCount-1).</summary>
    public int Rank { get; set; }
    public string Role { get; set; }
    public Dictionary<string, float> Properties { get; set; } = new();
    /// <summary>Group rank (-1 if not in any group). Indexes into Scenario.Groups.</summary>
    public int GroupRank { get; set; } = -1;
}

public class ScenarioGroup
{
    /// <summary>Stable rank within the scenario (0..GroupCount-1).</summary>
    public int Rank { get; set; }
    public string Type { get; set; }
    public List<int> MemberRanks { get; set; } = new();
}

public class ScenarioRelationship
{
    public int FromRank { get; set; }
    public int ToRank { get; set; }
    public float TrustAtoB { get; set; }
    public float TrustBtoA { get; set; }
    public int InteractionCount { get; set; }
}

public class ScenarioHistogram
{
    /// <summary>Bin upper bounds; counts[i] holds NPCs with property in (bins[i-1], bins[i]].</summary>
    public List<float> Bins { get; set; } = new();
    public List<int> Counts { get; set; } = new();
}


/// <summary>
/// Convert a finished DesSimulation into a Scenario, write Scenario JSON, read it back.
/// Build-time (Chushi) and runtime fallback (Phase D2 ScenarioLibrary) both call this.
/// </summary>
public static class ScenarioExporter
{
    private static readonly JsonSerializerOptions _writeOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions _readOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Build a Scenario object from a finished DesSimulation. Caller fills the
    /// Category/Index/Seed/SimulationDays metadata.
    /// </summary>
    public static Scenario Build(
        string category, int index, int seed, int simulationDays,
        DesSimulation sim)
    {
        var scenario = new Scenario
        {
            Category = category,
            Index = index,
            Seed = seed,
            SimulationDays = simulationDays,
            NpcCount = sim.Npcs.Count
        };

        // Stable rank assignment: sort NPCs by NpcId so rank ordering is reproducible.
        var orderedNpcs = sim.Npcs.Values.OrderBy(n => n.NpcId).ToList();
        var npcIdToRank = new Dictionary<int, int>(orderedNpcs.Count);
        for (int rank = 0; rank < orderedNpcs.Count; rank++)
            npcIdToRank[orderedNpcs[rank].NpcId] = rank;

        // Group rank assignment: deterministic ordering by GroupId so re-runs are stable.
        var groupIdToRank = new Dictionary<int, int>();
        if (sim.LastGroupDetection != null)
        {
            int gRank = 0;
            foreach (var group in sim.LastGroupDetection.Groups.OrderBy(g => g.GroupId))
            {
                groupIdToRank[group.GroupId] = gRank;
                scenario.Groups.Add(new ScenarioGroup
                {
                    Rank = gRank,
                    Type = group.Type,
                    MemberRanks = group.MemberIds
                        .Where(id => npcIdToRank.ContainsKey(id))
                        .Select(id => npcIdToRank[id])
                        .OrderBy(r => r)
                        .ToList()
                });
                gRank++;
            }
        }

        // Per-NPC export.
        foreach (var npc in orderedNpcs)
        {
            int rank = npcIdToRank[npc.NpcId];
            int groupRank = -1;
            if (npc.GroupId >= 0 && groupIdToRank.TryGetValue(npc.GroupId, out int gr))
                groupRank = gr;

            // Copy only the social-meaningful property subset; everything else
            // is regenerated by the population generator at apply time.
            var propsCopy = new Dictionary<string, float>();
            foreach (var key in new[] { "morality", "wealth", "fear", "anger", "reputation" })
            {
                if (npc.Properties != null && npc.Properties.TryGetValue(key, out float v))
                    propsCopy[key] = v;
            }

            scenario.Npcs.Add(new ScenarioNpc
            {
                Rank = rank,
                Role = npc.Role,
                Properties = propsCopy,
                GroupRank = groupRank
            });
        }

        // Relationships: emit each pair once, addressed by rank.
        if (sim.Relationships != null)
        {
            foreach (var kvp in sim.Relationships.AllRelationships)
            {
                long pair = kvp.Key;
                int aId = (int)(pair >> 32);
                int bId = (int)(pair & 0xFFFFFFFFL);
                if (!npcIdToRank.TryGetValue(aId, out int aRank)) continue;
                if (!npcIdToRank.TryGetValue(bId, out int bRank)) continue;

                var state = kvp.Value;
                scenario.Relationships.Add(new ScenarioRelationship
                {
                    FromRank = aRank,
                    ToRank = bRank,
                    TrustAtoB = state.TrustAtoB,
                    TrustBtoA = state.TrustBtoA,
                    InteractionCount = state.TotalInteractions
                });
            }
        }

        // Histograms over the social-meaningful properties.
        foreach (var key in new[] { "morality", "wealth", "fear", "anger", "reputation" })
        {
            scenario.Histograms[key] = BuildHistogram(orderedNpcs, key, binCount: 10);
        }

        return scenario;
    }


    private static ScenarioHistogram BuildHistogram(
        IReadOnlyList<NpcSchedule> npcs, string property, int binCount)
    {
        var hist = new ScenarioHistogram();
        for (int i = 1; i <= binCount; i++)
            hist.Bins.Add((float)i / binCount);
        for (int i = 0; i < binCount; i++)
            hist.Counts.Add(0);

        foreach (var npc in npcs)
        {
            if (npc.Properties == null) continue;
            if (!npc.Properties.TryGetValue(property, out float v)) continue;
            v = Math.Clamp(v, 0f, 1f);
            int bin = Math.Min(binCount - 1, (int)(v * binCount));
            hist.Counts[bin]++;
        }
        return hist;
    }


    public static void WriteToFile(Scenario scenario, string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        JsonSerializer.Serialize(fs, scenario, _writeOptions);
    }

    public static Scenario ReadFromStream(Stream stream)
    {
        return JsonSerializer.Deserialize<Scenario>(stream, _readOptions);
    }
}
