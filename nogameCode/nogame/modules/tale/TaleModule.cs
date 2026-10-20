using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using builtin;
using builtin.modules.satnav.desc;
using engine;
using engine.narration;
using engine.navigation;
using engine.news;
using engine.tale;
using engine.world;
using nogame.modules.story;
using static engine.Logger;

namespace nogame.modules.tale;

/// <summary>
/// Module that bootstraps the TALE narrative system.
/// Loads the storylet library, creates the TaleManager singleton,
/// and hooks into cluster lifecycle for population management.
/// </summary>
public class TaleModule : AModule
{
    private static readonly engine.Dc _dcSocial = engine.Dc.TaleSocial;

    private TaleManager _taleManager;
    public TaleManager TaleManager => _taleManager;
    private ClusterList _clusterList;
    private PipeNetwork _pedestrianNetwork;
    private PipeController _pipeController;

    /*
     * TALE-SOCIAL Phase E3: runtime social evolution. Every
     * SocialTickGameMinutes of game time, one populated cluster (round-robin)
     * gets an encounter tick; every RedetectGameHours per cluster the
     * community structure is re-detected from the evolved trust graph.
     * All of it runs on the logical-frame thread.
     */
    private readonly RuntimeSocialEvolver _socialEvolver = new();
    private DateTime _lastSocialTickGameTime = DateTime.MinValue;
    private DateTime _lastMetricsDate = DateTime.MinValue;
    private static readonly engine.Dc _dcMetrics = engine.Dc.TaleMetrics;
    private static readonly engine.Dc _dcTaleManager = engine.Dc.TaleManager;
    private readonly Dictionary<int, DateTime> _lastRedetectGameTime = new();
    private int _socialTickRoundRobin;

    public double SocialTickGameMinutes = 10.0;
    public double RedetectGameHours = 2.0;

    /*
     * S0 W4: a cluster no loaded fragment has touched for this long (real
     * time) is depopulated. Longer than the loader's KeepFragmentMinTime
     * (25 s), so the purged fragments' entities are gone and driving along a
     * cluster edge does not depopulate and repopulate it back and forth.
     */
    public double DepopulateGraceSeconds = 60.0;
    private DateTime _lastResidencyCheck = DateTime.MinValue;

    /*
     * S0 W8: the snapshot hand-over to the ImGui TALE panel.
     */
    private readonly TaleDebugState _debugState = new();
    private DateTime _lastDebugSnapshot = DateTime.MinValue;

    /*
     * S0 W5: the background tick advances the schedules no entity runs, a
     * slice per logical frame. Stats are summed and traced once per game hour.
     */
    // 16 advances per frame: at 4095 NPCs, p99.9 0.07 ms per slice; 64 gave
    // longer bursts with no gain (BackgroundTickTests perf, S0 W5).
    public int AbstractTickScanBudget = 512;
    public int AbstractTickAdvanceBudget = 16;
    private readonly AbstractTickStats _abstractTickStats = new();
    private DateTime _abstractTickStatsHour = DateTime.MinValue;

    /// <summary>
    /// Get the pedestrian pipe controller for NPC movement.
    /// </summary>
    public PipeController GetPipeController() => _pipeController;


    private DateTime? _tryGetGameNow()
    {
        try { return M<modules.daynite.Controller>().GameNow; }
        catch { return null; }
    }


    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<modules.daynite.Controller>(),
        new SharedModule<Saver>(),
        // Player position for the density cross-check (S0 W9); the save hook
        // already read it from here.
        new SharedModule<nogame.modules.AutoSave>()
    };


    // TXWTODO: Let's investigate if it would make sense to make TaleModule a WorldOperator that triggers after clusters can be made
    private void _onClusterCompleted(Event ev)
    {
        Trace($"TALE: _onClusterCompleted fired! Type='{ev.Type}' Code='{ev.Code}'");
        if (_taleManager == null)
        {
            Warning("TALE: _onClusterCompleted called but _taleManager is null!");
            return;
        }

        // Find the ClusterDesc by name (event Code carries the cluster name)
        ClusterDesc clusterDesc = null;
        foreach (var cd in _clusterList.GetClusterList())
        {
            if (cd.Name == ev.Code)
            {
                clusterDesc = cd;
                break;
            }
        }

        if (clusterDesc == null)
        {
            Warning($"TALE: ClusterCompletedEvent for unknown cluster '{ev.Code}'.");
            return;
        }

        // Find NavCluster for this cluster (if NavMap available)
        builtin.modules.satnav.desc.NavCluster navClusterForCluster = null;
        try
        {
            var navMap = I.Get<NavMap>();
            if (navMap?.TopCluster?.Content != null)
            {
                navClusterForCluster = navMap.TopCluster.Content.Clusters.FirstOrDefault(nc => nc.Id == clusterDesc.IdString);
            }
        }
        catch (Exception e)
        {
            Trace($"TALE: Failed to find NavCluster: {e.Message}");
        }

        // Extract spatial model for location assignment
        // Pass NavCluster so street_segment NPCs get positioned on pedestrian NavLanes
        var spatialModel = SpatialModel.ExtractFrom(clusterDesc, navClusterForCluster);

        // Validate that all location entry points are reachable via NavMap (Phase 1 stuck NPC fix)
        try
        {
            if (navClusterForCluster != null)
            {
                spatialModel.ValidateReachability(navClusterForCluster, clusterDesc);
            }
        }
        catch (Exception e)
        {
            Trace($"TALE: Reachability validation failed: {e.Message}");
        }

        _taleManager.PopulateCluster(clusterDesc, spatialModel, _tryGetGameNow());

        Trace($"TALE: Populated cluster '{ev.Code}' (index {clusterDesc.Index}). " +
              $"Spatial: {spatialModel.BuildingCount} buildings, {spatialModel.ShopCount} shops, " +
              $"{spatialModel.StreetPointCount} street points.");
    }


    private void _initializePipeSystem()
    {
        try
        {
            // Get NavMap if available
            var navMap = I.Get<NavMap>();
            if (navMap == null)
            {
                Trace("TALE: NavMap not available, pipe system disabled for this session");
                return;
            }

            // Create pedestrian pipe network from NavMap
            // For Phase B5 (rest state): 1:1 NavLane-to-Pipe mapping
            _pedestrianNetwork = new PipeNetwork
            {
                SupportedType = TransportationType.Pedestrian
            };

            int pipeId = 0;
            if (navMap.AllLanes != null)
            {
                foreach (var lane in navMap.AllLanes)
                {
                    // Only add pedestrian-accessible lanes
                    if (!lane.AllowedTypes.HasFlag(TransportationType.Pedestrian))
                        continue;

                    var pipe = new Pipe
                    {
                        Id = pipeId++,
                        NavLanes = new List<builtin.modules.satnav.desc.NavLane> { lane },
                        StartPosition = lane.Start.Position,
                        EndPosition = lane.End.Position,
                        SupportedType = TransportationType.Pedestrian,
                        Length = lane.Length
                    };

                    _pedestrianNetwork.Pipes.Add(pipe);
                }

                // Create controller
                _pipeController = new PipeController(_pedestrianNetwork);
                I.Register<PipeController>(() => _pipeController);

                Trace($"TALE: Pipe system initialized with {_pedestrianNetwork.Pipes.Count} pedestrian pipes");
            }
        }
        catch (Exception e)
        {
            Warning($"TALE: Failed to initialize pipe system: {e.Message}");
        }
    }


    private void _onBeforeSaveGame(object sender, object args)
    {
        if (_taleManager == null) return;

        var autoSave = M<nogame.modules.AutoSave>();

        var deviated = _taleManager.GetAllDeviatedNpcs();
        if (deviated.Count > 0)
        {
            var jArray = new JsonArray();
            foreach (var npc in deviated)
            {
                jArray.Add(SerializeNpcSchedule(npc));
            }

            if (autoSave != null && autoSave.GameState != null && autoSave.GameState.TaleDeviations != null)
            {
                autoSave.GameState.TaleDeviations = jArray.ToJsonString();
            }

            Trace($"TALE: Saved {deviated.Count} deviated NPCs.");
        }

        // TALE-SOCIAL Phase E6: persist the per-cluster social fabric. Snapshot
        // the currently populated clusters first so the save carries their live
        // evolved state, not just previously-departed clusters'.
        _taleManager.SnapshotPopulatedClusters();
        var socialStates = _taleManager.GetAllSocialStates();
        if (socialStates.Count > 0 && autoSave?.GameState != null)
        {
            var jStates = new JsonArray();
            foreach (var social in socialStates.OrderBy(s => s.ClusterIndex))
            {
                jStates.Add(social.ToJson());
            }
            autoSave.GameState.TaleSocialState = jStates.ToJsonString();
            Trace(_dcSocial, $"Saved social state for {socialStates.Count} clusters " +
                  $"({socialStates.Sum(s => s.Groups.Count)} groups, " +
                  $"{socialStates.Sum(s => s.Snapshots.Count)} NPC snapshots).");
        }
    }


    private void _onAfterLoadGame(object sender, object objGameState)
    {
        if (_taleManager == null) return;
        if (objGameState is not GameState gs) return;

        if (!string.IsNullOrEmpty(gs.TaleDeviations))
        {
            try
            {
                var jArray = JsonNode.Parse(gs.TaleDeviations)?.AsArray();
                if (jArray != null)
                {
                    int count = 0;
                    foreach (var jNode in jArray)
                    {
                        var schedule = DeserializeNpcSchedule(jNode);
                        if (schedule != null)
                        {
                            schedule.HasPlayerDeviation = true;
                            _taleManager.RegisterNpc(schedule);
                            count++;
                        }
                    }

                    Trace($"TALE: Loaded {count} deviated NPCs from save.");
                }
            }
            catch (Exception e)
            {
                Warning($"TALE: Failed to load deviated NPCs: {e.Message}");
            }
        }

        // TALE-SOCIAL Phase E6: restore per-cluster social states. The next
        // PopulateCluster of each cluster overlays the contained snapshots
        // onto its regenerated schedules (snapshot wins over the baked
        // scenario). Old saves have an empty string here — skipped.
        if (!string.IsNullOrEmpty(gs.TaleSocialState))
        {
            try
            {
                var jStates = JsonNode.Parse(gs.TaleSocialState)?.AsArray();
                if (jStates != null)
                {
                    int count = 0;
                    foreach (var jNode in jStates)
                    {
                        var social = ClusterSocialState.FromJson(jNode);
                        if (social != null)
                        {
                            _taleManager.ReplaceSocialState(social);
                            count++;
                        }
                    }
                    Trace(_dcSocial, $"Loaded social state for {count} clusters from save.");
                }
            }
            catch (Exception e)
            {
                Warning($"TALE: Failed to load social state: {e.Message}");
            }
        }
    }




    protected override void OnModuleActivate()
    {
        try
        {
            // Load the storylet library through the asset system rather than off
            // the filesystem: on Android the model tree lives inside the APK and
            // there is no models/tale directory to enumerate. The files ship
            // because /resources/list names them; that same list tells us which
            // tags to open, so declaration and loading cannot drift apart.
            var storyletTags = StoryletLibrary.CollectDeclaredTags();
            if (storyletTags.Count == 0)
            {
                Error($"TALE: no storylet resources declared (expected entries of type "
                      + $"'{StoryletLibrary.ResourceType}' in /resources/list). TALE stays disabled.");
                return;
            }

            var library = new StoryletLibrary();
            int nLoaded = library.LoadFromAssets(storyletTags, out var failedTags);
            if (failedTags.Count > 0)
                Warning($"TALE: {failedTags.Count} storylet resource(s) could not be read: "
                        + string.Join(", ", failedTags));

            if (nLoaded == 0)
            {
                Error($"TALE: none of the {storyletTags.Count} declared storylet resources "
                      + "could be loaded. TALE stays disabled.");
                return;
            }

            Trace($"TALE: Loaded {library.All.Count} storylets from {nLoaded} resource(s) "
                  + $"({library.GetCandidates("worker").Count} candidates for role 'worker').");

            // Create and register Role and Interaction registries
            TaleRegistryLoader.Register();

            // TALE-SOCIAL Phase D2: register the runtime scenario library + selector.
            // These are lazy singletons; the library only touches disk when something
            // actually asks for a scenario, and falls through to in-process baking
            // if the file is missing or unparsable. The asset implementation has
            // already populated AvailableScenarios at this point because
            // InterpretConfig ran during engine setup.
            I.Register<engine.tale.bake.ScenarioLibrary>(() => new engine.tale.bake.ScenarioLibrary());
            I.Register<engine.tale.bake.ScenarioSelector>(() => new engine.tale.bake.ScenarioSelector());
            // TALE-SOCIAL Phase D3: applicator that re-attaches scenario state
            // onto real cluster NPCs. Stateless; called from TaleManager.PopulateCluster.
            I.Register<engine.tale.bake.ScenarioApplicator>(() => new engine.tale.bake.ScenarioApplicator());
            Trace("TALE: ScenarioLibrary, ScenarioSelector and ScenarioApplicator registered.");

            // Create and register TaleManager
            _taleManager = new TaleManager();
            _taleManager.Initialize(library);

            // TALE-SOCIAL Phase E2: deterministic faction naming for the
            // runtime group table (game-layer flavor, engine stays content-free).
            _taleManager.GroupNameProvider = GroupNameGenerator.Generate;

            I.Register<TaleManager>(() => _taleManager);
            I.Register<TaleDebugState>(() => _debugState);
            Trace("TALE: TaleManager registered.");

            // Register narration bindings for NPC conversations
            try
            {
                TaleNarrationBindings.Register();

                // Phase C4: Subscribe to ScriptEndedEvent for trust increment
                Subscribe(engine.narration.ScriptEndedEvent.EVENT_TYPE, _onScriptEnded);

                // Phase C4: Register tale.npc.remember event handler for memory facts
                var narrationMgr = I.Get<NarrationManager>();
                if (narrationMgr != null)
                {
                    narrationMgr.RegisterEventHandler("tale.npc.remember", async desc =>
                    {
                        var currentSchedule = TaleNarrationBindings.GetCurrentSchedule();
                        if (currentSchedule != null && desc.Params.TryGetValue("fact", out var factObj))
                        {
                            PlayerConversation.Remember(currentSchedule, factObj.ToString());
                            _taleManager.MarkPlayerContact(currentSchedule);
                            Trace($"TALE: NPC {currentSchedule.NpcId} remembered fact '{factObj}'");
                        }
                        await Task.CompletedTask;
                    });
                }
            }
            catch (Exception e)
            {
                Warning($"TALE: Failed to register narration bindings: {e.Message}");
            }

            // Load roles and interaction types from config
            TaleRegistryLoader.LoadFromConfig(I.Get<engine.casette.Loader>());

            // Get cluster list for lifecycle events
            _clusterList = I.Get<ClusterList>();

            // Subscribe to cluster lifecycle
            Subscribe(ClusterCompletedEvent.EVENT_TYPE, _onClusterCompleted);

            // Catch up on clusters that completed before we subscribed
            var navMap = I.Get<NavMap>();
            foreach (var cd in _clusterList.GetClusterList())
            {
                if (cd.IsCompleted)
                {
                    Trace($"TALE: Catching up on already-completed cluster '{cd.Name}'.");
                    builtin.modules.satnav.desc.NavCluster navClusterForCluster = null;
                    if (navMap?.TopCluster?.Content != null)
                    {
                        navClusterForCluster = navMap.TopCluster.Content.Clusters.FirstOrDefault(nc => nc.Id == cd.IdString);
                    }
                    var spatialModel = SpatialModel.ExtractFrom(cd, navClusterForCluster);
                    _taleManager.PopulateCluster(cd, spatialModel, _tryGetGameNow());
                }
            }

            // Initialize pipe system for NPC movement (Phase B5)
            _initializePipeSystem();

            // Subscribe to save/load for deviation persistence
            M<Saver>().OnBeforeSaveGame += _onBeforeSaveGame;
            M<Saver>().OnAfterLoadGame += _onAfterLoadGame;

            // TALE-SOCIAL Phase E3: start the runtime social evolution tick.
            _engine.OnLogicalFrame += _onSocialFrame;
        }
        catch (Exception e)
        {
            Warning($"TALE: Failed to initialize: {e}");
        }
    }


    protected override void OnModuleDeactivate()
    {
        _engine.OnLogicalFrame -= _onSocialFrame;
        M<Saver>().OnBeforeSaveGame -= _onBeforeSaveGame;
        M<Saver>().OnAfterLoadGame -= _onAfterLoadGame;
    }


    /// <summary>
    /// TALE-SOCIAL Phase E3: periodic social evolution. Cheap early-outs on
    /// every logical frame; real work only once per SocialTickGameMinutes of
    /// game time (12.5 real seconds at the default 30-min game day), for one
    /// cluster at a time.
    /// </summary>
    /**
     * S0 W6: when the game date changes, close the metrics history day of every
     * populated cluster and, with debug.category.talemetrics on, log its metrics.
     */
    private void _rollSocialMetricsDay(DateTime gameNow)
    {
        if (gameNow.Date == _lastMetricsDate) return;
        _lastMetricsDate = gameNow.Date;

        foreach (int clusterIndex in _taleManager.GetPopulatedClusters())
        {
            var sample = _taleManager.RollSocialMetricsDay(clusterIndex, gameNow);
            if (sample != null && Is(_dcMetrics))
            {
                var day = sample.Value;
                string metrics = _taleManager.ComputeSocialMetrics(clusterIndex).ToLogLine();
                Trace(_dcMetrics, $"Cluster {clusterIndex} day {day.Date:yyyy-MM-dd}: " +
                      $"{day.MembershipChanges} membership changes, " +
                      $"+{day.GroupsCreated}/-{day.GroupsDissolved} groups. {metrics}");
            }
        }
    }


    /**
     * S0 W4: depopulate the clusters that have had no resident fragment for
     * the grace period. Once a second; real time, like the loader's own
     * fragment lifetimes.
     */
    private void _depopulateLeftClusters()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastResidencyCheck).TotalSeconds < 1.0) return;
        _lastResidencyCheck = now;

        if (Is(_dcMetrics)) _logPlayerClusterChange();

        var due = _taleManager.Residency.DueForDepopulate(
            _taleManager.GetPopulatedClusters(), now, TimeSpan.FromSeconds(DepopulateGraceSeconds));
        foreach (int clusterIndex in due)
        {
            Trace($"TALE: no fragment of cluster {clusterIndex} loaded for {DepopulateGraceSeconds:F0} s; depopulating.");
            _taleManager.DepopulateCluster(clusterIndex);
            _taleManager.Residency.Depopulated(clusterIndex);
            _lastRedetectGameTime.Remove(clusterIndex);
        }
    }


    /**
     * S0 W5: one slice of the background tick, and once per game hour a trace
     * of what it did (TaleManager debug category).
     */
    private void _advanceAbstractSchedules(DateTime gameNow)
    {
        _abstractTickStats.Add(_taleManager.AdvanceAbstractSchedules(
            gameNow, AbstractTickScanBudget, AbstractTickAdvanceBudget));

        var hour = new DateTime(gameNow.Year, gameNow.Month, gameNow.Day, gameNow.Hour, 0, 0);
        if (hour == _abstractTickStatsHour) return;
        if (_abstractTickStatsHour != DateTime.MinValue)
        {
            Trace(_dcTaleManager, $"Background tick {_abstractTickStatsHour:yyyy-MM-dd HH}:00: " +
                  $"{_abstractTickStats} over {_taleManager.GetPopulatedClusters().Count} populated clusters.");
        }
        _abstractTickStatsHour = hour;
        _abstractTickStats.Reset();

        if (Is(_dcMetrics)) _logDensityNearPlayer(gameNow);
    }


    /**
     * S0 W9 cross-check, once per game hour with debug.category.talemetrics:
     * NPCs within 100 m of the player by schedule (the counter the headless
     * --tale-arrival measurement uses) against the TALE entities actually there.
     */
    private void _logDensityNearPlayer(DateTime gameNow)
    {
        const float radius = 100f;
        var playerPos = M<nogame.modules.AutoSave>().GameState.PlayerPosition;

        var bySchedule = new NpcDensity(0, 0, 0);
        foreach (int clusterIndex in _taleManager.GetPopulatedClusters())
        {
            var d = NpcDensityCounter.CountNear(_taleManager.GetNpcsInCluster(clusterIndex),
                _taleManager.GetSpatialModel(clusterIndex), playerPos, radius, gameNow);
            bySchedule = new NpcDensity(bySchedule.Street + d.Street, bySchedule.Venue + d.Venue,
                bySchedule.HomeOrWork + d.HomeOrWork);
        }

        int byEntity = 0;
        foreach (var e in _engine.GetEcsWorld().GetEntities()
                     .With<engine.tale.components.TaleNpcId>()
                     .With<engine.joyce.components.Transform3ToWorld>().AsEnumerable())
        {
            var p = e.Get<engine.joyce.components.Transform3ToWorld>().Matrix.Translation;
            float dx = p.X - playerPos.X, dz = p.Z - playerPos.Z;
            if (dx * dx + dz * dz <= radius * radius) byEntity++;
        }

        Trace(_dcMetrics, $"Density within {radius:F0} m of the player at {gameNow:HH:mm}: " +
                          $"schedules {bySchedule}, entities {byEntity}.");
    }


    /**
     * S0 W9: log a cluster's arrival metrics when the player enters it.
     */
    private int _playerClusterIndex = -1;

    private void _logPlayerClusterChange()
    {
        var playerPos = M<nogame.modules.AutoSave>().GameState.PlayerPosition;
        int found = -1;
        foreach (var cd in _clusterList.GetClusterList())
        {
            var aabb = cd.AABB;
            if (playerPos.X >= aabb.AA.X && playerPos.X <= aabb.BB.X
                && playerPos.Z >= aabb.AA.Z && playerPos.Z <= aabb.BB.Z)
            {
                found = cd.Index;
                break;
            }
        }
        if (found == _playerClusterIndex) return;
        _playerClusterIndex = found;
        if (found >= 0 && _taleManager.IsClusterPopulated(found))
        {
            Trace(_dcMetrics, $"Player entered cluster {found}: " +
                              $"{_taleManager.ComputeSocialMetrics(found).ToLogLine()}");
        }
    }


    /**
     * S0 W8: while the editor is open, once a real second, copy one cluster
     * into a snapshot for the ImGui TALE panel, which renders on the render
     * thread and must not read TaleManager itself.
     */
    private void _buildDebugSnapshot(DateTime gameNow)
    {
        if (!(bool)Props.Get("engine.editor.isOpen", false)) return;
        var now = DateTime.UtcNow;
        if ((now - _lastDebugSnapshot).TotalSeconds < 1.0) return;
        _lastDebugSnapshot = now;

        Vector3 playerPos = default;
        if (_engine.Player.TryGet(out var ePlayer)
            && ePlayer.IsAlive && ePlayer.Has<engine.joyce.components.Transform3ToWorld>())
        {
            playerPos = ePlayer.Get<engine.joyce.components.Transform3ToWorld>().Matrix.Translation;
        }

        var populated = _taleManager.GetPopulatedClusters();
        int playersCluster = _clusterList?.GetClusterAt(playerPos)?.Index ?? -1;
        int clusterIndex = _debugState.RequestedClusterIndex;
        if (clusterIndex < 0 || !populated.Contains(clusterIndex))
        {
            clusterIndex = populated.Contains(playersCluster) ? playersCluster
                : populated.Count > 0 ? populated[0] : -1;
        }

        int npcId = _debugState.RequestedNpcId;
        if (npcId < 0)
        {
            var eSelected = _debugState.SelectedEntity;
            if (eSelected != default && eSelected.IsAlive && eSelected.Has<engine.tale.components.TaleNpcId>())
                npcId = eSelected.Get<engine.tale.components.TaleNpcId>().NpcId;
        }

        try
        {
            _debugState.Latest = TaleDebugSnapshotBuilder.Build(
                _taleManager, clusterIndex, gameNow, playerPos, npcId, clusterIndex == playersCluster);
        }
        catch (InvalidOperationException)
        {
            // A spawn catch-up thread changed a schedule's dictionaries while
            // we copied them. Debug view: try again next second.
        }
    }


    private void _onSocialFrame(object sender, float dt)
    {
        if (_taleManager == null) return;
        _depopulateLeftClusters();

        var maybeNow = _tryGetGameNow();
        if (maybeNow == null) return;
        DateTime gameNow = maybeNow.Value;

        _advanceAbstractSchedules(gameNow);
        _rollSocialMetricsDay(gameNow);
        _buildDebugSnapshot(gameNow);

        if (_lastSocialTickGameTime == DateTime.MinValue)
        {
            _lastSocialTickGameTime = gameNow;
            return;
        }
        if ((gameNow - _lastSocialTickGameTime).TotalMinutes < SocialTickGameMinutes) return;
        _lastSocialTickGameTime = gameNow;

        var clusters = _taleManager.GetPopulatedClusters();
        if (clusters.Count == 0) return;
        int clusterIndex = clusters[_socialTickRoundRobin++ % clusters.Count];

        // Encounters must see where the NPCs are now, not where the slice left them.
        _abstractTickStats.Add(_taleManager.CatchUpCluster(clusterIndex, gameNow));

        var npcs = _taleManager.GetNpcsInCluster(clusterIndex);
        if (npcs.Count == 0) return;
        var spatial = _taleManager.GetSpatialModel(clusterIndex);

        var stats = _socialEvolver.TickEncounters(clusterIndex, npcs, spatial, gameNow);
        if (stats.Encounters > 0 || stats.PairsSampled > 0)
        {
            Trace(_dcSocial, $"Cluster {clusterIndex} tick @{gameNow:HH:mm}: " +
                  $"{stats.NpcsPresent} present, {stats.LocationsWithCompany} shared locations, " +
                  $"{stats.PairsSampled} pairs sampled, {stats.Encounters} encounters.");
        }

        _lastRedetectGameTime.TryGetValue(clusterIndex, out var lastRedetect);
        if ((gameNow - lastRedetect).TotalHours < RedetectGameHours) return;
        _lastRedetectGameTime[clusterIndex] = gameNow;

        var rstats = _taleManager.RedetectGroups(clusterIndex, _socialEvolver, GroupNameGenerator.Generate);
        if (rstats == null) return;
        string groupSummary = string.Join(", ", _taleManager.CopyGroupTable(clusterIndex).Values.Select(
            g => $"\"{g.Name}\" [{g.Type}] x{g.MemberIds.Count}"));
        Trace(_dcSocial, $"Cluster {clusterIndex} re-detect @{gameNow:HH:mm}: " +
              $"{rstats.Communities} communities ({rstats.KeptIdentity} kept identity, " +
              $"{rstats.Created} new, {rstats.Dissolved} dissolved). Groups: {groupSummary}");
    }


    #region NpcSchedule Serialization

    private static JsonObject SerializeNpcSchedule(NpcSchedule npc)
    {
        var jo = new JsonObject
        {
            ["npcId"] = npc.NpcId,
            ["seed"] = npc.Seed,
            ["role"] = npc.Role,
            ["clusterIndex"] = npc.ClusterIndex,
            ["npcIndex"] = npc.NpcIndex,
            ["currentLocationId"] = npc.CurrentLocationId,
            ["currentStorylet"] = npc.CurrentStorylet,
            ["scheduleStep"] = npc.ScheduleStep,
            ["homeLocationId"] = npc.HomeLocationId,
            ["workplaceLocationId"] = npc.WorkplaceLocationId,
            ["groupId"] = npc.GroupId,
        };

        // Activity timing
        jo["currentStartTicks"] = npc.CurrentStart.Ticks;
        jo["currentEndTicks"] = npc.CurrentEnd.Ticks;

        // Transit state (critical for resuming walks on load)
        jo["isInTransit"] = npc.IsInTransit;
        if (npc.IsInTransit)
        {
            jo["transitFromLocationId"] = npc.TransitFromLocationId;
            jo["transitToLocationId"] = npc.TransitToLocationId;
            jo["transitStartTicks"] = npc.TransitStart.Ticks;
            jo["transitEndTicks"] = npc.TransitEnd.Ticks;
        }

        // Home/workplace positions
        jo["homePositionX"] = npc.HomePosition.X;
        jo["homePositionY"] = npc.HomePosition.Y;
        jo["homePositionZ"] = npc.HomePosition.Z;
        jo["workplacePositionX"] = npc.WorkplacePosition.X;
        jo["workplacePositionY"] = npc.WorkplacePosition.Y;
        jo["workplacePositionZ"] = npc.WorkplacePosition.Z;

        // Current world position (for fragment-accurate spawning)
        if (npc.CurrentWorldPosition != System.Numerics.Vector3.Zero)
        {
            jo["currentWorldPositionX"] = npc.CurrentWorldPosition.X;
            jo["currentWorldPositionY"] = npc.CurrentWorldPosition.Y;
            jo["currentWorldPositionZ"] = npc.CurrentWorldPosition.Z;
        }

        // Properties
        var jProps = new JsonObject();
        foreach (var kvp in npc.Properties)
        {
            jProps[kvp.Key] = kvp.Value;
        }
        jo["properties"] = jProps;

        // Trust
        var jTrust = new JsonObject();
        foreach (var kvp in npc.Trust)
        {
            jTrust[kvp.Key.ToString()] = kvp.Value;
        }
        jo["trust"] = jTrust;

        // Social venue IDs
        if (npc.SocialVenueIds != null)
        {
            var jVenues = new JsonArray();
            foreach (var v in npc.SocialVenueIds)
                jVenues.Add(v);
            jo["socialVenueIds"] = jVenues;
        }

        return jo;
    }


    private static NpcSchedule DeserializeNpcSchedule(JsonNode jNode)
    {
        if (jNode is not JsonObject jo) return null;

        var npc = new NpcSchedule
        {
            NpcId = jo["npcId"]?.GetValue<int>() ?? 0,
            Seed = jo["seed"]?.GetValue<int>() ?? 0,
            Role = jo["role"]?.GetValue<string>() ?? "worker",
            ClusterIndex = jo["clusterIndex"]?.GetValue<int>() ?? 0,
            NpcIndex = jo["npcIndex"]?.GetValue<int>() ?? 0,
            CurrentLocationId = jo["currentLocationId"]?.GetValue<int>() ?? 0,
            CurrentStorylet = jo["currentStorylet"]?.GetValue<string>(),
            ScheduleStep = jo["scheduleStep"]?.GetValue<int>() ?? 0,
            HomeLocationId = jo["homeLocationId"]?.GetValue<int>() ?? 0,
            WorkplaceLocationId = jo["workplaceLocationId"]?.GetValue<int>() ?? 0,
            GroupId = jo["groupId"]?.GetValue<int>() ?? -1,
            HasPlayerDeviation = true,
            // Treat load as a touch so saved deviated NPCs aren't immediately
            // aged out by the dematerialization decay path on first traversal.
            LastConversationTime = DateTime.UtcNow,
        };

        // Activity timing
        npc.CurrentStart = new DateTime(jo["currentStartTicks"]?.GetValue<long>() ?? 0);
        npc.CurrentEnd = new DateTime(jo["currentEndTicks"]?.GetValue<long>() ?? 0);

        // Transit state (restore for walking NPCs)
        npc.IsInTransit = jo["isInTransit"]?.GetValue<bool>() ?? false;
        if (npc.IsInTransit)
        {
            npc.TransitFromLocationId = jo["transitFromLocationId"]?.GetValue<int>() ?? 0;
            npc.TransitToLocationId = jo["transitToLocationId"]?.GetValue<int>() ?? 0;
            npc.TransitStart = new DateTime(jo["transitStartTicks"]?.GetValue<long>() ?? 0);
            npc.TransitEnd = new DateTime(jo["transitEndTicks"]?.GetValue<long>() ?? 0);
        }

        // Positions
        npc.HomePosition = new System.Numerics.Vector3(
            jo["homePositionX"]?.GetValue<float>() ?? 0f,
            jo["homePositionY"]?.GetValue<float>() ?? 0f,
            jo["homePositionZ"]?.GetValue<float>() ?? 0f);
        npc.WorkplacePosition = new System.Numerics.Vector3(
            jo["workplacePositionX"]?.GetValue<float>() ?? 0f,
            jo["workplacePositionY"]?.GetValue<float>() ?? 0f,
            jo["workplacePositionZ"]?.GetValue<float>() ?? 0f);
        npc.CurrentWorldPosition = new System.Numerics.Vector3(
            jo["currentWorldPositionX"]?.GetValue<float>() ?? 0f,
            jo["currentWorldPositionY"]?.GetValue<float>() ?? 0f,
            jo["currentWorldPositionZ"]?.GetValue<float>() ?? 0f);

        // Properties
        npc.Properties = new System.Collections.Generic.Dictionary<string, float>();
        if (jo["properties"] is JsonObject jProps)
        {
            foreach (var kvp in jProps)
            {
                npc.Properties[kvp.Key] = kvp.Value?.GetValue<float>() ?? 0.5f;
            }
        }

        // Ensure all expected properties are initialized (backfill any missing from older saves)
        StoryletSelector.EnsurePropertiesInitialized(npc);

        // Trust
        npc.Trust = new System.Collections.Generic.Dictionary<int, float>();
        if (jo["trust"] is JsonObject jTrust)
        {
            foreach (var kvp in jTrust)
            {
                if (int.TryParse(kvp.Key, out int trustId))
                {
                    npc.Trust[trustId] = kvp.Value?.GetValue<float>() ?? 0f;
                }
            }
        }

        // Social venues
        npc.SocialVenueIds = new System.Collections.Generic.List<int>();
        if (jo["socialVenueIds"] is JsonArray jVenues)
        {
            foreach (var v in jVenues)
            {
                npc.SocialVenueIds.Add(v?.GetValue<int>() ?? 0);
            }
        }

        return npc;
    }

    #endregion


    /// <summary>
    /// Handle ScriptEndedEvent: increment Trust[-1] for the current NPC.
    /// Phase C4: Trust tracking via conversation count.
    /// </summary>
    private void _onScriptEnded(engine.news.Event ev)
    {
        try
        {
            var currentSchedule = TaleNarrationBindings.GetCurrentSchedule();
            if (currentSchedule != null)
            {
                PlayerConversation.OnEnded(currentSchedule);
                _taleManager.MarkPlayerContact(currentSchedule);
                Trace($"TALE: Trust incremented for NPC {currentSchedule.NpcId} (now {currentSchedule.Trust[-1]:F2})");
            }
            TaleNarrationBindings.ClearNpcProps();
        }
        catch (Exception e)
        {
            Error($"TALE: Exception in _onScriptEnded: {e.Message}");
        }
    }
}
