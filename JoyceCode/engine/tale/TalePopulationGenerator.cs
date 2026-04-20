using System;
using System.Collections.Generic;
using System.Numerics;
using builtin.tools;
using engine.world;
using static engine.Logger;

namespace engine.tale;

/// <summary>
/// Deterministically generates NPC population for a cluster from its seed.
/// Each NPC's seed is independent (string-concat, not sequential RNG),
/// so skipping deviated indices does not affect other NPCs.
/// </summary>
public class TalePopulationGenerator
{
    private static readonly engine.Dc _dc = engine.Dc.TaleManager;

    private static readonly string[] Roles = { "worker", "merchant", "socialite", "drifter", "authority", "nightworker", "hustler", "reveler" };

    /// <summary>
    /// Role distribution weights. Downtown-heavy clusters shift toward merchant/authority/hustler.
    /// </summary>
    private static readonly float[] DefaultRoleWeights = { 0.30f, 0.13f, 0.15f, 0.12f, 0.10f, 0.08f, 0.07f, 0.05f };

    /// <summary>
    /// NPCs per street point. Legacy system used ~6 per street point per fragment;
    /// we use 3 per street point for the whole cluster since ~2/3 will be indoors.
    /// </summary>
    private const int NpcsPerStreetPoint = 3;


    /// <summary>
    /// Generate NPC schedules for a cluster, skipping deviated NPC indices.
    /// </summary>
    /// <param name="clusterDesc">The cluster to populate.</param>
    /// <param name="spatialModel">Spatial model for location assignment. If null, falls back to street points.</param>
    /// <param name="skipIndices">NPC indices to skip (deviated NPCs loaded from save).</param>
    /// <returns>List of generated NpcSchedule objects (excludes skipped indices).</returns>
    public List<NpcSchedule> Generate(ClusterDesc clusterDesc, SpatialModel spatialModel, HashSet<int> skipIndices = null)
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

            var schedule = GenerateNpc(clusterIndex, i, clusterSeed, clusterDesc, streetPoints, spatialModel);
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

        // Scale with street density, capped at 4096 (12-bit NPC index limit)
        int count = Math.Max(1, streetPoints.Count * NpcsPerStreetPoint);
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
        List<Vector3> streetPoints,
        SpatialModel spatialModel)
    {
        // Independent seed per NPC — skipping other indices has no effect
        var rnd = new RandomSource(clusterSeed + "-npc-" + npcIndex);

        int npcId = NpcSchedule.MakeNpcId(clusterIndex, npcIndex);

        // Assign role
        string role = PickRole(rnd, clusterDesc);

        // Assign location IDs from spatial model (if available)
        int homeLocId, workLocId;
        Vector3 homePos, workPos;

        if (spatialModel != null && spatialModel.Locations.Count > 0)
        {
            homeLocId = AssignLocationByRole(rnd, spatialModel, role, "home");
            workLocId = AssignLocationByRole(rnd, spatialModel, role, "workplace");
            var homeLoc = spatialModel.GetLocation(homeLocId);
            var workLoc = spatialModel.GetLocation(workLocId);
            homePos = homeLoc?.Position ?? Vector3.Zero;
            workPos = workLoc?.Position ?? Vector3.Zero;

            // If location lookup failed, fall back to street points
            if (homePos == Vector3.Zero && streetPoints.Count > 0)
            {
                Warning($"TALE GEN: Location {homeLocId} not found for NPC {npcId}, falling back to street point.");
                homeLocId = rnd.GetInt(streetPoints.Count - 1);
                homePos = streetPoints[homeLocId];
            }
            if (workPos == Vector3.Zero && streetPoints.Count > 0)
            {
                Warning($"TALE GEN: Location {workLocId} not found for NPC {npcId}, falling back to street point.");
                workLocId = rnd.GetInt(streetPoints.Count - 1);
                workPos = streetPoints[workLocId];
            }
        }
        else
        {
            // Fallback: use street points as before
            if (streetPoints.Count > 0)
            {
                homeLocId = rnd.GetInt(streetPoints.Count - 1);
                workLocId = rnd.GetInt(streetPoints.Count - 1);
                homePos = streetPoints[homeLocId];
                workPos = streetPoints[workLocId];
            }
            else
            {
                Warning($"TALE GEN: No spatial model or street points for cluster, NPCs may spawn at origin");
                homeLocId = 0;
                workLocId = 0;
                homePos = clusterDesc.Pos + new Vector3(0, 5f, 0);
                workPos = homePos;
            }
        }

        // Assign social venue IDs (1-3 venues)
        int venueCount = 1 + rnd.GetInt(2);
        var socialVenueIds = new List<int>(venueCount);
        if (spatialModel != null && spatialModel.Locations.Count > 0)
        {
            for (int v = 0; v < venueCount; v++)
            {
                int venueId = AssignLocationByRole(rnd, spatialModel, role, "social_venue");
                socialVenueIds.Add(venueId);
            }
        }
        else
        {
            for (int v = 0; v < venueCount; v++)
            {
                socialVenueIds.Add(rnd.GetInt(streetPoints.Count - 1));
            }
        }

        // Generate initial properties with per-NPC variation
        var properties = GenerateProperties(rnd, role);

        // Assign role-based routing preferences for multi-objective pathfinding
        var routingPreferences = GenerateRoutingPreferences(role, rnd);

        // Log location assignments for debugging
        var homeLocDebug = spatialModel?.GetLocation(homeLocId);
        var workLocDebug = spatialModel?.GetLocation(workLocId);
        Trace(_dc, $"TALE GEN NPC {npcId}: role={role} home={homeLocDebug?.Type}(id={homeLocId}) work={workLocDebug?.Type}(id={workLocId}) homePos=({homePos.X:F1},{homePos.Z:F1}) workPos=({workPos.X:F1},{workPos.Z:F1})");

        return new NpcSchedule
        {
            NpcId = npcId,
            Seed = npcId,
            Role = role,
            ClusterIndex = clusterIndex,
            NpcIndex = npcIndex,
            HomeLocationId = homeLocId,
            WorkplaceLocationId = workLocId,
            SocialVenueIds = socialVenueIds,
            HomePosition = homePos,
            WorkplacePosition = workPos,
            CurrentLocationId = homeLocId,
            Properties = properties,
            CurrentWorldPosition = homePos,
            Trust = new Dictionary<int, float>(),
            HasPlayerDeviation = false,
            RoutingPreferences = routingPreferences,
        };
    }


    /// <summary>
    /// Assign a location ID to an NPC based on role and location type preference.
    /// Falls back to any location if type bucket is empty.
    /// </summary>
    private int AssignLocationByRole(RandomSource rnd, SpatialModel spatialModel, string role, string preferredType)
    {
        var candidates = new List<int>();

        // Role-based location preference filtering
        foreach (var loc in spatialModel.Locations)
        {
            bool matches = false;

            switch (role)
            {
                case "merchant":
                    matches = (preferredType == "shop" && loc.Type == "shop") ||
                              (preferredType == "social_venue" && loc.Type == "social_venue") ||
                              (preferredType == "workplace" && loc.Type == "shop") ||
                              (preferredType == "home" && (loc.Type == "home" || loc.Type == "shop"));
                    break;

                case "worker":
                case "authority":
                    matches = (preferredType == "workplace" && (loc.Type == "office" || loc.Type == "warehouse")) ||
                              (preferredType == "home" && loc.Type == "home") ||
                              (preferredType == "social_venue" && loc.Type == "social_venue");
                    break;

                case "socialite":
                    matches = (preferredType == "social_venue" && loc.Type == "social_venue") ||
                              (preferredType == "home" && (loc.Type == "home" || loc.Type == "street_segment")) ||
                              (preferredType == "workplace" && loc.Type == "street_segment");
                    break;

                case "drifter":
                    matches = (preferredType == "home" && (loc.Type == "home" || loc.Type == "street_segment")) ||
                              (preferredType == "workplace" && loc.Type == "street_segment") ||
                              (preferredType == "social_venue" && (loc.Type == "social_venue" || loc.Type == "street_segment"));
                    break;

                case "nightworker":
                    matches = (preferredType == "workplace" && (loc.Type == "office" || loc.Type == "warehouse")) ||
                              (preferredType == "home" && loc.Type == "home") ||
                              (preferredType == "social_venue" && loc.Type == "social_venue");
                    break;

                case "hustler":
                    matches = (preferredType == "home" && loc.Type == "home") ||
                              (preferredType == "workplace" && loc.Type == "street_segment") ||
                              (preferredType == "social_venue" && (loc.Type == "social_venue" || loc.Type == "street_segment"));
                    break;

                case "reveler":
                    matches = (preferredType == "home" && loc.Type == "home") ||
                              (preferredType == "workplace" && loc.Type == "social_venue") ||
                              (preferredType == "social_venue" && loc.Type == "social_venue");
                    break;

                default:
                    matches = loc.Type == preferredType;
                    break;
            }

            if (matches)
                candidates.Add(loc.Id);
        }

        // If no candidates for preferred type, fall back to any location
        if (candidates.Count == 0)
        {
            foreach (var loc in spatialModel.Locations)
                candidates.Add(loc.Id);
        }

        // Pick random candidate
        if (candidates.Count == 0)
            return 0;

        int idx = rnd.GetInt(candidates.Count - 1);
        return candidates[idx];
    }


    private string PickRole(RandomSource rnd, ClusterDesc clusterDesc)
    {
        // Adjust weights based on cluster character
        float downtown = clusterDesc.GetAttributeIntensity(clusterDesc.Pos, ClusterDesc.LocationAttributes.Downtown);

        float[] weights = new float[Roles.Length];
        Array.Copy(DefaultRoleWeights, weights, Roles.Length);

        // Downtown clusters: more merchants, authority, hustlers, revelers; fewer drifters
        weights[1] += downtown * 0.10f; // merchant
        weights[4] += downtown * 0.05f; // authority
        weights[6] += downtown * 0.05f; // hustler
        weights[7] += downtown * 0.03f; // reveler
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
            case "nightworker":
                props["morality"] = 0.5f + rnd.GetFloat() * 0.3f;
                props["wealth"] = 0.2f + rnd.GetFloat() * 0.3f;
                break;
            case "hustler":
                props["morality"] = 0.2f + rnd.GetFloat() * 0.4f;
                props["wealth"] = 0.2f + rnd.GetFloat() * 0.4f;
                break;
            case "reveler":
                props["morality"] = 0.4f + rnd.GetFloat() * 0.3f;
                props["wealth"] = 0.3f + rnd.GetFloat() * 0.4f;
                break;
            default:
                props["morality"] = 0.6f + rnd.GetFloat() * 0.2f;
                props["wealth"] = 0.3f + rnd.GetFloat() * 0.4f;
                break;
        }

        return props;
    }


    /// <summary>
    /// Assign role-based routing preferences for multi-objective pathfinding.
    /// Different roles favor different routing goals and behaviors.
    /// </summary>
    private RoutingPreferences GenerateRoutingPreferences(string role, RandomSource rnd)
    {
        var prefs = new RoutingPreferences();

        switch (role)
        {
            // Time-conscious roles: prioritize on-time arrival (deadlines matter)
            case "worker":
            case "authority":
            case "nightworker":
                prefs.Goal = NpcGoal.OnTime;
                prefs.SceneryWeight = 0.1f; // Minor preference for scenery
                prefs.SafetyWeight = 0.2f;  // Moderate safety concern
                break;

            // Commerce-driven roles: prioritize speed (fast routes = more time for business)
            case "merchant":
            case "hustler":
                prefs.Goal = NpcGoal.Fast;
                prefs.SceneryWeight = 0.0f; // No time for sightseeing
                prefs.SafetyWeight = 0.1f;  // Low safety priority (commercial districts)
                break;

            // Leisure-focused roles: prefer scenic routes (enjoy the journey)
            case "socialite":
            case "reveler":
                prefs.Goal = NpcGoal.Scenic;
                prefs.SceneryWeight = 0.8f; // High scenery preference
                prefs.SafetyWeight = 0.3f;  // Moderate safety (public areas)
                break;

            // Cautious role: avoid dangerous areas
            case "drifter":
                prefs.Goal = NpcGoal.Safe;
                prefs.SceneryWeight = 0.4f; // Moderate scenery (avoiding certain areas)
                prefs.SafetyWeight = 0.9f;  // Very high safety priority
                break;

            default:
                prefs.Goal = NpcGoal.Fast;
                break;
        }

        return prefs;
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
