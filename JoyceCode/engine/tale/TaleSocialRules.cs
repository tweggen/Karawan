using System;
using System.Collections.Generic;
using System.Linq;

namespace engine.tale;


/**
 * What a node arrival, an encounter and a day boundary mean for TALE's requests, claims,
 * escalations and interrupts - one set of rules for the two drivers that run them
 * (KAR-486, TALE S2b-a; plan docs/tale/plans/proposed/TALE-S2B-LIVE-ESCALATION.md):
 * DesSimulation, which bakes each city's society, and the live game (TaleManager), which
 * gets them in S2b-b/c.
 *
 * Moved unchanged out of DesSimulation. What differs per driver is passed in: the request
 * pool, the logger and metrics, and - where a rule draws at random - the random source. The
 * DES passes its own stream, so a bake reproduces exactly; the live game passes per-event
 * sources. An encounter's interaction type and trust stay with the driver (the DES's
 * RelationshipTracker, the live InteractionTypeRegistry).
 *
 * Not thread-safe by itself: a driver that calls it from several threads holds its own
 * lock around the pool.
 */
public sealed class TaleSocialRules
{
    private readonly StoryletLibrary _library;
    private readonly StoryletSelector _selector;
    private readonly InteractionPool _pool;
    private readonly IEventLogger _logger;
    private readonly MetricsCollector? _metrics;

    /*
     * Per role, the request types any of its storylets can claim (KAR-488). The library is
     * fixed once loaded, so this is filled lazily and never invalidated. Most roles claim
     * few types and most encounters have nothing to claim: without it every encounter
     * walked every open request and the role's storylets for each, 40 ms p99 per social
     * tick at 4095 NPCs.
     */
    private readonly Dictionary<string, HashSet<string>> _claimableByRole = new();

    public InteractionPool Pool => _pool;


    public TaleSocialRules(StoryletLibrary library, StoryletSelector selector, InteractionPool pool,
        IEventLogger logger, MetricsCollector? metrics)
    {
        _library = library;
        _selector = selector;
        _pool = pool;
        _logger = logger ?? new NullEventLogger();
        _metrics = metrics;
    }


    /**
     * The next storylet at a node arrival, in the fixed order: a forced storylet (set by an
     * escalation), a pending interrupt (Nest pauses the current storylet, Replace drops it,
     * Cancel falls through), resuming a paused arc, else routine selection.
     */
    public (StoryletDefinition Next, SelectionSource Source) SelectNext(
        NpcSchedule npc, string previousStorylet, DateTime clock, int day)
    {
        StoryletDefinition next;
        SelectionSource source;

        // Forced next storylet from a conditional postcondition
        if (npc.NextForcedStorylet != null)
        {
            next = _library.GetById(npc.NextForcedStorylet) ?? _selector.SelectNext(npc, clock);
            npc.NextForcedStorylet = null;
            source = SelectionSource.Forced;
        }
        // Pending interrupt
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
                    RemainingDurationMinutes = Math.Max(0, (float)(npc.CurrentEnd - clock).TotalMinutes),
                    PropertiesAtPause = new Dictionary<string, float>(npc.Properties)
                });
                next = _library.GetById(interruptId) ?? _selector.SelectNext(npc, clock);
                source = SelectionSource.InterruptNest;
            }
            else if (scope == InterruptScope.Replace)
            {
                // Simply replace current with interrupt (don't pause)
                next = _library.GetById(interruptId) ?? _selector.SelectNext(npc, clock);
                source = SelectionSource.InterruptReplace;
            }
            else // Cancel
            {
                // Fall through to normal selection
                next = _selector.SelectNext(npc, clock);
                source = SelectionSource.Routine;
            }
        }
        // Resume paused arc if no pending interrupt
        else if (npc.ArcStack.PausedArcs.Count > 0)
        {
            var resumed = npc.ArcStack.TryPop()!.Value;
            next = _library.GetById(resumed.StoryletId) ?? _selector.SelectNext(npc, clock);
            _logger.LogStoryletResumed(npc.NpcId, resumed.StoryletId, day, clock);
            source = SelectionSource.Resumed;
        }
        else
        {
            // Normal selection
            next = _selector.SelectNext(npc, clock);
            source = SelectionSource.Routine;
        }

        _metrics?.OnSelection(npc.Role, source);
        return (next, source);
    }


    /**
     * The request the storylet just started emits, if it has a request postcondition,
     * at the location the NPC is going to.
     */
    public void EmitRequest(NpcSchedule npc, StoryletDefinition next, int location, DateTime clock, int day)
    {
        if (next.RequestPostcondition == null) return;

        DateTime reqTimeout = clock.AddMinutes(next.RequestPostcondition.TimeoutMinutes);
        int requestId = _pool.EmitRequest(
            npc.NpcId, next.RequestPostcondition.Type, location,
            next.RequestPostcondition.Urgency, reqTimeout, next.Id);
        _logger.LogRequestEmitted(requestId, npc.NpcId, next.RequestPostcondition.Type,
            location, next.RequestPostcondition.Urgency,
            next.RequestPostcondition.TimeoutMinutes, next.Id, clock, day);
        _metrics?.OnRequestEmitted(requestId);
    }


    /**
     * An NPC in an encounter claims one active request a storylet of its can answer, and
     * the claim starts that storylet as its interrupt (KAR-477).
     */
    public void ClaimRequests(NpcSchedule npc, DateTime encounterTime, int day)
    {
        if (npc == null) return;
        var claimable = _claimableTypesOf(npc.Role);
        if (claimable.Count == 0) return;

        /*
         * The active requests (unclaimed, not timed out) in the pool's order, as
         * GetActiveRequests lists them, without copying them into a list per encounter.
         * Claiming one only changes that request, and the loop ends right after it.
         */
        foreach (var request in _pool.Requests.Values)
        {
            if (request.ClaimerId.HasValue) continue;
            if (request.Timeout <= encounterTime) continue;
            if (!claimable.Contains(request.Type)) continue;

            /*
             * The storylets that can claim it. Several may - an authority answers a crime
             * report by investigate_crime or investigate_complaint - and the claim picks one by
             * weight (KAR-477), seeded by the request and the claimer so it neither depends on
             * the order the library lists them in nor draws from the driver's random stream.
             */
            var matching = new List<StoryletDefinition>();
            foreach (var candidate in _library.GetCandidates(npc.Role))
            {
                if (candidate.ClaimTrigger == null) continue;
                if (candidate.ClaimTrigger.RequestType != request.Type) continue;
                if (candidate.ClaimTrigger.RoleMatch != null &&
                    candidate.ClaimTrigger.RoleMatch.Length > 0 &&
                    !candidate.ClaimTrigger.RoleMatch.Contains(npc.Role.ToLowerInvariant()))
                    continue;
                matching.Add(candidate);
            }
            if (matching.Count == 0) continue;
            var storylet = PickClaimStorylet(matching, request.Id, npc.NpcId);

            // Claim the request
            if (_pool.ClaimRequest(request.Id, npc.NpcId))
            {
                _logger.LogRequestClaimed(request.Id, npc.NpcId, encounterTime, day);
                _metrics?.OnRequestFulfilled(request.Id);

                /*
                 * The claim starts the storylet that claims it - investigating the crime,
                 * fleeing the threat - as the trigger's interrupt scope says (nest by default).
                 */
                if (npc.ArcStack.PendingInterruptStorylet == null)
                {
                    var scope = ClaimScopeOf(storylet.ClaimTrigger);
                    npc.ArcStack.SetInterrupt(storylet.Id, scope);
                    _logger.LogInterruptFired(npc.NpcId, storylet.Id, scope.ToString(),
                        npc.CurrentStorylet, day, encounterTime);
                    _metrics?.OnInterrupt(npc.NpcId, npc.Role);
                }

                // Emit fulfillment signal for direct Tier-2 claim
                int signalId = _pool.EmitSignal(request.Id, "request_fulfilled", npc.NpcId, encounterTime);
                _logger.LogSignalEmitted(signalId, request.Id, "request_fulfilled", npc.NpcId, encounterTime, day);
                break; // Only claim one request per encounter
            }
        }
    }


    private HashSet<string> _claimableTypesOf(string role)
    {
        if (_claimableByRole.TryGetValue(role, out var types)) return types;
        types = new HashSet<string>();
        string lower = role.ToLowerInvariant();
        foreach (var candidate in _library.GetCandidates(role))
        {
            var trigger = candidate.ClaimTrigger;
            if (trigger?.RequestType == null) continue;
            if (trigger.RoleMatch != null && trigger.RoleMatch.Length > 0 && !trigger.RoleMatch.Contains(lower))
                continue;
            types.Add(trigger.RequestType);
        }
        _claimableByRole[role] = types;
        return types;
    }


    public static StoryletDefinition PickClaimStorylet(List<StoryletDefinition> matching, int requestId, int npcId)
    {
        if (matching.Count == 1) return matching[0];
        float total = 0f;
        foreach (var m in matching) total += Math.Max(0f, m.Weight);
        if (total <= 0f) return matching[0];
        var rng = new Random(unchecked(requestId * 7919 + npcId * 104729));
        float pick = (float)rng.NextDouble() * total;
        foreach (var m in matching)
        {
            pick -= Math.Max(0f, m.Weight);
            if (pick < 0f) return m;
        }
        return matching[^1];
    }


    public static InterruptScope ClaimScopeOf(InteractionClaimTrigger trigger) =>
        (trigger?.InterruptScope ?? "").ToLowerInvariant() switch
        {
            "replace" => InterruptScope.Replace,
            "cancel" => InterruptScope.Cancel,
            _ => InterruptScope.Nest
        };


    /**
     * An encounter's conditional postconditions and the interrupts they bring, both ways:
     * a storylet with postconditions_if checks its conditions against the partner, may force
     * the next storylet (escalation), and - only then - interrupts the partner's
     * lower-priority storylet if its own priority is 5 or more.
     */
    public void ApplyEncounterConditionals(NpcSchedule schedA, NpcSchedule schedB, DateTime encounterTime, int day)
    {
        if (schedA == null || schedB == null) return;

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
                    _metrics?.OnInterrupt(schedB.NpcId, schedB.Role);
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
                    _metrics?.OnInterrupt(schedA.NpcId, schedA.Role);
                }
            }
        }
    }


    /**
     * The day boundary: expired requests are purged, and unclaimed ones are resolved
     * abstractly when any NPC of a capable role exists. The resolver drawn from
     * <paramref name="rng"/> is not used; the draw is kept so the DES's stream - and with it
     * every bake - stays as it was.
     */
    public void ResolveDaily(DateTime dayBoundary, int completedDay,
        IEnumerable<NpcSchedule> npcs, Func<string, HashSet<string>> capableRoles, Random rng)
    {
        // Purge expired requests
        int expiredCount = _pool.PurgeExpired(dayBoundary);

        // Tier 3 abstract resolution: for unclaimed requests, check if any Tier 3 NPC matches
        var activeRequests = _pool.GetActiveRequests(dayBoundary);
        foreach (var request in activeRequests)
        {
            if (request.ClaimerId.HasValue) continue; // Already claimed

            // Get matching request types and candidate roles
            var requestTypeCapabilities = capableRoles(request.Type);
            var candidates = npcs
                .Where(n => requestTypeCapabilities.Contains(n.Role.ToLowerInvariant()))
                .ToList();

            if (candidates.Any())
            {
                // Pick a random candidate to fulfill abstractly
                var chosen = candidates[rng.Next(candidates.Count)];
                int signalId = _pool.EmitSignal(request.Id, "request_fulfilled",
                    -1, dayBoundary); // -1 = abstract/system source
                _logger.LogSignalEmitted(signalId, request.Id, "request_fulfilled", -1, dayBoundary, completedDay);
                _metrics?.OnRequestFulfilled(request.Id);
            }
        }
    }
}
