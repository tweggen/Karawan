using System;
using System.Collections.Generic;
using System.Numerics;
using builtin.tools;
using engine.world;

namespace engine.tale;

/// <summary>
/// Deterministically generates NPC population for a cluster from its seed.
/// Each NPC's seed is independent (string-concat, not sequential RNG),
/// so skipping deviated indices does not affect other NPCs.
/// </summary>
public class TalePopulationGenerator
{
    private static readonly string[] Roles = { "worker", "merchant", "socialite", "drifter", "authority" };

    /// <summary>
    /// Role distribution weights. Downtown-heavy clusters shift toward merchant/authority.
    /// </summary>
    private static readonly float[] DefaultRoleWeights = { 0.40f, 0.15f, 0.20f, 0.15f, 0.10f };


    /// <summary>
    /// Generate NPC schedules for a cluster, skipping deviated NPC indices.
    /// </summary>
    /// <param name="clusterDesc">The cluster to populate.</param>
    /// <param name="skipIndices">NPC indices to skip (deviated NPCs loaded from save).</param>
    /// <returns>List of generated NpcSchedule objects (excludes skipped indices).</returns>
    public List<NpcSchedule> Generate(ClusterDesc clusterDesc, HashSet<int> skipIndices = null)
    {
        int clusterIndex = clusterDesc.Index;
        string clusterSeed = clusterDesc.GetKey();

        int npcCount = ComputeNpcCount(clusterDesc);
        var streetPoints = CollectStreetPoints(clusterDesc);

        if (streetPoints.Count == 0)
            return new List<NpcSchedule>();

        var schedules = new List<NpcSchedule>(npcCount);

        for (int i = 0; i < npcCount; i++)
        {
            if (skipIndices != null && skipIndices.Contains(i))
                continue;

            var schedule = GenerateNpc(clusterIndex, i, clusterSeed, clusterDesc, streetPoints);
            schedules.Add(schedule);
        }

        return schedules;
    }


    /// <summary>
    /// Determine how many NPCs a cluster should have.
    /// Based on street point count and cluster size — deterministic from cluster data.
    /// </summary>
    public int ComputeNpcCount(ClusterDesc clusterDesc)
    {
        var streetPoints = CollectStreetPoints(clusterDesc);
        if (streetPoints.Count == 0) return 0;

        // Roughly 1 NPC per 2 street points, capped at 4096 (12-bit NPC index limit)
        int count = Math.Max(1, streetPoints.Count / 2);
        return Math.Min(count, 4095);
    }


    /// <summary>
    /// Generate a single NPC with fully independent seed.
    /// </summary>
    private NpcSchedule GenerateNpc(
        int clusterIndex,
        int npcIndex,
        string clusterSeed,
        ClusterDesc clusterDesc,
        List<Vector3> streetPoints)
    {
        // Independent seed per NPC — skipping other indices has no effect
        var rnd = new RandomSource(clusterSeed + "-npc-" + npcIndex);

        int npcId = NpcSchedule.MakeNpcId(clusterIndex, npcIndex);

        // Assign role
        string role = PickRole(rnd, clusterDesc);

        // Assign home position (pick a street point deterministically)
        int homeIdx = rnd.GetInt(streetPoints.Count - 1);
        Vector3 homePos = streetPoints[homeIdx];

        // Assign workplace position (different street point)
        int workIdx = rnd.GetInt(streetPoints.Count - 1);
        Vector3 workPos = streetPoints[workIdx];

        // Assign social venue positions (1-3 venues)
        int venueCount = 1 + rnd.GetInt(2);
        var socialVenueIds = new List<int>(venueCount);
        for (int v = 0; v < venueCount; v++)
        {
            socialVenueIds.Add(rnd.GetInt(streetPoints.Count - 1));
        }

        // Generate initial properties with per-NPC variation
        var properties = GenerateProperties(rnd, role);

        return new NpcSchedule
        {
            NpcId = npcId,
            Seed = npcId,
            Role = role,
            ClusterIndex = clusterIndex,
            NpcIndex = npcIndex,
            HomeLocationId = homeIdx,
            WorkplaceLocationId = workIdx,
            SocialVenueIds = socialVenueIds,
            HomePosition = homePos,
            WorkplacePosition = workPos,
            CurrentLocationId = homeIdx,
            Properties = properties,
            Trust = new Dictionary<int, float>(),
            HasPlayerDeviation = false,
        };
    }


    private string PickRole(RandomSource rnd, ClusterDesc clusterDesc)
    {
        // Adjust weights based on cluster character
        float downtown = clusterDesc.GetAttributeIntensity(clusterDesc.Pos, ClusterDesc.LocationAttributes.Downtown);

        float[] weights = new float[Roles.Length];
        Array.Copy(DefaultRoleWeights, weights, Roles.Length);

        // Downtown clusters: more merchants and authority, fewer drifters
        weights[1] += downtown * 0.10f; // merchant
        weights[4] += downtown * 0.05f; // authority
        weights[3] -= downtown * 0.10f; // drifter
        if (weights[3] < 0.02f) weights[3] = 0.02f;

        // Normalize
        float total = 0f;
        for (int i = 0; i < weights.Length; i++) total += weights[i];

        float roll = rnd.GetFloat() * total;
        float cumulative = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative) return Roles[i];
        }

        return Roles[0];
    }


    private Dictionary<string, float> GenerateProperties(RandomSource rnd, string role)
    {
        var props = new Dictionary<string, float>
        {
            { "hunger", 0f },
            { "health", 0.9f + rnd.GetFloat() * 0.1f },
            { "fatigue", rnd.GetFloat() * 0.2f },
            { "anger", rnd.GetFloat() * 0.1f },
            { "fear", 0f },
            { "trust", 0.4f + rnd.GetFloat() * 0.2f },
            { "happiness", 0.4f + rnd.GetFloat() * 0.3f },
            { "reputation", 0.4f + rnd.GetFloat() * 0.2f },
        };

        // Role-specific property variation
        switch (role)
        {
            case "worker":
                props["morality"] = 0.6f + rnd.GetFloat() * 0.2f;
                props["wealth"] = 0.3f + rnd.GetFloat() * 0.3f;
                break;
            case "merchant":
                props["morality"] = 0.5f + rnd.GetFloat() * 0.3f;
                props["wealth"] = 0.5f + rnd.GetFloat() * 0.3f;
                break;
            case "socialite":
                props["morality"] = 0.5f + rnd.GetFloat() * 0.3f;
                props["wealth"] = 0.4f + rnd.GetFloat() * 0.4f;
                break;
            case "drifter":
                props["morality"] = 0.3f + rnd.GetFloat() * 0.4f;
                props["wealth"] = 0.05f + rnd.GetFloat() * 0.2f;
                break;
            case "authority":
                props["morality"] = 0.6f + rnd.GetFloat() * 0.3f;
                props["wealth"] = 0.4f + rnd.GetFloat() * 0.2f;
                break;
            default:
                props["morality"] = 0.6f + rnd.GetFloat() * 0.2f;
                props["wealth"] = 0.3f + rnd.GetFloat() * 0.4f;
                break;
        }

        return props;
    }


    /// <summary>
    /// Collect all street point world positions from a cluster.
    /// Positions are in world space (cluster.Pos + local offset).
    /// </summary>
    private List<Vector3> CollectStreetPoints(ClusterDesc clusterDesc)
    {
        var strokeStore = clusterDesc.StrokeStore();
        if (strokeStore == null)
            return new List<Vector3>();

        var rawPoints = strokeStore.GetStreetPoints();
        var result = new List<Vector3>(rawPoints.Count);

        float groundHeight = clusterDesc.AverageHeight
            + MetaGen.ClusterStreetHeight
            + MetaGen.QuarterSidewalkOffset;

        foreach (var sp in rawPoints)
        {
            var worldPos = new Vector3(
                sp.Pos.X + clusterDesc.Pos.X,
                groundHeight,
                sp.Pos.Y + clusterDesc.Pos.Z);
            result.Add(worldPos);
        }

        return result;
    }
}
