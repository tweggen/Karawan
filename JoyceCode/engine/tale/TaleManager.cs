using System;
using System.Collections.Concurrent;
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
    private static readonly engine.Dc _dc = engine.Dc.TaleManager;

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
    /// Uses ConcurrentDictionary for thread-safe concurrent NPC spawning.
    /// </summary>
    private readonly ConcurrentDictionary<string, float> _deltasBuffer = new();

    private readonly TalePopulationGenerator _generator = new();

    /// <summary>
    /// Per-cluster social state (group table + depopulation snapshots).
    /// Deliberately NOT cleared on DepopulateCluster — the snapshots carry
    /// evolved trust/groups across populate cycles within a session.
    /// Guarded by _loSocial: AdvanceNpc runs concurrently during spawn
    /// catch-up (see _deltasBuffer) and later phases mutate the table there.
    /// </summary>
    private readonly Dictionary<int, ClusterSocialState> _socialStates = new();
    private readonly object _loSocial = new();

    /// <summary>
    /// Hook for game-layer group naming (flavor text is game content, not
    /// engine). Args: clusterIndex, groupId, groupType. Null → empty names.
    /// </summary>
    public Func<int, int, string, string> GroupNameProvider;


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
    /// gameNow anchors the warmup at the live game clock so the resulting transit
    /// windows align with the time TaleSpawnOperator will materialize NPCs at.
    /// When null, falls back to a deterministic constant (engine-layer callers
    /// without a daynite clock, e.g. tests).
    /// </summary>
    public void PopulateCluster(ClusterDesc clusterDesc, SpatialModel spatialModel, DateTime? gameNow = null)
    {
        int clusterIndex = clusterDesc.Index;

        if (_populatedClusters.Contains(clusterIndex))
        {
            Trace(_dc, $"Cluster {clusterIndex} already populated, skipping.");
            return;
        }

        if (spatialModel != null)
        {
            _spatialModels[clusterIndex] = spatialModel;
            if (spatialModel.Locations.Count == 0)
            {
                Warning(_dc, $"Cluster {clusterIndex} spatial model has ZERO locations! " +
                        $"({spatialModel.BuildingCount} buildings, {spatialModel.ShopCount} shops, " +
                        $"{spatialModel.StreetPointCount} street points). NPCs may spawn at Y=0.");
            }
            else
            {
                Trace(_dc, $"Cluster {clusterIndex} spatial model: " +
                      $"{spatialModel.Locations.Count} locations, {spatialModel.BuildingCount} buildings, " +
                      $"{spatialModel.ShopCount} shops, {spatialModel.StreetPointCount} street points.");
            }
        }
        else
        {
            Warning(_dc, $"Cluster {clusterIndex} has NO spatial model!");
        }

        _deviationSkipMasks.TryGetValue(clusterIndex, out var skipMask);
        var schedules = _generator.Generate(clusterDesc, spatialModel, skipMask);

        foreach (var schedule in schedules)
        {
            _schedules[schedule.NpcId] = schedule;
        }

        // Warm up newly generated schedules so NPCs are mid-storylet when the player
        // first encounters them, instead of all parked at their home location. Anchor
        // the warmup at the live game clock when supplied so the resulting transit
        // windows align with the time TaleSpawnOperator and PositionAt will use moments
        // later. The ±45-minute per-NPC offset desynchronizes schedule positions; the
        // second pass re-aligns everyone so some transits straddle gameStartTime.
        DateTime gameStartTime = gameNow ?? new DateTime(2000, 1, 1, 0, 0, 0)
            .AddDays(1).AddHours(22).AddMinutes(46);

        foreach (var schedule in schedules)
        {
            // Deterministic per-NPC offset: seed RNG from NpcId
            var offsetRng = new builtin.tools.RandomSource(schedule.NpcId.ToString());
            int offsetMinutes = offsetRng.GetInt(91) - 45; // ±45 minutes: 0-90 becomes -45 to 45
            DateTime npcTime = gameStartTime.AddMinutes(offsetMinutes);

            AdvanceNpc(schedule.NpcId, npcTime);
            Trace(_dc, $"Advanced NPC {schedule.NpcId} through full cycle (24+ hours) to {npcTime:HH:mm} " +
                  $"(offset {offsetMinutes:+0;-0} min), now at location {schedule.CurrentLocationId}");
        }

        // Final advance to actual game start time so all transit phases align with spawn time
        foreach (var schedule in schedules)
        {
            AdvanceNpc(schedule.NpcId, gameStartTime);
        }

        // TALE-SOCIAL Phase D3: re-attach a baked scenario's groups, trust edges
        // and post-365-day property snapshot onto the real cluster NPCs. Runs
        // AFTER both warmup advance loops on purpose: the warmup desynchronizes
        // schedule positions but its property drift is then overwritten by the
        // scenario's settled state, so the cluster starts with realistic
        // post-simulation social structure but staggered storylet timing.
        // Skipped silently if no scenarios are configured (selector returns null).
        try
        {
            var selector = I.Get<engine.tale.bake.ScenarioSelector>();
            var library = I.Get<engine.tale.bake.ScenarioLibrary>();
            var applicator = I.Get<engine.tale.bake.ScenarioApplicator>();
            if (selector != null && library != null && applicator != null && schedules.Count > 0)
            {
                var bakeRequest = selector.Pick(schedules.Count, clusterIndex);
                if (bakeRequest != null)
                {
                    var scenario = library.GetOrLoad(bakeRequest);
                    if (scenario != null)
                    {
                        var stats = applicator.Apply(scenario, schedules);
                        _buildGroupTable(clusterIndex, stats.Groups);
                        Trace(_dc, $"Applied scenario {bakeRequest.CategoryName}/{bakeRequest.Index} " +
                              $"to cluster {clusterIndex}: matched {stats.MatchedNpcCount}/{schedules.Count} npcs " +
                              $"(unmatched scenario={stats.UnmatchedScenarioRanks}, real={stats.UnmatchedRealNpcs}), " +
                              $"applied {stats.RelationshipsApplied} trust edges (skipped {stats.RelationshipsSkipped}), " +
                              $"touched {stats.GroupsTouched} groups. " +
                              $"Per-role: {string.Join(", ", stats.MatchedByRole.Select(kv => $"{kv.Key}={kv.Value}"))}");
                    }
                    else
                    {
                        Trace(_dc, $"ScenarioLibrary returned null for {bakeRequest.CategoryName}/{bakeRequest.Index}; skipping scenario application.");
                    }
                }
                else
                {
                    Trace(_dc, $"No scenario available for cluster {clusterIndex} ({schedules.Count} npcs); skipping scenario application.");
                }
            }
        }
        catch (Exception ex)
        {
            Warning(_dc, $"Scenario application failed for cluster {clusterIndex}: {ex.Message}");
        }

        // Phase E2: overlay depopulation snapshots AFTER scenario application —
        // evolved social state from a previous visit trumps the static bake.
        lock (_loSocial)
        {
            if (_socialStates.TryGetValue(clusterIndex, out var social) && social.Snapshots.Count > 0)
            {
                int restored = 0;
                foreach (var schedule in schedules)
                {
                    if (social.RestoreNpc(schedule)) restored++;
                }
                Trace(engine.Dc.TaleSocial, $"Cluster {clusterIndex}: restored social snapshots " +
                      $"for {restored}/{schedules.Count} NPCs ({social.Groups.Count} groups in table).");
            }
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
        Trace(_dc, $"Populated cluster {clusterIndex} with {schedules.Count} NPCs. " +
              $"Total schedules now: {_schedules.Count}. Fragment distribution: {distrib}");
        Trace(_dc, $"{inTransit.Count}/{schedules.Count} NPCs in transit at spawn time ({gameStartTime:HH:mm})");

        // Show NPCs in transit for debugging
        if (inTransit.Count > 0)
        {
            Trace(_dc, $"NPCs in transit:");
            for (int i = 0; i < Math.Min(10, inTransit.Count); i++)
            {
                var s = inTransit[i];
                var fromLoc = spatialModel?.GetLocation(s.TransitFromLocationId);
                var toLoc = spatialModel?.GetLocation(s.TransitToLocationId);
                string fromName = fromLoc?.Type ?? "unknown";
                string toName = toLoc?.Type ?? "unknown";
                float travelMinutes = (float)(s.TransitEnd - s.TransitStart).TotalMinutes;
                Trace(_dc, $"  NPC {s.NpcId} ({s.Role}): {fromName} → {toName} " +
                      $"({travelMinutes:F1} min, {s.TransitStart:HH:mm}-{s.TransitEnd:HH:mm})");
            }
        }

        // Show first few NPCs for debugging
        for (int i = 0; i < Math.Min(3, schedules.Count); i++)
        {
            var s = schedules[i];
            Trace(_dc, $"  NPC[{i}] id={s.NpcId} role={s.Role} home={s.HomePosition} " +
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

        // Phase E2: snapshot the cluster's social state (groups, trust, social
        // props) before the schedules are thrown away, so the evolved fabric
        // survives the player leaving and returning. Deviated NPCs are
        // snapshotted too — RestoreNpc only touches regenerated schedules, and
        // a stale entry is overwritten on the next depopulate.
        lock (_loSocial)
        {
            if (!_socialStates.TryGetValue(clusterIndex, out var social))
            {
                social = new ClusterSocialState { ClusterIndex = clusterIndex };
                _socialStates[clusterIndex] = social;
            }
            int snapshotted = 0;
            foreach (var kvp in _schedules)
            {
                if (kvp.Value.ClusterIndex == clusterIndex)
                {
                    social.SnapshotNpc(kvp.Value);
                    snapshotted++;
                }
            }
            Trace(engine.Dc.TaleSocial, $"Cluster {clusterIndex}: snapshotted social state " +
                  $"for {snapshotted} NPCs on depopulate.");
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


    /// <summary>
    /// Currently populated cluster indices, sorted (Phase E3 social tick
    /// iterates these round-robin).
    /// </summary>
    public List<int> GetPopulatedClusters()
    {
        var list = _populatedClusters.ToList();
        list.Sort();
        return list;
    }


    /// <summary>
    /// All schedules belonging to a cluster.
    /// </summary>
    public List<NpcSchedule> GetNpcsInCluster(int clusterIndex)
    {
        var result = new List<NpcSchedule>();
        foreach (var kvp in _schedules)
        {
            if (kvp.Value.ClusterIndex == clusterIndex)
                result.Add(kvp.Value);
        }
        return result;
    }


    #endregion


    #region Social State (Phase E2)

    /// <summary>
    /// Seed the runtime group table from the applied scenario's groups.
    /// Existing table entries (from a previous visit's snapshots) win: the
    /// bake only fills a table that evolution hasn't already shaped.
    /// </summary>
    private void _buildGroupTable(int clusterIndex, IReadOnlyList<bake.ScenarioApplicator.AppliedGroup> appliedGroups)
    {
        lock (_loSocial)
        {
            if (!_socialStates.TryGetValue(clusterIndex, out var social))
            {
                social = new ClusterSocialState { ClusterIndex = clusterIndex };
                _socialStates[clusterIndex] = social;
            }

            if (social.Groups.Count > 0)
            {
                Trace(engine.Dc.TaleSocial, $"Cluster {clusterIndex}: group table already has " +
                      $"{social.Groups.Count} groups (evolved state); not reseeding from bake.");
                return;
            }

            int maxId = 0;
            foreach (var ag in appliedGroups)
            {
                social.Groups[ag.GroupRank] = new RuntimeGroup
                {
                    GroupId = ag.GroupRank,
                    Type = ag.Type,
                    Name = GroupNameProvider?.Invoke(clusterIndex, ag.GroupRank, ag.Type) ?? "",
                    MemberIds = new List<int>(ag.RealMemberIds)
                };
                if (ag.GroupRank > maxId) maxId = ag.GroupRank;
            }
            social.NextGroupId = maxId + 1;

            if (social.Groups.Count > 0)
            {
                var byType = social.Groups.Values
                    .GroupBy(g => g.Type)
                    .Select(g => $"{g.Key}={g.Count()}");
                Trace(engine.Dc.TaleSocial, $"Cluster {clusterIndex}: group table seeded with " +
                      $"{social.Groups.Count} groups ({string.Join(", ", byType)}).");
            }
        }
    }


    /// <summary>
    /// The cluster's social state, or null if the cluster was never populated.
    /// </summary>
    public ClusterSocialState GetSocialState(int clusterIndex)
    {
        lock (_loSocial)
        {
            return _socialStates.TryGetValue(clusterIndex, out var social) ? social : null;
        }
    }


    /// <summary>
    /// Look up a runtime group, or null. groupId -1 (ungrouped) yields null.
    /// </summary>
    public RuntimeGroup GetGroup(int clusterIndex, int groupId)
    {
        if (groupId < 0) return null;
        lock (_loSocial)
        {
            if (!_socialStates.TryGetValue(clusterIndex, out var social)) return null;
            return social.Groups.TryGetValue(groupId, out var group) ? group : null;
        }
    }


    /// <summary>
    /// The NPC's groupmates (excluding the NPC itself); empty when ungrouped
    /// or unknown.
    /// </summary>
    public IReadOnlyList<int> GetGroupmates(int npcId)
    {
        var npc = GetSchedule(npcId);
        if (npc == null || npc.GroupId < 0) return Array.Empty<int>();
        var group = GetGroup(npc.ClusterIndex, npc.GroupId);
        if (group == null) return Array.Empty<int>();
        return group.MemberIds.Where(id => id != npcId).ToList();
    }

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

    /// <summary>
    /// True iff a schedule with this id is still tracked. Cheap guard for callers
    /// (e.g. quest steps) that hold an NpcId across time and need to know whether
    /// the NPC has been forgotten / re-randomized.
    /// </summary>
    public bool IsScheduleAlive(int npcId) => _schedules.ContainsKey(npcId);

    /// <summary>
    /// Drop an NPC's schedule entirely. Called by the dematerialization decay path
    /// (TaleSpawnOperator.TerminateCharacters) when an NPC is no longer "remembered"
    /// (HasPlayerDeviation cleared, or never set, AND no recent conversation).
    /// The NPC's slot can be re-randomized by future cluster repopulation.
    /// </summary>
    public void ForgetSchedule(int npcId)
    {
        _schedules.Remove(npcId);
        _tier2NpcIds.Remove(npcId);
    }


    /// <summary>
    /// Default wall-clock window used by the decay path in TaleSpawnOperator
    /// to decide whether an NPC is still "remembered" at dematerialization time.
    /// 1800 s = 30 minutes. Other call sites are free to pass their own window
    /// to <see cref="HasRecentConversation"/>.
    /// </summary>
    public const double ConversationMemorySeconds = 1800.0;

    /// <summary>
    /// Stamp <see cref="NpcSchedule.LastConversationTime"/> to UtcNow.
    /// Called from the Tale*Behavior.OnAction methods every time the player
    /// initiates a conversation. No-op if NPC unknown.
    ///
    /// This used to double as an anti-spam cooldown; that gate has been removed.
    /// The timestamp is a memory signal: the dematerialization decay path
    /// (TaleSpawnOperator.TerminateCharacters) checks it to decide whether to
    /// preserve the NPC as Tier-2 or drop the schedule as forgettable.
    /// </summary>
    public void RecordConversation(int npcId)
    {
        if (_schedules.TryGetValue(npcId, out var schedule))
            schedule.LastConversationTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Returns true if the player has talked to this NPC within the last
    /// <paramref name="withinSeconds"/> real seconds. The window is per-callsite
    /// policy; the canonical default is <see cref="ConversationMemorySeconds"/>.
    /// Returns false for unknown NPCs and for NPCs that have never been talked to.
    /// </summary>
    public bool HasRecentConversation(int npcId, double withinSeconds)
    {
        if (!_schedules.TryGetValue(npcId, out var schedule)) return false;
        if (schedule.LastConversationTime == DateTime.MinValue) return false;
        return (DateTime.UtcNow - schedule.LastConversationTime).TotalSeconds
               < withinSeconds;
    }

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
        StoryletDefinition next;
        try
        {
            next = _selector.SelectNext(npc, gameNow);
        }
        catch (InvalidOperationException ex)
        {
            Warning(_dc, $"StoryletSelector failed for npc={npc.NpcId} role={npc.Role}: {ex.Message}. " +
                    $"Using fallback stay at current location.");
            return null; // Can't advance this NPC
        }

        if (next == null)
        {
            Warning(_dc, $"SelectNext returned null for npc={npc.NpcId} role={npc.Role}. " +
                    $"Using fallback stay at current location.");
            return null; // Can't advance this NPC
        }

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
        // Null safety: if no storylet selected, use current location
        if (storylet == null)
        {
            Warning(_dc, $"ResolveLocation received null storylet for npc={npc.NpcId} role={npc.Role}, " +
                    $"using currentLoc={npc.CurrentLocationId}.");
            return npc.CurrentLocationId;
        }

        var locType = storylet.ResolveLocationType();
        int resolved = locType switch
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
            Warning(_dc, $"ResolveLocation fallback for npc={npc.NpcId} role={npc.Role} " +
                    $"storylet={storylet.Id} locType={locType}: resolved={resolved}, using currentLoc={npc.CurrentLocationId}.");
            resolved = npc.CurrentLocationId;
        }

        // Check if resolved location is a street_segment
        if (_spatialModels.TryGetValue(npc.ClusterIndex, out var sm) && sm != null)
        {
            var loc = sm.GetLocation(resolved);
            if (loc != null && loc.Type == "street_segment")
            {
                Warning(_dc, $"NPC {npc.NpcId} role={npc.Role} resolved to street_segment " +
                        $"for locType={locType} storylet={storylet.Id} (locId={resolved}).");
            }
        }

        return resolved;
    }


    private int ResolveSocialVenue(NpcSchedule npc)
    {
        if (npc.SocialVenueIds == null || npc.SocialVenueIds.Count == 0)
        {
            Warning(_dc, $"ResolveSocialVenue fallback for npc={npc.NpcId} role={npc.Role}: " +
                    $"no social venues assigned, using home={npc.HomeLocationId}.");
            return npc.HomeLocationId;
        }
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
            Warning(_dc, $"ResolveEatVenue fallback for npc={npc.NpcId} role={npc.Role}: " +
                    $"no 'Eat' venue found near loc={npc.CurrentLocationId}.");
        }
        if (npc.SocialVenueIds != null && npc.SocialVenueIds.Count > 0)
        {
            Warning(_dc, $"ResolveEatVenue using first social venue={npc.SocialVenueIds[0]} " +
                    $"for npc={npc.NpcId}.");
            return npc.SocialVenueIds[0];
        }
        Warning(_dc, $"ResolveEatVenue final fallback for npc={npc.NpcId} role={npc.Role}: " +
                $"no venues at all, using home={npc.HomeLocationId}.");
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
