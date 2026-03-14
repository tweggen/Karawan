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
    private StoryletLibrary _library;
    private StoryletSelector _storylets;
    private IEventLogger _logger;
    private RelationshipTracker _relationships;
    private MetricsCollector _metrics;
    private GroupDetector _groupDetector;
    private InteractionPool _interactionPool;
    private DateTime _clock;
    private DateTime _startTime;
    private Random _rng;
    private int _lastGroupDetectionDay;

    // Reusable buffer for property deltas
    private readonly Dictionary<string, float> _deltasBuffer = new();

    public int EventsProcessed { get; private set; }
    public EncounterResolver Encounters => _encounters;
    public RelationshipTracker Relationships => _relationships;
    public MetricsCollector Metrics => _metrics;
    public IReadOnlyDictionary<int, NpcSchedule> Npcs => _npcs;
    public GroupDetectionResult LastGroupDetection { get; private set; }
    public InteractionPool InteractionPool => _interactionPool;

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
        StoryletLibrary library, IEventLogger logger, DateTime startTime, int seed = 42)
    {
        _spatial = model;
        _library = library;
        _logger = logger;
        _startTime = startTime;
        _clock = startTime;
        _rng = new Random(seed);
        _queue = new EventQueue();
        _encounters = new EncounterResolver();
        _storylets = new StoryletSelector(library);
        _relationships = new RelationshipTracker();
        _metrics = new MetricsCollector();
        _groupDetector = new GroupDetector();
        _interactionPool = new InteractionPool();
        _lastGroupDetectionDay = 0;

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
                ApplyMoralityDrift();

                // Interaction pool cleanup and abstract resolution
                ResolveInteractionsDaily(nextDayBoundary, completedDay);

                // Group detection every 30 days
                if (completedDay - _lastGroupDetectionDay >= 30)
                {
                    LastGroupDetection = _groupDetector.Detect(_relationships, _npcs);
                    _metrics.OnGroupDetection(LastGroupDetection, completedDay);
                    _lastGroupDetectionDay = completedDay;
                }

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

        // Final group detection
        LastGroupDetection = _groupDetector.Detect(_relationships, _npcs);
        _metrics.OnGroupDetection(LastGroupDetection, finalDay);

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
            "Authority" => 6f,
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

        // 1. Apply postconditions from previous storylet (data-driven if definition found)
        var prevDef = _library.GetById(previousStorylet);
        var deltas = prevDef != null
            ? _storylets.ApplyPostconditions(npc, prevDef, previousDuration, _deltasBuffer)
            : _storylets.ApplyPostconditions(npc, previousStorylet, previousDuration, _deltasBuffer);

        // Track property changes for metrics
        foreach (var kvp in npc.Properties)
            _metrics.OnPropertyChanged(npc.NpcId, kvp.Key, kvp.Value);

        // 2. Select next storylet (with interrupt and escalation resolution)
        StoryletDefinition next;

        // 2a. Handle forced next storylet from conditional postcondition
        if (npc.NextForcedStorylet != null)
        {
            next = _library.GetById(npc.NextForcedStorylet) ?? _storylets.SelectNext(npc, _clock);
            npc.NextForcedStorylet = null;
        }
        // 2b. Handle pending interrupt
        else if (npc.ArcStack.HasPendingInterrupt)
        {
            var scope = npc.ArcStack.PendingInterruptScope!.Value;
            var interruptId = npc.ArcStack.PendingInterruptStorylet!;
            npc.ArcStack.ClearInterrupt();

            if (scope == InterruptScope.Nest)
            {
                // Pause current storylet, switch to interrupt
                npc.ArcStack.Push(new PausedStorylet
                {
                    StoryletId = previousStorylet,
                    RemainingDurationMinutes = Math.Max(0, (float)(npc.CurrentEnd - _clock).TotalMinutes),
                    PropertiesAtPause = new Dictionary<string, float>(npc.Properties)
                });
                next = _library.GetById(interruptId) ?? _storylets.SelectNext(npc, _clock);
            }
            else if (scope == InterruptScope.Replace)
            {
                // Simply replace current with interrupt (don't pause)
                next = _library.GetById(interruptId) ?? _storylets.SelectNext(npc, _clock);
            }
            else // Cancel
            {
                // Fall through to normal selection
                next = _storylets.SelectNext(npc, _clock);
            }
        }
        // 2c. Resume paused arc if no pending interrupt
        else if (npc.ArcStack.PausedArcs.Count > 0)
        {
            var resumed = npc.ArcStack.TryPop()!.Value;
            next = _library.GetById(resumed.StoryletId) ?? _storylets.SelectNext(npc, _clock);
            _logger.LogStoryletResumed(npc.NpcId, resumed.StoryletId, day, _clock);
        }
        else
        {
            // Normal selection
            next = _storylets.SelectNext(npc, _clock);
        }

        npc.ScheduleStep++;

        // 3. Resolve destination location
        int destination = ResolveLocation(npc, next);

        // 4. Compute travel time
        float travelMinutes = _spatial.GetTravelTime(npc.CurrentLocationId, destination);

        // 5. Determine duration
        float durationMinutes = next.GetDuration(_rng);

        // 6. Update NPC state
        npc.CurrentStorylet = next.Id;
        npc.CurrentLocationId = destination;
        npc.CurrentStart = _clock + TimeSpan.FromMinutes(travelMinutes);
        npc.CurrentEnd = npc.CurrentStart + TimeSpan.FromMinutes(durationMinutes);

        // 6b. Emit interaction request if postcondition specifies one
        if (next.RequestPostcondition != null)
        {
            int reqLoc = destination; // Use current location
            DateTime reqTimeout = _clock.AddMinutes(next.RequestPostcondition.TimeoutMinutes);
            int requestId = _interactionPool.EmitRequest(
                npc.NpcId, next.RequestPostcondition.Type, reqLoc,
                next.RequestPostcondition.Urgency, reqTimeout, next.Id);
            _logger.LogRequestEmitted(requestId, npc.NpcId, next.RequestPostcondition.Type,
                reqLoc, next.RequestPostcondition.Urgency,
                next.RequestPostcondition.TimeoutMinutes, next.Id, _clock, day);
        }

        // 7. Register presence for encounter detection
        _encounters.RegisterPresence(npc.NpcId, destination, npc.CurrentStart, npc.CurrentEnd);

        // 8. Check for encounters at this location
        var loc = _spatial.GetLocation(destination);
        string locType = loc?.Type ?? "street_segment";
        var encounterResults = _encounters.CheckEncounters(
            npc.NpcId, destination, npc.CurrentStart, npc.CurrentEnd, locType, _rng);

        // 9. Process encounters: determine type, update trust, log
        foreach (var (otherId, encounterTime) in encounterResults)
        {
            ProcessEncounter(npc.NpcId, otherId, destination, locType, encounterTime, day);
        }

        // 10. Schedule next NodeArrival
        _queue.Push(new SimEvent
        {
            GameTime = npc.CurrentEnd,
            NpcId = npc.NpcId,
            Type = SimEventType.NodeArrival
        });

        // 11. Log node arrival
        var deltasSnapshot = new Dictionary<string, float>(deltas);
        _logger.LogNodeArrival(npc.NpcId, next.Id, destination, locType,
            _clock, day, npc.Properties, deltasSnapshot);

        // 12. Track metrics
        _metrics.OnStoryletCompleted(npc.NpcId, npc.Role);

        // 13. Collect trace
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
        // Record encounter partners for interrupt context
        var schedA = _npcs.GetValueOrDefault(npcA);
        var schedB = _npcs.GetValueOrDefault(npcB);
        if (schedA != null) schedA.LastEncounterPartnerId = npcB;
        if (schedB != null) schedB.LastEncounterPartnerId = npcA;

        // Check for interaction requests that either NPC can claim
        CheckAndClaimRequests(npcA, npcB, encounterTime, day);
        CheckAndClaimRequests(npcB, npcA, encounterTime, day);

        // Determine interaction type using NPC-aware overload
        float currentTrust = _relationships.GetTrust(npcA, npcB);

        string interactionType;
        if (schedA != null && schedB != null)
            interactionType = _relationships.DetermineInteractionType(currentTrust, schedA, schedB, _rng);
        else
        {
            float angerA = schedA?.Properties.GetValueOrDefault("anger", 0f) ?? 0f;
            float angerB = schedB?.Properties.GetValueOrDefault("anger", 0f) ?? 0f;
            interactionType = _relationships.DetermineInteractionType(currentTrust, angerA, angerB, _rng);
        }

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
                string otherRole = schedB?.Role ?? "?";
                _traceEncounters.Add(new TraceEncounter(
                    npcA, npcB, otherRole, interactionType, encounterTime, oldTrust, newTrust));
            }
            if (_tracedNpcs.Contains(npcB))
            {
                string otherRole = schedA?.Role ?? "?";
                _traceEncounters.Add(new TraceEncounter(
                    npcB, npcA, otherRole, interactionType, encounterTime, oldTrust, newTrust));
            }
        }

        // Phase 5: Apply conditional postconditions and trigger escalation/interrupts
        if (schedA != null && schedB != null)
        {
            // Check if A's current storylet has conditional postconditions
            var defA = _library.GetById(schedA.CurrentStorylet);
            if (defA?.PostconditionsIf != null)
            {
                string? nextA = StoryletSelector.ApplyConditionalPostconditions(defA, schedA, schedB);
                if (nextA != null)
                {
                    schedA.NextForcedStorylet = nextA;
                    _logger.LogEscalationTriggered(schedA.NpcId, defA.Id, schedB.NpcId, day, encounterTime);
                }
                // Trigger interrupt on B if A's storylet has high priority
                if (defA.InterruptPriority >= 5)
                {
                    var defB = _library.GetById(schedB.CurrentStorylet);
                    if (defB == null || defB.InterruptPriority < defA.InterruptPriority)
                    {
                        var scope = defA.InterruptPriority >= 8 ? InterruptScope.Replace : InterruptScope.Nest;
                        schedB.ArcStack.SetInterrupt(defA.Id, scope);
                        _logger.LogInterruptFired(schedB.NpcId, defA.Id, scope.ToString(),
                            schedB.CurrentStorylet, day, encounterTime);
                        _metrics.OnInterrupt();
                    }
                }
            }

            // Check if B's current storylet has conditional postconditions (symmetric)
            var defB2 = _library.GetById(schedB.CurrentStorylet);
            if (defB2?.PostconditionsIf != null)
            {
                string? nextB = StoryletSelector.ApplyConditionalPostconditions(defB2, schedB, schedA);
                if (nextB != null)
                {
                    schedB.NextForcedStorylet = nextB;
                    _logger.LogEscalationTriggered(schedB.NpcId, defB2.Id, schedA.NpcId, day, encounterTime);
                }
                // Trigger interrupt on A if B's storylet has high priority
                if (defB2.InterruptPriority >= 5)
                {
                    var defA2 = _library.GetById(schedA.CurrentStorylet);
                    if (defA2 == null || defA2.InterruptPriority < defB2.InterruptPriority)
                    {
                        var scope = defB2.InterruptPriority >= 8 ? InterruptScope.Replace : InterruptScope.Nest;
                        schedA.ArcStack.SetInterrupt(defB2.Id, scope);
                        _logger.LogInterruptFired(schedA.NpcId, defB2.Id, scope.ToString(),
                            schedA.CurrentStorylet, day, encounterTime);
                        _metrics.OnInterrupt();
                    }
                }
            }
        }
    }


    private void CheckAndClaimRequests(int npcId, int otherNpcId, DateTime encounterTime, int day)
    {
        var npc = _npcs.GetValueOrDefault(npcId);
        if (npc == null) return;

        // Find active requests this NPC can claim
        var activeRequests = _interactionPool.GetActiveRequests(encounterTime);
        foreach (var request in activeRequests)
        {
            // Skip if already claimed
            if (request.ClaimerId.HasValue) continue;

            // Look for a storylet with a matching claim trigger
            var candidates = _library.GetCandidates(npc.Role);
            foreach (var storylet in candidates)
            {
                if (storylet.ClaimTrigger == null) continue;
                if (storylet.ClaimTrigger.RequestType != request.Type) continue;

                // Check if NPC's role matches the claim trigger
                if (storylet.ClaimTrigger.RoleMatch != null &&
                    storylet.ClaimTrigger.RoleMatch.Length > 0 &&
                    !storylet.ClaimTrigger.RoleMatch.Contains(npc.Role.ToLowerInvariant()))
                    continue;

                // Claim the request
                if (_interactionPool.ClaimRequest(request.Id, npcId))
                {
                    _logger.LogRequestClaimed(request.Id, npcId, encounterTime, day);
                    // Emit fulfillment signal for direct Tier-2 claim
                    int signalId = _interactionPool.EmitSignal(request.Id, "request_fulfilled", npcId, encounterTime);
                    _logger.LogSignalEmitted(signalId, request.Id, "request_fulfilled", npcId, encounterTime, day);
                    break; // Only claim one request per encounter
                }
            }
        }
    }


    private void ResolveInteractionsDaily(DateTime dayBoundary, int completedDay)
    {
        // Purge expired requests
        int expiredCount = _interactionPool.PurgeExpired(dayBoundary);

        // Tier 3 abstract resolution: for unclaimed requests, check if any Tier 3 NPC matches
        var activeRequests = _interactionPool.GetActiveRequests(dayBoundary);
        foreach (var request in activeRequests)
        {
            if (request.ClaimerId.HasValue) continue; // Already claimed

            // Get matching request types and candidate roles
            var requestTypeCapabilities = GetCapableRoles(request.Type);
            var candidates = _npcs.Values
                .Where(n => requestTypeCapabilities.Contains(n.Role.ToLowerInvariant()))
                .ToList();

            if (candidates.Any())
            {
                // Pick a random candidate to fulfill abstractly
                var chosen = candidates[_rng.Next(candidates.Count)];
                int signalId = _interactionPool.EmitSignal(request.Id, "request_fulfilled",
                    -1, dayBoundary); // -1 = abstract/system source
                _logger.LogSignalEmitted(signalId, request.Id, "request_fulfilled", -1, dayBoundary, completedDay);
            }
        }
    }


    private static HashSet<string> GetCapableRoles(string requestType)
    {
        // Map request types to roles that can fulfill them
        return requestType switch
        {
            "food_delivery" => new HashSet<string> { "merchant", "drifter" },
            "restock_supply" => new HashSet<string> { "merchant", "drifter" },
            "trade_service" => new HashSet<string> { "merchant", "drifter" },
            "greeting" => new HashSet<string> { "worker", "socialite", "merchant", "drifter" },
            "help_request" => new HashSet<string> { "worker", "socialite" },
            "argument" => new HashSet<string> { "worker", "socialite", "drifter" },
            "threat" => new HashSet<string> { "drifter" },
            "pickpocket" => new HashSet<string> { "authority" },
            "blackmail" => new HashSet<string> { "drifter" },
            "crime_report" => new HashSet<string> { "authority" },
            _ => new HashSet<string>()
        };
    }


    /// <summary>
    /// Drift morality based on desperation. Called once per sim day.
    /// </summary>
    private void ApplyMoralityDrift()
    {
        foreach (var npc in _npcs.Values)
        {
            float morality = npc.Properties.GetValueOrDefault("morality", 0.7f);
            float desperation = StoryletSelector.ComputeDesperation(npc);

            // Desperation pushes morality down
            float drift = 0f;
            if (desperation > 0.4f)
                drift -= (desperation - 0.4f) * 0.03f;

            // Low desperation allows slow morality recovery
            if (desperation < 0.2f)
                drift += 0.003f;

            if (drift != 0f)
                npc.Properties["morality"] = Math.Clamp(morality + drift, 0f, 1f);
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
