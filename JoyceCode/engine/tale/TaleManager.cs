using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
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
    /*
     * Spawn workers call AdvanceNpc and the queries below from pool threads
     * while the logical thread populates, registers and ticks, so every
     * collection here is concurrent. Sets are ConcurrentDictionary<int, byte>.
     * This guards the collections, not a single schedule: two threads
     * advancing the SAME NPC at once is still a caller error.
     */
    private readonly ConcurrentDictionary<int, SpatialModel> _spatialModels = new();

    /// <summary>
    /// All schedules by NpcId. Includes both generated (primary) and deviated NPCs.
    /// </summary>
    private readonly ConcurrentDictionary<int, NpcSchedule> _schedules = new();

    /// <summary>
    /// Track which clusters are currently populated.
    /// </summary>
    private readonly ConcurrentDictionary<int, byte> _populatedClusters = new();

    /// <summary>
    /// Deviation skip masks per cluster: cluster index → set of NPC indices to skip on regeneration.
    /// Copy-on-write: a published set is never modified, so readers may hold it.
    /// </summary>
    private readonly ConcurrentDictionary<int, HashSet<int>> _deviationSkipMasks = new();

    /// <summary>
    /// NPC IDs that are currently materialized as Tier 2/1 ECS entities.
    /// </summary>
    private readonly ConcurrentDictionary<int, byte> _materializedNpcIds = new();

    /// <summary>
    /// NPC IDs that are currently in Tier 2 (noticed but dematerialized, map icon visible).
    /// These remain in _materializedNpcIds; strategy is frozen.
    /// </summary>
    private readonly ConcurrentDictionary<int, byte> _tier2NpcIds = new();

    /// <summary>
    /// Per-thread buffer for property deltas in ApplyPostconditions. It only
    /// feeds logging (the deltas are written to npc.Properties directly), and
    /// one buffer per thread keeps concurrent advances from clearing each other's.
    /// </summary>
    [ThreadStatic] private static ConcurrentDictionary<string, float> _deltasBuffer;

    private readonly TalePopulationGenerator _generator = new();

    /// <summary>
    /// Which loaded fragments keep each cluster populated (S0 W4). The spawn
    /// operator writes it, TaleModule depopulates what it reports as due.
    /// </summary>
    public TaleClusterResidency Residency { get; } = new();

    /// <summary>
    /// Per-cluster social state (group table + depopulation snapshots).
    /// Deliberately NOT cleared on DepopulateCluster — the snapshots carry
    /// evolved trust/groups across populate cycles within a session.
    /// Guarded by _loSocial: AdvanceNpc runs concurrently during spawn
    /// catch-up and later phases mutate the table there.
    /// </summary>
    private readonly Dictionary<int, ClusterSocialState> _socialStates = new();
    private readonly object _loSocial = new();

    /// <summary>
    /// What the scenario bake did to each cluster at its last populate (S0 W6).
    /// Guarded by _loSocial.
    /// </summary>
    private readonly Dictionary<int, SocialBakeInfo> _bakeInfo = new();

    /// <summary>
    /// Hook for game-layer group naming (flavor text is game content, not
    /// engine). Args: clusterIndex, groupId, groupType. Null → empty names.
    /// </summary>
    public Func<int, int, string, string> GroupNameProvider;


    /// <summary>
    /// The seed is unused since S0 W2: AdvanceNpc draws from per-(NPC, step)
    /// sources, so results don't depend on call order. Kept for callers.
    /// </summary>
    public void Initialize(StoryletLibrary library, int seed = 42)
    {
        _library = library;
        _selector = new StoryletSelector(library);
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
        PopulateClusterWith(clusterDesc.Index, spatialModel, gameNow,
            skipMask => _generator.Generate(clusterDesc, spatialModel, skipMask));
    }


    /// <summary>
    /// PopulateCluster with the schedule generation passed in, so tests can
    /// populate without a generated ClusterDesc (S0 W4). <paramref name="generate"/>
    /// gets the cluster's skip mask (may be null) and returns the fresh
    /// schedules. <paramref name="applyScenario"/>, when given, replaces the
    /// scenario bake lookup and returns the groups it applied.
    /// </summary>
    internal void PopulateClusterWith(
        int clusterIndex, SpatialModel spatialModel, DateTime? gameNow,
        Func<HashSet<int>, List<NpcSchedule>> generate,
        Func<List<NpcSchedule>, IReadOnlyList<bake.ScenarioApplicator.AppliedGroup>> applyScenario = null)
    {
        if (_populatedClusters.ContainsKey(clusterIndex))
        {
            Trace(_dc, $"Cluster {clusterIndex} already populated, skipping.");
            return;
        }
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

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
        var schedules = generate(skipMask);

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

        // S0 W4: the warm-up fires no group verbs. They would run against the
        // group table before the snapshot overlay restores GroupId, and on a
        // first population a group formed here would keep _buildGroupTable
        // from seeding the bake's groups at all.

        foreach (var schedule in schedules)
        {
            // Deterministic per-NPC offset: seed RNG from NpcId
            var offsetRng = new builtin.tools.RandomSource(schedule.NpcId.ToString());
            int offsetMinutes = offsetRng.GetInt(91) - 45; // ±45 minutes: 0-90 becomes -45 to 45
            DateTime npcTime = gameStartTime.AddMinutes(offsetMinutes);

            _advanceNpc(schedule.NpcId, npcTime, applyGroupVerbs: false);
            Trace(_dc, $"Warm-up step 1 of 2 for NPC {schedule.NpcId} at {npcTime:HH:mm} " +
                  $"(offset {offsetMinutes:+0;-0} min), now at location {schedule.CurrentLocationId}");
        }

        // Final advance to actual game start time so all transit phases align with spawn time
        foreach (var schedule in schedules)
        {
            _advanceNpc(schedule.NpcId, gameStartTime, applyGroupVerbs: false);
        }

        // TALE-SOCIAL Phase D3: re-attach a baked scenario's groups, trust edges
        // and post-365-day property snapshot onto the real cluster NPCs. Runs
        // AFTER both warmup advance loops on purpose: the warmup desynchronizes
        // schedule positions but its property drift is then overwritten by the
        // scenario's settled state, so the cluster starts with realistic
        // post-simulation social structure but staggered storylet timing.
        // Skipped silently if no scenarios are configured (selector returns null).
        if (applyScenario != null)
        {
            _buildGroupTable(clusterIndex, applyScenario(schedules));
        }
        else try
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
                        lock (_loSocial)
                        {
                            _bakeInfo[clusterIndex] = new SocialBakeInfo
                            {
                                Category = bakeRequest.CategoryName,
                                Index = bakeRequest.Index,
                                RealNpcCount = schedules.Count,
                                ScenarioNpcCount = scenario.NpcCount,
                                MatchedNpcCount = stats.MatchedNpcCount,
                                RelationshipsApplied = stats.RelationshipsApplied,
                                RelationshipsSkipped = stats.RelationshipsSkipped,
                                GroupsTouched = stats.GroupsTouched
                            };
                        }
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
        Trace(_dc, $"Populated cluster {clusterIndex} with {schedules.Count} NPCs " +
              $"in {stopwatch.ElapsedMilliseconds} ms. " +
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

        _populatedClusters[clusterIndex] = 0;
    }


    /// <summary>
    /// Depopulate a cluster: remove the schedules that can be regenerated.
    /// An NPC known to the player (IsKnownToPlayer) is kept as the same object
    /// and put into the cluster's skip mask, so the next PopulateCluster does
    /// not generate the same NpcId over it. Every NPC's social state is
    /// snapshotted first and restored on return. Clears the materialised and
    /// Tier-2 flags of all of the cluster's NPCs and drops its spatial model.
    /// Called by TaleModule once no fragment of the cluster has been loaded for
    /// a grace period (S0 W4, TaleClusterResidency), so its entities are gone.
    /// </summary>
    public void DepopulateCluster(int clusterIndex)
    {
        if (!_populatedClusters.ContainsKey(clusterIndex))
            return;

        var toRemove = new List<int>();
        int kept = 0;
        foreach (var kvp in _schedules)
        {
            var schedule = kvp.Value;
            if (schedule.ClusterIndex != clusterIndex) continue;

            _materializedNpcIds.TryRemove(kvp.Key, out _);
            _tier2NpcIds.TryRemove(kvp.Key, out _);

            _checkPlayerFlag(schedule);
            if (schedule.IsKnownToPlayer)
            {
                // Flagged NPCs normally are in the mask already; make sure.
                MarkPlayerContact(schedule);
                kept++;
            }
            else
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
            _schedules.TryRemove(npcId, out _);
        }
        Trace(_dc, $"Depopulated cluster {clusterIndex}: removed {toRemove.Count} schedules, " +
              $"kept {kept} known to the player. Total schedules now: {_schedules.Count}.");

        _populatedClusters.TryRemove(clusterIndex, out _);
        _spatialModels.TryRemove(clusterIndex, out _);
    }


    /// <summary>
    /// Check if a cluster is currently populated.
    /// </summary>
    public bool IsClusterPopulated(int clusterIndex) => _populatedClusters.ContainsKey(clusterIndex);


    /// <summary>
    /// Currently populated cluster indices, sorted (Phase E3 social tick
    /// iterates these round-robin).
    /// </summary>
    public List<int> GetPopulatedClusters()
    {
        var list = _populatedClusters.Keys.ToList();
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

            /*
             * S0 W4: a cluster that was depopulated before is a return visit. Its
             * snapshots restore every NPC's GroupId, so seeding the bake's groups
             * into an emptied table would list members that belong to no group.
             */
            if (social.Snapshots.Count > 0)
            {
                Trace(engine.Dc.TaleSocial, $"Cluster {clusterIndex}: snapshots exist (return visit); " +
                      $"not reseeding the group table from bake.");
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
    /// The cluster's social state, created on demand. Used by save-game
    /// restore and tests; regular runtime paths create states lazily anyway.
    /// </summary>
    public ClusterSocialState GetOrCreateSocialState(int clusterIndex)
    {
        lock (_loSocial)
        {
            if (!_socialStates.TryGetValue(clusterIndex, out var social))
            {
                social = new ClusterSocialState { ClusterIndex = clusterIndex };
                _socialStates[clusterIndex] = social;
            }
            return social;
        }
    }


    /// <summary>
    /// What the scenario bake did to the cluster at its last populate, or null
    /// when no scenario was applied.
    /// </summary>
    public SocialBakeInfo GetBakeInfo(int clusterIndex)
    {
        lock (_loSocial)
        {
            return _bakeInfo.GetValueOrDefault(clusterIndex);
        }
    }


    /// <summary>
    /// The shared social metrics of one populated cluster (S0 W6): trust from
    /// the schedules, types and mismatch from the group table, the bake result
    /// and the churn history. Logical thread: it reads the schedules' Trust
    /// dictionaries, which the social frame mutates there.
    /// </summary>
    public SocialMetrics ComputeSocialMetrics(int clusterIndex)
    {
        var npcs = GetNpcsInCluster(clusterIndex);
        Dictionary<int, RuntimeGroup> table = null;
        SocialMetricsHistory history = null;
        SocialBakeInfo bake;
        lock (_loSocial)
        {
            if (_socialStates.TryGetValue(clusterIndex, out var social))
            {
                table = new Dictionary<int, RuntimeGroup>(social.Groups);
                history = social.MetricsHistory;
            }
            bake = _bakeInfo.GetValueOrDefault(clusterIndex);
        }

        return SocialMetricsCalculator.Compute(npcs, TrustPair.FromSchedules(npcs), table, bake, history);
    }


    /// <summary>
    /// A game day ended for this cluster: close its metrics history day.
    /// Returns the day's sample, or null when this only started the history.
    /// </summary>
    public SocialDaySample? RollSocialMetricsDay(int clusterIndex, DateTime gameDate)
    {
        var npcs = GetNpcsInCluster(clusterIndex);
        var social = GetOrCreateSocialState(clusterIndex);
        lock (_loSocial)
        {
            return social.MetricsHistory.RollDay(gameDate, npcs);
        }
    }


    /// <summary>
    /// All social states, for save-game serialization (Phase E6).
    /// </summary>
    public List<ClusterSocialState> GetAllSocialStates()
    {
        lock (_loSocial)
        {
            return _socialStates.Values.ToList();
        }
    }


    /// <summary>
    /// Snapshot every schedule of every populated cluster into its cluster's
    /// social state. Called before saving so the CURRENT clusters' evolved
    /// social fabric lands in the save, not just previously-departed ones.
    /// </summary>
    public void SnapshotPopulatedClusters()
    {
        lock (_loSocial)
        {
            foreach (int clusterIndex in _populatedClusters.Keys)
            {
                if (!_socialStates.TryGetValue(clusterIndex, out var social))
                {
                    social = new ClusterSocialState { ClusterIndex = clusterIndex };
                    _socialStates[clusterIndex] = social;
                }
                foreach (var kvp in _schedules)
                {
                    if (kvp.Value.ClusterIndex == clusterIndex)
                        social.SnapshotNpc(kvp.Value);
                }
            }
        }
    }


    /// <summary>
    /// Install a social state loaded from a save, replacing any state derived
    /// so far for that cluster (Phase E6). The next PopulateCluster overlay
    /// then restores the saved snapshots onto regenerated schedules.
    /// </summary>
    public void ReplaceSocialState(ClusterSocialState social)
    {
        if (social == null) return;
        lock (_loSocial)
        {
            _socialStates[social.ClusterIndex] = social;
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
    /// TALE-SOCIAL Phase E4: execute a storylet group verb for an NPC.
    /// Runs under _loSocial because AdvanceNpc is called concurrently during
    /// spawn catch-up.
    /// </summary>
    private void _applyGroupVerb(NpcSchedule npc, string action, DateTime gameNow)
    {
        lock (_loSocial)
        {
            if (!_socialStates.TryGetValue(npc.ClusterIndex, out var social))
            {
                social = new ClusterSocialState { ClusterIndex = npc.ClusterIndex };
                _socialStates[npc.ClusterIndex] = social;
            }

            var byId = new Dictionary<int, NpcSchedule>();
            foreach (var kvp in _schedules)
            {
                if (kvp.Value.ClusterIndex == npc.ClusterIndex)
                    byId[kvp.Key] = kvp.Value;
            }

            GroupActions.Apply(action, npc, byId, social, GroupNameProvider,
                logger: null, day: 0, gameTime: gameNow);
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
            _addToSkipMask(schedule);
    }


    private void _addToSkipMask(NpcSchedule schedule)
    {
        _deviationSkipMasks.AddOrUpdate(schedule.ClusterIndex,
            _ => new HashSet<int> { schedule.NpcIndex },
            (_, mask) => mask.Contains(schedule.NpcIndex)
                ? mask
                : new HashSet<int>(mask) { schedule.NpcIndex });
    }


    /// <summary>
    /// TALE S0 W3: the one place that marks an NPC as touched by the player.
    /// Call it whenever Trust[-1] or a player_fact.* is written. It sets
    /// HasPlayerDeviation, so the save includes the NPC, and puts the NPC into
    /// its cluster's skip mask, so a repopulation cannot regenerate the same
    /// NpcId over it.
    /// </summary>
    public void MarkPlayerContact(NpcSchedule schedule)
    {
        if (schedule == null) return;
        schedule.HasPlayerDeviation = true;
        _addToSkipMask(schedule);
    }


    private int _unflaggedPlayerContacts;

    /// <summary>
    /// How many schedules were found known to the player but not flagged
    /// (each is warned about and flagged). Nonzero means a writer of Trust[-1]
    /// or player_fact.* bypassed MarkPlayerContact.
    /// </summary>
    public int UnflaggedPlayerContacts => _unflaggedPlayerContacts;

    private void _checkPlayerFlag(NpcSchedule schedule)
    {
        if (schedule.HasPlayerDeviation || !schedule.IsKnownToPlayer) return;
        Interlocked.Increment(ref _unflaggedPlayerContacts);
        Warning(_dc, $"NPC {schedule.NpcId} is known to the player (Trust[-1] or player_fact.*) "
                     + "but was never marked; marking it now. Its writer must call MarkPlayerContact.");
        MarkPlayerContact(schedule);
    }


    public NpcSchedule GetSchedule(int npcId)
    {
        return _schedules.GetValueOrDefault(npcId);
    }


    public IReadOnlyDictionary<int, NpcSchedule> AllSchedules => _schedules;


    /// <summary>
    /// Get all NPC schedules whose current position falls in the given fragment.
    /// Used by TaleSpawnOperator to decide which NPCs to materialize.
    /// Uses CurrentWorldPosition (the current location's entry position, or where
    /// the entity last stood when it was dematerialised), else HomePosition.
    /// This is where the NPC is, not which fragment owns its entity: despawn
    /// follows the owner (TaleEntityOwnership), never this bucket.
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
            _checkPlayerFlag(kvp.Value);
            if (kvp.Value.HasPlayerDeviation)
            {
                result.Add(kvp.Value);
            }
        }
        return result;
    }


    /// <summary>
    /// Get the deviation skip mask for a cluster. The returned set is a
    /// snapshot: never modify it.
    /// </summary>
    public HashSet<int> GetDeviationSkipMask(int clusterIndex)
    {
        return _deviationSkipMasks.GetValueOrDefault(clusterIndex);
    }

    #endregion


    #region Materialization Tracking

    public bool IsMaterialized(int npcId) => _materializedNpcIds.ContainsKey(npcId);

    public void SetMaterialized(int npcId) => _materializedNpcIds[npcId] = 0;

    public void SetDematerialized(int npcId) => _materializedNpcIds.TryRemove(npcId, out _);


    /// <summary>
    /// TALE S0 W3: an NPC's entity is gone (its owner fragment unloaded, or the
    /// wiper took it). Records where the entity last stood, when known, so the
    /// NPC is bucketed there, and clears the materialised flag. It never removes
    /// the schedule: Trust[-1] and player_fact.* survive.
    /// </summary>
    public void OnNpcDematerialized(int npcId, Vector3? worldPos)
    {
        if (_schedules.TryGetValue(npcId, out var schedule))
        {
            if (worldPos.HasValue)
                schedule.CurrentWorldPosition = worldPos.Value;
            _checkPlayerFlag(schedule);
        }
        _tier2NpcIds.TryRemove(npcId, out _);
        SetDematerialized(npcId);
    }


    /*
     * Tier 2 (an entity kept alive with a frozen strategy after despawn) is
     * unused since S0 W3 (KAR-447): the only path that entered it,
     * TaleSpawnOperator.TerminateCharacters, never ran, because TALE NPCs are
     * never despawned by distance. These members stay for a later stage to
     * remove or revive.
     */

    /// <summary>
    /// Check if an NPC is currently in Tier 2 (noticed but with frozen strategy).
    /// </summary>
    public bool IsTier2(int npcId) => _tier2NpcIds.ContainsKey(npcId);

    /// <summary>
    /// Demote an NPC to Tier 2: entity stays alive but strategy is frozen.
    /// </summary>
    public void SetTier2(int npcId) => _tier2NpcIds[npcId] = 0;

    /// <summary>
    /// Clear Tier 2 flag when promoting back to Tier 1 or destroying.
    /// </summary>
    public void ClearTier2(int npcId) => _tier2NpcIds.TryRemove(npcId, out _);

    /// <summary>
    /// True iff a schedule with this id is still tracked. Cheap guard for callers
    /// (e.g. quest steps) that hold an NpcId across time and need to know whether
    /// the NPC has been forgotten / re-randomized.
    /// </summary>
    public bool IsScheduleAlive(int npcId) => _schedules.ContainsKey(npcId);

    /// <summary>
    /// A 30-minute wall-clock window for <see cref="HasRecentConversation"/>.
    /// Unused since S0 W3 (KAR-447): the decay path that read it, which would
    /// have forgotten NPCs the player talked to, was dead code and is removed.
    /// </summary>
    public const double ConversationMemorySeconds = 1800.0;

    /// <summary>
    /// Stamp <see cref="NpcSchedule.LastConversationTime"/> to UtcNow.
    /// Called from the Tale*Behavior.OnAction methods every time the player
    /// initiates a conversation. No-op if NPC unknown.
    ///
    /// This used to double as an anti-spam cooldown; that gate has been removed.
    /// It is a memory signal only; nothing in the game decides on it since
    /// S0 W3 removed the decay path.
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
        => _advanceNpc(npcId, gameNow, applyGroupVerbs: true);


    private StoryletDefinition _advanceNpc(int npcId, DateTime gameNow, bool applyGroupVerbs)
    {
        if (!_schedules.TryGetValue(npcId, out var npc)) return null;

        // Apply postconditions from the completed storylet
        string previousStorylet = npc.CurrentStorylet;
        if (previousStorylet != null)
        {
            _deltasBuffer ??= new ConcurrentDictionary<string, float>();
            float previousDuration = (float)(npc.CurrentEnd - npc.CurrentStart).TotalMinutes;
            var prevDef = _library.GetById(previousStorylet);
            if (prevDef != null)
                _selector.ApplyPostconditions(npc, prevDef, previousDuration, _deltasBuffer);
            else
                _selector.ApplyPostconditions(npc, previousStorylet, previousDuration, _deltasBuffer);

            // TALE-SOCIAL Phase E4: fire the completed storylet's group verb
            // (form/join/leave), e.g. form_gang founding a criminal group.
            if (applyGroupVerbs && prevDef?.GroupAction != null)
                _applyGroupVerb(npc, prevDef.GroupAction, gameNow);
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

        // Determine duration (per NPC and step, independent of other NPCs)
        float durationMinutes = next.GetDuration(
            new builtin.tools.RandomSource(npc.NpcId.ToString() + "-dur-" + npc.ScheduleStep));

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

        // Update current world position from location: where this NPC stands
        // (PositionAt uses the same entry position), so fragment bucketing in
        // GetNpcsInFragment agrees with where the entity appears.
        if (_spatialModels.TryGetValue(npc.ClusterIndex, out spatialModel) && spatialModel != null)
        {
            var loc = spatialModel.GetLocation(destination);
            if (loc != null)
                npc.CurrentWorldPosition = loc.EntryPositionFor(npc.NpcId);
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
    /// Test seam: install a cluster's spatial model without PopulateCluster
    /// (which needs a generated ClusterDesc).
    /// </summary>
    internal void SetSpatialModelForTests(int clusterIndex, SpatialModel spatialModel)
    {
        _spatialModels[clusterIndex] = spatialModel;
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

        // TALE-SOCIAL Phase E5: groupmates share a hangout, so factions are
        // SEEN together. Applies only to social-venue storylets — work/home
        // schedules keep NPCs dispersed, no runaway convergence.
        if (npc.GroupId >= 0)
        {
            int hangout = GetGroupHangout(npc.ClusterIndex, npc.GroupId);
            if (hangout >= 0) return hangout;
        }

        int idx = npc.ScheduleStep % npc.SocialVenueIds.Count;
        return npc.SocialVenueIds[idx];
    }


    /// <summary>
    /// The group's hangout venue, computed lazily on first request: the
    /// modal first social venue among members (ties broken by lowest venue
    /// id) — deterministic, and by construction a real venue in the cluster.
    /// Cached on the RuntimeGroup; re-detection creating a new group yields
    /// a fresh computation.
    /// </summary>
    public int GetGroupHangout(int clusterIndex, int groupId)
    {
        lock (_loSocial)
        {
            if (!_socialStates.TryGetValue(clusterIndex, out var social)) return -1;
            if (!social.Groups.TryGetValue(groupId, out var group)) return -1;
            if (group.HangoutLocationId >= 0) return group.HangoutLocationId;

            var counts = new Dictionary<int, int>();
            foreach (int memberId in group.MemberIds)
            {
                if (!_schedules.TryGetValue(memberId, out var member)) continue;
                if (member.SocialVenueIds == null || member.SocialVenueIds.Count == 0) continue;
                int venue = member.SocialVenueIds[0];
                counts.TryGetValue(venue, out int c);
                counts[venue] = c + 1;
            }
            if (counts.Count == 0) return -1;

            int best = -1, bestCount = 0;
            foreach (var (venue, count) in counts)
            {
                if (count > bestCount || (count == bestCount && venue < best))
                {
                    best = venue;
                    bestCount = count;
                }
            }
            group.HangoutLocationId = best;
            Trace(engine.Dc.TaleSocial, $"Group {groupId} (cluster {clusterIndex}) hangout: " +
                  $"venue {best} ({bestCount}/{group.MemberIds.Count} members' first choice).");
            return best;
        }
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
            var streetLocations = spatialModel.StreetLocations;
            if (streetLocations.Count > 0)
            {
                // ScheduleStep was already incremented for this advance.
                var streetRng = new builtin.tools.RandomSource(npc.NpcId.ToString() + "-street-" + npc.ScheduleStep);
                int randomIdx = Math.Min(streetRng.GetInt(streetLocations.Count), streetLocations.Count - 1);
                return streetLocations[randomIdx].Id;
            }
        }
        return npc.CurrentLocationId;
    }

    #endregion
}
