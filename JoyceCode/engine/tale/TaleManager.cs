using System;
using System.Collections.Generic;
using System.Numerics;
using engine.joyce;
using engine.world;

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
    private SpatialModel _spatialModel;
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
    /// Reusable buffer for property deltas in ApplyPostconditions.
    /// </summary>
    private readonly Dictionary<string, float> _deltasBuffer = new();

    private readonly TalePopulationGenerator _generator = new();


    public void Initialize(StoryletLibrary library, SpatialModel spatialModel, int seed = 42)
    {
        _library = library;
        _selector = new StoryletSelector(library);
        _spatialModel = spatialModel;
        _rng = new Random(seed);
    }


    #region Cluster Lifecycle

    /// <summary>
    /// Populate a cluster: generate NPC schedules from seed, skipping deviated indices.
    /// Called on ClusterCompletedEvent.
    /// </summary>
    public void PopulateCluster(ClusterDesc clusterDesc)
    {
        int clusterIndex = clusterDesc.Index;

        if (_populatedClusters.Contains(clusterIndex))
            return;

        _deviationSkipMasks.TryGetValue(clusterIndex, out var skipMask);
        var schedules = _generator.Generate(clusterDesc, skipMask);

        foreach (var schedule in schedules)
        {
            _schedules[schedule.NpcId] = schedule;
        }

        _populatedClusters.Add(clusterIndex);
    }


    /// <summary>
    /// Depopulate a cluster: remove non-deviated schedules (they can be regenerated).
    /// Deviated NPCs remain in _schedules and their indices stay in the skip mask.
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
    /// Get all NPC schedules whose home position falls in the given fragment.
    /// Used by TaleSpawnOperator to decide which NPCs to materialize.
    /// </summary>
    public List<NpcSchedule> GetNpcsInFragment(Index3 idxFragment)
    {
        var result = new List<NpcSchedule>();
        foreach (var kvp in _schedules)
        {
            var npc = kvp.Value;
            var npcFragment = Fragment.PosToIndex3(npc.HomePosition);
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

    #endregion


    #region Storylet Advancement

    /// <summary>
    /// Advance an NPC to its next storylet. Called when the entity strategy
    /// completes its current verb sequence, or when an NPC is first spawned
    /// and needs its current activity.
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
        float travelMinutes = _spatialModel != null
            ? _spatialModel.GetTravelTime(npc.CurrentLocationId, destination)
            : 0f;

        // Determine duration
        float durationMinutes = next.GetDuration(_rng);

        // Update NPC state
        npc.CurrentStorylet = next.Id;
        npc.CurrentLocationId = destination;
        npc.CurrentStart = gameNow + TimeSpan.FromMinutes(travelMinutes);
        npc.CurrentEnd = npc.CurrentStart + TimeSpan.FromMinutes(durationMinutes);

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
        return npc.PositionAt(gameNow, _spatialModel);
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

        if (resolved < 0) resolved = npc.CurrentLocationId;
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
        if (_spatialModel != null)
        {
            int eatId = _spatialModel.FindNearestOfType(npc.CurrentLocationId, "social_venue", "Eat");
            if (eatId >= 0) return eatId;
        }
        if (npc.SocialVenueIds != null && npc.SocialVenueIds.Count > 0)
            return npc.SocialVenueIds[0];
        return npc.HomeLocationId;
    }


    private int ResolveStreet(NpcSchedule npc)
    {
        if (_spatialModel != null)
        {
            int streetId = _spatialModel.FindNearestOfType(npc.CurrentLocationId, "street_segment");
            if (streetId >= 0) return streetId;
        }
        return npc.CurrentLocationId;
    }

    #endregion
}
