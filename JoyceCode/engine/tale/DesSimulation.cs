using System;
using System.Collections.Generic;
using System.Linq;

namespace engine.tale;

/// <summary>
/// Tier 3 Discrete Event Simulation engine.
/// NPCs advance from storylet to storylet by jumping between events.
/// No per-frame ticking. Production code used for background NPCs.
/// </summary>
public class DesSimulation
{
    private EventQueue _queue;
    private Dictionary<int, NpcSchedule> _npcs;
    private EncounterResolver _encounters;
    private SpatialModel _spatial;
    private StoryletSelector _storylets;
    private IEventLogger _logger;
    private RelationshipTracker _relationships;
    private MetricsCollector _metrics;
    private DateTime _clock;
    private DateTime _startTime;
    private Random _rng;

    // Reusable buffer for property deltas
    private readonly Dictionary<string, float> _deltasBuffer = new();

    public int EventsProcessed { get; private set; }
    public EncounterResolver Encounters => _encounters;
    public RelationshipTracker Relationships => _relationships;
    public MetricsCollector Metrics => _metrics;
    public IReadOnlyDictionary<int, NpcSchedule> Npcs => _npcs;

    // Trace collection for sampled NPCs
    public record struct TraceEntry(
        int NpcId, string Storylet, DateTime Start, DateTime End, int LocationId,
        string LocationType, float TravelMinutes,
        Dictionary<string, float> PropsSnapshot, Dictionary<string, float> Deltas);

    public record struct TraceEncounter(
        int NpcId, int OtherId, string OtherRole, string InteractionType,
        DateTime Time, float TrustBefore, float TrustAfter);

    private List<TraceEntry> _traces;
    private List<TraceEncounter> _traceEncounters;
    private HashSet<int> _tracedNpcs;

    public IReadOnlyList<TraceEntry> Traces => _traces;
    public IReadOnlyList<TraceEncounter> TraceEncounters => _traceEncounters;


    public void SetTracedNpcs(IEnumerable<int> npcIds)
    {
        _tracedNpcs = new HashSet<int>(npcIds);
        _traces = new List<TraceEntry>();
        _traceEncounters = new List<TraceEncounter>();
    }


    public void Initialize(SpatialModel model, List<NpcSchedule> npcs,
        IEventLogger logger, DateTime startTime, int seed = 42)
    {
        _spatial = model;
        _logger = logger;
        _startTime = startTime;
        _clock = startTime;
        _rng = new Random(seed);
        _queue = new EventQueue();
        _encounters = new EncounterResolver();
        _storylets = new StoryletSelector();
        _relationships = new RelationshipTracker();
        _metrics = new MetricsCollector();

        _npcs = new Dictionary<int, NpcSchedule>(npcs.Count);
        foreach (var npc in npcs)
        {
            _npcs[npc.NpcId] = npc;
            _metrics.RegisterNpc(npc.Role);
            _metrics.InitializePropertyRanges(npc.NpcId, npc.Properties);

            // Log NPC creation
            _logger.LogNpcCreated(npc.NpcId, npc.Seed, npc.Role,
                npc.HomeLocationId, npc.WorkplaceLocationId,
                npc.SocialVenueIds, npc.Properties, startTime);

            SeedNpc(npc, startTime);
        }
    }


    public void RunUntil(DateTime endTime)
    {
        DateTime lastCleanup = _clock;
        DateTime nextDayBoundary = _startTime.Date.AddDays(1);

        while (!_queue.IsEmpty && _queue.NextTime <= endTime)
        {
            var evt = _queue.Pop();
            _clock = evt.GameTime;

            // Day boundary processing
            while (_clock >= nextDayBoundary && nextDayBoundary <= endTime)
            {
                int completedDay = (int)(nextDayBoundary - _startTime.Date).TotalDays;
                EmitDaySummaries(completedDay);
                _metrics.OnDayEnd();
                _encounters.ClearDailyDedup();

                // Re-initialize property ranges for the new day
                foreach (var npc in _npcs.Values)
                    _metrics.InitializePropertyRanges(npc.NpcId, npc.Properties);

                nextDayBoundary = nextDayBoundary.AddDays(1);
            }

            switch (evt.Type)
            {
                case SimEventType.NodeArrival:
                    ProcessNodeArrival(evt);
                    break;
                case SimEventType.EncounterCheck:
                    break;
                case SimEventType.InterruptResolution:
                    break;
            }

            EventsProcessed++;

            // Periodic cleanup of old presence data
            if ((_clock - lastCleanup).TotalHours >= 24)
            {
                _encounters.ClearBefore(_clock - TimeSpan.FromHours(24));
                lastCleanup = _clock;
            }
        }

        // Final day summary
        int finalDay = (int)(_clock.Date - _startTime.Date).TotalDays + 1;
        EmitDaySummaries(finalDay);
        _metrics.OnDayEnd();
        _logger.Flush();
    }


    private int DayOf(DateTime t) => (int)(t.Date - _startTime.Date).TotalDays + 1;


    private void SeedNpc(NpcSchedule npc, DateTime startTime)
    {
        npc.CurrentLocationId = npc.HomeLocationId >= 0 ? npc.HomeLocationId : 0;
        npc.CurrentStorylet = "sleep";
        npc.CurrentStart = startTime - TimeSpan.FromHours(8);

        float baseWakeHour = npc.Role switch
        {
            "Worker" => 6f,
            "Merchant" => 7f,
            "Socialite" => 9f,
            "Drifter" => 5f,
            _ => 6f
        };
        float jitterMinutes = (npc.Seed % 60) - 30;
        npc.CurrentEnd = startTime.AddHours(baseWakeHour).AddMinutes(jitterMinutes);
        npc.ScheduleStep = 0;

        _queue.Push(new SimEvent
        {
            GameTime = npc.CurrentEnd,
            NpcId = npc.NpcId,
            Type = SimEventType.NodeArrival
        });

        _encounters.RegisterPresence(npc.NpcId, npc.CurrentLocationId, npc.CurrentStart, npc.CurrentEnd);
    }


    private void ProcessNodeArrival(SimEvent evt)
    {
        if (!_npcs.TryGetValue(evt.NpcId, out var npc)) return;

        string previousStorylet = npc.CurrentStorylet;
        float previousDuration = (float)(npc.CurrentEnd - npc.CurrentStart).TotalMinutes;
        int day = DayOf(_clock);

        // 1. Apply postconditions and collect deltas
        var deltas = _storylets.ApplyPostconditions(npc, previousStorylet, previousDuration, _deltasBuffer);

        // Track property changes for metrics
        foreach (var kvp in npc.Properties)
            _metrics.OnPropertyChanged(npc.NpcId, kvp.Key, kvp.Value);

        // 2. Select next storylet
        var next = _storylets.SelectNext(npc);
        _storylets.AdvanceStep(npc);

        // 3. Resolve destination location
        int destination = ResolveLocation(npc, next);

        // 4. Compute travel time
        float travelMinutes = _spatial.GetTravelTime(npc.CurrentLocationId, destination);

        // 5. Update NPC state
        int previousLocation = npc.CurrentLocationId;
        npc.CurrentStorylet = next.Id;
        npc.CurrentLocationId = destination;
        npc.CurrentStart = _clock + TimeSpan.FromMinutes(travelMinutes);
        npc.CurrentEnd = npc.CurrentStart + TimeSpan.FromMinutes(next.DurationMinutes);

        // 6. Register presence for encounter detection
        _encounters.RegisterPresence(npc.NpcId, destination, npc.CurrentStart, npc.CurrentEnd);

        // 7. Check for encounters at this location
        var loc = _spatial.GetLocation(destination);
        string locType = loc?.Type ?? "street_segment";
        var encounterResults = _encounters.CheckEncounters(
            npc.NpcId, destination, npc.CurrentStart, npc.CurrentEnd, locType, _rng);

        // 8. Process encounters: determine type, update trust, log
        foreach (var (otherId, encounterTime) in encounterResults)
        {
            ProcessEncounter(npc.NpcId, otherId, destination, locType, encounterTime, day);
        }

        // 9. Schedule next NodeArrival
        _queue.Push(new SimEvent
        {
            GameTime = npc.CurrentEnd,
            NpcId = npc.NpcId,
            Type = SimEventType.NodeArrival
        });

        // 10. Log node arrival
        var deltasSnapshot = new Dictionary<string, float>(deltas);
        _logger.LogNodeArrival(npc.NpcId, next.Id, destination, locType,
            _clock, day, npc.Properties, deltasSnapshot);

        // 11. Track metrics
        _metrics.OnStoryletCompleted(npc.NpcId, npc.Role);

        // 12. Collect trace
        if (_tracedNpcs != null && _tracedNpcs.Contains(npc.NpcId))
        {
            _traces.Add(new TraceEntry(
                npc.NpcId, next.Id, npc.CurrentStart, npc.CurrentEnd, destination,
                locType, travelMinutes,
                new Dictionary<string, float>(npc.Properties),
                new Dictionary<string, float>(deltas)));
        }
    }


    private void ProcessEncounter(int npcA, int npcB, int locationId,
        string locationType, DateTime encounterTime, int day)
    {
        // Determine interaction type
        float currentTrust = _relationships.GetTrust(npcA, npcB);
        float angerA = _npcs.TryGetValue(npcA, out var a) ? a.Properties.GetValueOrDefault("anger", 0f) : 0f;
        float angerB = _npcs.TryGetValue(npcB, out var b) ? b.Properties.GetValueOrDefault("anger", 0f) : 0f;
        string interactionType = _relationships.DetermineInteractionType(currentTrust, angerA, angerB, _rng);

        // Update trust
        var (oldTrust, newTrust, oldTier, newTier) = _relationships.RecordInteraction(npcA, npcB, interactionType, day);

        // Log encounter
        _logger.LogEncounter(Math.Min(npcA, npcB), Math.Max(npcA, npcB),
            interactionType, locationId, locationType, encounterTime, day,
            oldTrust, newTrust);

        // Log tier change if applicable
        if (oldTier != newTier)
        {
            var state = _relationships.GetOrCreate(npcA, npcB);
            _logger.LogRelationshipChanged(Math.Min(npcA, npcB), Math.Max(npcA, npcB),
                oldTier, newTier, newTrust, state.TotalInteractions, encounterTime, day);
        }

        // Track metrics
        _metrics.OnEncounter(npcA, npcB, interactionType, locationId, day);

        // Trace encounters
        if (_tracedNpcs != null)
        {
            if (_tracedNpcs.Contains(npcA))
            {
                string otherRole = _npcs.TryGetValue(npcB, out var ob) ? ob.Role : "?";
                _traceEncounters.Add(new TraceEncounter(
                    npcA, npcB, otherRole, interactionType, encounterTime, oldTrust, newTrust));
            }
            if (_tracedNpcs.Contains(npcB))
            {
                string otherRole = _npcs.TryGetValue(npcA, out var oa) ? oa.Role : "?";
                _traceEncounters.Add(new TraceEncounter(
                    npcB, npcA, otherRole, interactionType, encounterTime, oldTrust, newTrust));
            }
        }
    }


    private void EmitDaySummaries(int day)
    {
        if (!_logger.WantsDaySummary) return;
        foreach (var npc in _npcs.Values)
        {
            int storylets = _metrics.GetDailyStorylets(npc.NpcId);
            int encounters = _metrics.GetDailyEncounters(npc.NpcId);
            var topRels = _relationships.GetTopRelationships(npc.NpcId, 5);
            _logger.LogDaySummary(npc.NpcId, day, storylets, encounters,
                npc.Properties, topRels);
        }
    }


    private int ResolveLocation(NpcSchedule npc, StoryletTemplate storylet)
    {
        int resolved = storylet.LocationType switch
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
        int eatId = _spatial.FindNearestOfType(npc.CurrentLocationId, "social_venue", "Eat");
        if (eatId >= 0) return eatId;
        if (npc.SocialVenueIds != null && npc.SocialVenueIds.Count > 0)
            return npc.SocialVenueIds[0];
        return npc.HomeLocationId;
    }


    private int ResolveStreet(NpcSchedule npc)
    {
        int streetId = _spatial.FindNearestOfType(npc.CurrentLocationId, "street_segment");
        return streetId >= 0 ? streetId : npc.CurrentLocationId;
    }
}
