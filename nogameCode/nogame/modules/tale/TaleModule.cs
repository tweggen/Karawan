using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private readonly Dictionary<int, DateTime> _lastRedetectGameTime = new();
    private int _socialTickRoundRobin;

    public double SocialTickGameMinutes = 10.0;
    public double RedetectGameHours = 2.0;

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
        new SharedModule<Saver>()
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
            // Load storylet library from models/tale/
            string resourcePath = GlobalSettings.Get("Engine.ResourcePath") ?? "./models/";
            string talePath = Path.Combine(resourcePath, "tale");

            if (!Directory.Exists(talePath))
            {
                Trace($"TALE: No tale directory at {talePath}, module inactive.");
                return;
            }

            var library = new StoryletLibrary();
            library.LoadFromDirectory(talePath);
            Trace($"TALE: Loaded {library.GetCandidates("worker").Count} storylets.");

            // Create and register Role and Interaction registries
            I.Register<RoleRegistry>(() => new RoleRegistry());
            I.Register<InteractionTypeRegistry>(() => new InteractionTypeRegistry());
            I.Register<RelationshipTierRegistry>(() => new RelationshipTierRegistry());
            I.Register<GroupTypeRegistry>(() => new GroupTypeRegistry());
            Trace("TALE: RoleRegistry, InteractionTypeRegistry, RelationshipTierRegistry, and GroupTypeRegistry registered.");

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
                            currentSchedule.Properties[$"player_fact.{factObj}"] = 1f;
                            currentSchedule.HasPlayerDeviation = true;
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
            var loader = I.Get<engine.casette.Loader>();

            loader.WhenLoaded("/roles", (path, rolesNode) =>
            {
                var registry = I.Get<RoleRegistry>();
                if (rolesNode is JsonArray rolesArray)
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    options.Converters.Add(new ValueTupleFloatFloatConverter());
                    foreach (JsonNode roleNode in rolesArray)
                    {
                        var def = JsonSerializer.Deserialize<RoleDefinition>(roleNode.ToJsonString(), options);
                        if (def != null && !string.IsNullOrEmpty(def.Id))
                            registry.Add(def.Id, def);
                        else if (def != null)
                            Warning($"TALE: Role definition has null or empty Id: {roleNode.ToJsonString()}");
                    }
                    Trace($"TALE: Loaded {rolesArray.Count} role definitions.");
                }
                else
                    Warning($"TALE: /roles is not a JsonArray, got {rolesNode?.GetType().Name ?? "null"}");
            }, 100);

            loader.WhenLoaded("/interactions", (path, interactionsNode) =>
            {
                var registry = I.Get<InteractionTypeRegistry>();
                if (interactionsNode is JsonArray interactionsArray)
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    foreach (JsonNode typeNode in interactionsArray)
                    {
                        var def = JsonSerializer.Deserialize<InteractionTypeDefinition>(typeNode.ToJsonString(), options);
                        if (def != null && !string.IsNullOrEmpty(def.Id))
                            registry.Add(def.Id, def);
                        else if (def != null)
                            Warning($"TALE: Interaction type has null or empty Id: {typeNode.ToJsonString()}");
                    }
                    registry.FinalizeOrder();
                    Trace($"TALE: Loaded {interactionsArray.Count} interaction type definitions.");
                }
                else
                    Warning($"TALE: /interactions is not a JsonArray, got {interactionsNode?.GetType().Name ?? "null"}");
            }, 100);

            loader.WhenLoaded("/relationships/tiers", (path, tiersNode) =>
            {
                var registry = I.Get<RelationshipTierRegistry>();
                if (tiersNode is JsonArray tiersArray)
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    foreach (JsonNode tierNode in tiersArray)
                    {
                        var def = JsonSerializer.Deserialize<RelationshipTierDefinition>(tierNode.ToJsonString(), options);
                        if (def != null && !string.IsNullOrEmpty(def.Id))
                            registry.Add(def.Id, def);
                        else if (def != null)
                            Warning($"TALE: Relationship tier has null or empty Id: {tierNode.ToJsonString()}");
                    }
                    Trace($"TALE: Loaded {tiersArray.Count} relationship tier definitions.");
                }
                else
                    Warning($"TALE: /relationships/tiers is not a JsonArray, got {tiersNode?.GetType().Name ?? "null"}");
            }, 100);

            loader.WhenLoaded("/groups/types", (path, typesNode) =>
            {
                var registry = I.Get<GroupTypeRegistry>();
                if (typesNode is JsonArray typesArray)
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    foreach (JsonNode typeNode in typesArray)
                    {
                        var def = JsonSerializer.Deserialize<GroupTypeDefinition>(typeNode.ToJsonString(), options);
                        if (def != null && !string.IsNullOrEmpty(def.Id))
                            registry.Add(def.Id, def);
                        else if (def != null)
                            Warning($"TALE: Group type has null or empty Id: {typeNode.ToJsonString()}");
                    }
                    Trace($"TALE: Loaded {typesArray.Count} group type definitions.");
                }
                else
                    Warning($"TALE: /groups/types is not a JsonArray, got {typesNode?.GetType().Name ?? "null"}");
            }, 100);

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
    private void _onSocialFrame(object sender, float dt)
    {
        var maybeNow = _tryGetGameNow();
        if (maybeNow == null || _taleManager == null) return;
        DateTime gameNow = maybeNow.Value;

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

        var social = _taleManager.GetSocialState(clusterIndex);
        if (social == null) return;

        var byId = npcs.ToDictionary(n => n.NpcId);
        var rstats = _socialEvolver.Redetect(clusterIndex, social, byId, GroupNameGenerator.Generate);
        string groupSummary = string.Join(", ", social.Groups.Values.Select(
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
                currentSchedule.Trust ??= new Dictionary<int, float>();
                float current = currentSchedule.Trust.GetValueOrDefault(-1, 0.5f);
                currentSchedule.Trust[-1] = Math.Min(1f, current + 0.02f);
                currentSchedule.HasPlayerDeviation = true;
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
