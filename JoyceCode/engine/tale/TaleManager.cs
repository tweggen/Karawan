using System;
using System.Collections.Generic;
using System.Numerics;

namespace engine.tale;

/// <summary>
/// Runtime TALE manager. Maintains NpcSchedules and advances storylets
/// at game time. Provides API for spawn operators and strategy completion
/// callbacks.
/// </summary>
public class TaleManager
{
    private StoryletLibrary _library;
    private StoryletSelector _selector;
    private SpatialModel _spatialModel;
    private Dictionary<int, NpcSchedule> _schedules = new();
    private Random _rng;

    /// <summary>
    /// Reusable buffer for property deltas in ApplyPostconditions.
    /// </summary>
    private readonly Dictionary<string, float> _deltasBuffer = new();


    public void Initialize(StoryletLibrary library, SpatialModel spatialModel, int seed = 42)
    {
        _library = library;
        _selector = new StoryletSelector(library);
        _spatialModel = spatialModel;
        _rng = new Random(seed);
    }


    public void RegisterNpc(NpcSchedule schedule)
    {
        _schedules[schedule.NpcId] = schedule;
    }


    public NpcSchedule GetSchedule(int npcId)
    {
        return _schedules.GetValueOrDefault(npcId);
    }


    public IReadOnlyDictionary<int, NpcSchedule> AllSchedules => _schedules;


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
    /// Uses the spatial model to resolve location positions.
    /// </summary>
    public Vector3 GetWorldPosition(int npcId, DateTime gameNow)
    {
        if (!_schedules.TryGetValue(npcId, out var npc)) return Vector3.Zero;
        return npc.PositionAt(gameNow, _spatialModel);
    }


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
}
