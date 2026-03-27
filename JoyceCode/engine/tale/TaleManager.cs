using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using engine.joyce;
using engine.world;
using static engine.Logger;

namespace engine.tale;

/// <summary>
/// Runtime TALE manager. Cluster-aware registry of NpcSchedules.
/// Manages Tier 3 background population with seed-based regeneration
/// and deviation-only persistence.
/// </summary>
public class TaleManager
{
    private StoryletLibrary _library;
    private StoryletSelector _selector;
    private Dictionary<int, SpatialModel> _spatialModels = new();
    private Random _rng;

    /// <summary>
    /// All schedules by NpcId. Includes both generated (primary) and deviated NPCs.
    /// </summary>
    private Dictionary<int, NpcSchedule> _schedules = new();

    /// <summary>
    /// Track which clusters are currently populated.
    /// </summary>
    private HashSet<int> _populatedClusters = new();

    /// <summary>
    /// Deviation skip masks per cluster: cluster index → set of NPC indices to skip on regeneration.
    /// </summary>
    private Dictionary<int, HashSet<int>> _deviationSkipMasks = new();

    /// <summary>
    /// NPC IDs that are currently materialized as Tier 2/1 ECS entities.
    /// </summary>
    private HashSet<int> _materializedNpcIds = new();

    /// <summary>
    /// NPC IDs that are currently in Tier 2 (noticed but dematerialized, map icon visible).
    /// These remain in _materializedNpcIds; strategy is frozen.
    /// </summary>
    private HashSet<int> _tier2NpcIds = new();

    /// <summary>
    /// Reusable buffer for property deltas in ApplyPostconditions.
    /// </summary>
    private readonly Dictionary<string, float> _deltasBuffer = new();

    private readonly TalePopulationGenerator _generator = new();


    public void Initialize(StoryletLibrary library, int seed = 42)
    {
        _library = library;
        _selector = new StoryletSelector(library);
        _rng = new Random(seed);
    }


    #region Cluster Lifecycle

    /// <summary>
    /// Populate a cluster: generate NPC schedules from seed, skipping deviated indices.
    /// Called on ClusterCompletedEvent. SpatialModel is used for location assignment.
    /// </summary>
    public void PopulateCluster(ClusterDesc clusterDesc, SpatialModel spatialModel)
    {
        int clusterIndex = clusterDesc.Index;

        if (_populatedClusters.Contains(clusterIndex))
        {
            Trace($"TALE MGR: Cluster {clusterIndex} already populated, skipping.");
            return;
        }

        if (spatialModel != null)
        {
            _spatialModels[clusterIndex] = spatialModel;
            if (spatialModel.Locations.Count == 0)
            {
                Warning($"TALE MGR: Cluster {clusterIndex} spatial model has ZERO locations! " +
                        $"({spatialModel.BuildingCount} buildings, {spatialModel.ShopCount} shops, " +
                        $"{spatialModel.StreetPointCount} street points). NPCs may spawn at Y=0.");
            }
            else
            {
                Trace($"TALE MGR: Cluster {clusterIndex} spatial model: " +
                      $"{spatialModel.Locations.Count} locations, {spatialModel.BuildingCount} buildings, " +
                      $"{spatialModel.ShopCount} shops, {spatialModel.StreetPointCount} street points.");
            }
        }
        else
        {
            Warning($"TALE MGR: Cluster {clusterIndex} has NO spatial model!");
        }

        _deviationSkipMasks.TryGetValue(clusterIndex, out var skipMask);
        var schedules = _generator.Generate(clusterDesc, spatialModel, skipMask);

        foreach (var schedule in schedules)
        {
            _schedules[schedule.NpcId] = schedule;
        }

        // Advance all newly generated schedules through a full day cycle before game start.
        // This ensures NPCs have traversed all conditions and edge cases in their storylet loops.
        // Advance from day 1 midnight to day 2 at 22:46 (24+ hours total).
        // Add per-NPC time offsets (±45 minutes) so NPCs are desynchronized — some mid-travel,
        // some mid-activity, avoiding all stationary at game start.
        DateTime baseTime = new DateTime(2000, 1, 1, 0, 0, 0); // day 1, midnight
        DateTime gameStartTime = baseTime.AddDays(1).AddHours(22).AddMinutes(46); // day 2, 22:46

        foreach (var schedule in schedules)
        {
            // Deterministic per-NPC offset: seed RNG from NpcId
            var offsetRng = new builtin.tools.RandomSource(schedule.NpcId.ToString());
            int offsetMinutes = offsetRng.GetInt(91) - 45; // ±45 minutes: 0-90 becomes -45 to 45
            DateTime npcTime = gameStartTime.AddMinutes(offsetMinutes);

            AdvanceNpc(schedule.NpcId, npcTime);
            Trace($"TALE MGR: Advanced NPC {schedule.NpcId} through full cycle (24+ hours) to {npcTime:HH:mm} " +
                  $"(offset {offsetMinutes:+0;-0} min), now at location {schedule.CurrentLocationId}");
        }

        // Final advance to actual game start time so all transit phases align with spawn time
        foreach (var schedule in schedules)
        {
            AdvanceNpc(schedule.NpcId, gameStartTime);
        }

        // Diagnostic: show transit distribution and fragment distribution of generated NPCs
        var inTransit = new List<NpcSchedule>();
        var fragmentCounts = new Dictionary<string, int>();
        foreach (var schedule in schedules)
        {
            Vector3 pos = schedule.CurrentWorldPosition != Vector3.Zero
                ? schedule.CurrentWorldPosition : schedule.HomePosition;
            var frag = Fragment.PosToIndex3(pos);
            string key = $"({frag.I},{frag.K})";
            fragmentCounts[key] = fragmentCounts.GetValueOrDefault(key) + 1;

            if (schedule.IsInTransit)
                inTransit.Add(schedule);
        }
        string distrib = string.Join(", ", fragmentCounts.Select(kv => $"{kv.Key}={kv.Value}"));
        Trace($"TALE MGR: Populated cluster {clusterIndex} with {schedules.Count} NPCs. " +
              $"Total schedules now: {_schedules.Count}. Fragment distribution: {distrib}");
        Trace($"TALE MGR: {inTransit.Count}/{schedules.Count} NPCs in transit at spawn time ({gameStartTime:HH:mm})");

        // Show NPCs in transit for debugging
        if (inTransit.Count > 0)
        {
            Trace($"TALE MGR: NPCs in transit:");
            for (int i = 0; i < Math.Min(10, inTransit.Count); i++)
            {
                var s = inTransit[i];
                var fromLoc = spatialModel?.GetLocation(s.TransitFromLocationId);
                var toLoc = spatialModel?.GetLocation(s.TransitToLocationId);
                string fromName = fromLoc?.Type ?? "unknown";
                string toName = toLoc?.Type ?? "unknown";
                float travelMinutes = (float)(s.TransitEnd - s.TransitStart).TotalMinutes;
                Trace($"TALE MGR:   NPC {s.NpcId} ({s.Role}): {fromName} → {toName} " +
                      $"({travelMinutes:F1} min, {s.TransitStart:HH:mm}-{s.TransitEnd:HH:mm})");
            }
        }

        // Show first few NPCs for debugging
        for (int i = 0; i < Math.Min(3, schedules.Count); i++)
        {
            var s = schedules[i];
            Trace($"TALE MGR:   NPC[{i}] id={s.NpcId} role={s.Role} home={s.HomePosition} " +
                  $"worldPos={s.CurrentWorldPosition} homeLoc={s.HomeLocationId} inTransit={s.IsInTransit}");
        }

        _populatedClusters.Add(clusterIndex);
    }


    /// <summary>
    /// Depopulate a cluster: remove non-deviated schedules (they can be regenerated).
    /// Deviated NPCs remain in _schedules and their indices stay in the skip mask.
    /// Also removes the spatial model for this cluster.
    /// </summary>
    public void DepopulateCluster(int clusterIndex)
    {
        if (!_populatedClusters.Contains(clusterIndex))
            return;

        var toRemove = new List<int>();
        foreach (var kvp in _schedules)
        {
            if (kvp.Value.ClusterIndex == clusterIndex && !kvp.Value.HasPlayerDeviation)
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var npcId in toRemove)
        {
            _schedules.Remove(npcId);
            _materializedNpcIds.Remove(npcId);
        }

        _populatedClusters.Remove(clusterIndex);
        _spatialModels.Remove(clusterIndex);
    }


    /// <summary>
    /// Check if a cluster is currently populated.
    /// </summary>
    public bool IsClusterPopulated(int clusterIndex) => _populatedClusters.Contains(clusterIndex);

    #endregion


    #region NPC Registration & Query

    /// <summary>
    /// Register a single NPC schedule (used for deviated NPCs loaded from save).
    /// </summary>
    public void RegisterNpc(NpcSchedule schedule)
    {
        _schedules[schedule.NpcId] = schedule;

        if (schedule.HasPlayerDeviation)
        {
            if (!_deviationSkipMasks.TryGetValue(schedule.ClusterIndex, out var mask))
            {
                mask = new HashSet<int>();
                _deviationSkipMasks[schedule.ClusterIndex] = mask;
            }
            mask.Add(schedule.NpcIndex);
        }
    }


    public NpcSchedule GetSchedule(int npcId)
    {
        return _schedules.GetValueOrDefault(npcId);
    }


    public IReadOnlyDictionary<int, NpcSchedule> AllSchedules => _schedules;


    /// <summary>
    /// Get all NPC schedules whose current position falls in the given fragment.
    /// Used by TaleSpawnOperator to decide which NPCs to materialize.
    /// Uses CurrentWorldPosition if set (NPC has deviated), otherwise falls back to HomePosition.
    /// </summary>
    public List<NpcSchedule> GetNpcsInFragment(Index3 idxFragment)
    {
        var result = new List<NpcSchedule>();
        foreach (var kvp in _schedules)
        {
            var npc = kvp.Value;
            // Use current world position if set, otherwise use home position
            Vector3 checkPos = npc.CurrentWorldPosition != Vector3.Zero ? npc.CurrentWorldPosition : npc.HomePosition;
            var npcFragment = Fragment.PosToIndex3(checkPos);
            if (npcFragment.I == idxFragment.I && npcFragment.K == idxFragment.K)
            {
                result.Add(npc);
            }
        }
        return result;
    }


    /// <summary>
    /// Get all deviated NPCs for a given cluster (for persistence).
    /// </summary>
    public List<NpcSchedule> GetDeviatedNpcs(int clusterIndex)
    {
        var result = new List<NpcSchedule>();
        foreach (var kvp in _schedules)
        {
            if (kvp.Value.ClusterIndex == clusterIndex && kvp.Value.HasPlayerDeviation)
            {
                result.Add(kvp.Value);
            }
        }
        return result;
    }


    /// <summary>
    /// Get all deviated NPCs across all clusters (for save).
    /// </summary>
    public List<NpcSchedule> GetAllDeviatedNpcs()
    {
        var result = new List<NpcSchedule>();
        foreach (var kvp in _schedules)
        {
            if (kvp.Value.HasPlayerDeviation)
            {
                result.Add(kvp.Value);
            }
        }
        return result;
    }


    /// <summary>
    /// Get the deviation skip mask for a cluster.
    /// </summary>
    public HashSet<int> GetDeviationSkipMask(int clusterIndex)
    {
        return _deviationSkipMasks.GetValueOrDefault(clusterIndex);
    }

    #endregion


    #region Materialization Tracking

    public bool IsMaterialized(int npcId) => _materializedNpcIds.Contains(npcId);

    public void SetMaterialized(int npcId) => _materializedNpcIds.Add(npcId);

    public void SetDematerialized(int npcId) => _materializedNpcIds.Remove(npcId);


    /// <summary>
    /// Check if an NPC is currently in Tier 2 (noticed but with frozen strategy).
    /// </summary>
    public bool IsTier2(int npcId) => _tier2NpcIds.Contains(npcId);

    /// <summary>
    /// Demote an NPC to Tier 2: entity stays alive but strategy is frozen.
    /// </summary>
    public void SetTier2(int npcId) => _tier2NpcIds.Add(npcId);

    /// <summary>
    /// Clear Tier 2 flag when promoting back to Tier 1 or destroying.
    /// </summary>
    public void ClearTier2(int npcId) => _tier2NpcIds.Remove(npcId);

    #endregion


    #region Storylet Advancement

    /// <summary>
    /// Advance an NPC to its next storylet. Called when the entity strategy
    /// completes its current verb sequence, or when an NPC is first spawned
    /// and needs its current activity.
    ///
    /// Time randomization (±5 minutes per NPC per step) ensures workers don't all
    /// arrive/depart at exact scheduled times — more realistic scheduling variation.
    /// </summary>
    public StoryletDefinition AdvanceNpc(int npcId, DateTime gameNow)
    {
        if (!_schedules.TryGetValue(npcId, out var npc)) return null;

        // Apply postconditions from the completed storylet
        string previousStorylet = npc.CurrentStorylet;
        if (previousStorylet != null)
        {
            float previousDuration = (float)(npc.CurrentEnd - npc.CurrentStart).TotalMinutes;
            var prevDef = _library.GetById(previousStorylet);
            if (prevDef != null)
                _selector.ApplyPostconditions(npc, prevDef, previousDuration, _deltasBuffer);
            else
                _selector.ApplyPostconditions(npc, previousStorylet, previousDuration, _deltasBuffer);
        }

        // Select next storylet
        var next = _selector.SelectNext(npc, gameNow);
        npc.ScheduleStep++;

        // Resolve destination
        int destination = ResolveLocation(npc, next);

        // Compute travel time from spatial model
        float travelMinutes = 0f;
        if (_spatialModels.TryGetValue(npc.ClusterIndex, out var spatialModel) && spatialModel != null)
        {
            travelMinutes = spatialModel.GetTravelTime(npc.CurrentLocationId, destination);
        }

        // Determine duration
        float durationMinutes = next.GetDuration(_rng);

        // Time randomization: ±5 minutes per NPC per schedule step
        // E.g., worker scheduled to arrive at 8:00 might arrive at 7:55-8:05
        var timeRng = new builtin.tools.RandomSource(npc.NpcId.ToString() + "-time-" + npc.ScheduleStep);
        int timeOffsetMinutes = timeRng.GetInt(11) - 5; // ±5 minutes: 0-10 becomes -5 to 5
        DateTime actualStart = gameNow + TimeSpan.FromMinutes(travelMinutes + timeOffsetMinutes);

        // Populate transit phase if travel needed
        if (travelMinutes > 0)
        {
            npc.IsInTransit = true;
            npc.TransitFromLocationId = npc.CurrentLocationId; // capture BEFORE overwrite
            npc.TransitToLocationId = destination;
            npc.TransitStart = gameNow;
            npc.TransitEnd = gameNow + TimeSpan.FromMinutes(travelMinutes);
        }
        else
        {
            npc.IsInTransit = false;
        }

        // Update NPC state
        npc.CurrentStorylet = next.Id;
        npc.CurrentLocationId = destination;
        npc.CurrentStart = actualStart;
        npc.CurrentEnd = actualStart + TimeSpan.FromMinutes(durationMinutes);

        // Update current world position from location
        if (_spatialModels.TryGetValue(npc.ClusterIndex, out spatialModel) && spatialModel != null)
        {
            var loc = spatialModel.GetLocation(destination);
            if (loc != null)
                npc.CurrentWorldPosition = loc.Position;
        }

        return next;
    }

    
    /// <summary>
    /// Get the current storylet definition for an NPC without advancing.
    /// </summary>
    public StoryletDefinition GetCurrentStorylet(int npcId)
    {
        if (!_schedules.TryGetValue(npcId, out var npc)) return null;
        if (npc.CurrentStorylet == null) return null;
        return _library.GetById(npc.CurrentStorylet);
    }


    /// <summary>
    /// Get the world position for an NPC at a given game time.
    /// </summary>
    public Vector3 GetWorldPosition(int npcId, DateTime gameNow)
    {
        if (!_schedules.TryGetValue(npcId, out var npc)) return Vector3.Zero;
        var spatialModel = _spatialModels.TryGetValue(npc.ClusterIndex, out var model) ? model : null;
        return npc.PositionAt(gameNow, spatialModel);
    }


    /// <summary>
    /// Get the spatial model for a cluster.
    /// </summary>
    public SpatialModel GetSpatialModel(int clusterIndex)
    {
        return _spatialModels.TryGetValue(clusterIndex, out var model) ? model : null;
    }

    #endregion


    #region Location Resolution

    private int ResolveLocation(NpcSchedule npc, StoryletDefinition storylet)
    {
        int resolved = storylet.ResolveLocationType() switch
        {
            StoryletLocationType.Home => npc.HomeLocationId,
            StoryletLocationType.Workplace => npc.WorkplaceLocationId,
            StoryletLocationType.SocialVenue => ResolveSocialVenue(npc),
            StoryletLocationType.EatVenue => ResolveEatVenue(npc),
            StoryletLocationType.Street => ResolveStreet(npc),
            _ => npc.CurrentLocationId
        };

        if (resolved < 0)
        {
            resolved = npc.CurrentLocationId;
        }
        return resolved;
    }


    private int ResolveSocialVenue(NpcSchedule npc)
    {
        if (npc.SocialVenueIds == null || npc.SocialVenueIds.Count == 0)
            return npc.HomeLocationId;
        int idx = npc.ScheduleStep % npc.SocialVenueIds.Count;
        return npc.SocialVenueIds[idx];
    }


    private int ResolveEatVenue(NpcSchedule npc)
    {
        var spatialModel = _spatialModels.TryGetValue(npc.ClusterIndex, out var model) ? model : null;
        if (spatialModel != null)
        {
            int eatId = spatialModel.FindNearestOfType(npc.CurrentLocationId, "social_venue", "Eat");
            if (eatId >= 0) return eatId;
        }
        if (npc.SocialVenueIds != null && npc.SocialVenueIds.Count > 0)
            return npc.SocialVenueIds[0];
        return npc.HomeLocationId;
    }


    private int ResolveStreet(NpcSchedule npc)
    {
        var spatialModel = _spatialModels.TryGetValue(npc.ClusterIndex, out var model) ? model : null;
        if (spatialModel != null)
        {
            // CRITICAL: Only pick street locations from the SAME cluster as the NPC
            // Street points are cluster-relative, so mixing clusters causes pathfinding to fail
            // (NavCluster.TryCreateCursor checks AABB containment for both start and end positions)
            var streetLocations = spatialModel.Locations.FindAll(l => l.Type == "street_segment");
            if (streetLocations.Count > 0)
            {
                int randomIdx = _rng.Next(streetLocations.Count);
                return streetLocations[randomIdx].Id;
            }
        }
        return npc.CurrentLocationId;
    }

    #endregion
}
