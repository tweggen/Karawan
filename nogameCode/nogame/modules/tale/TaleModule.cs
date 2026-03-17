using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using engine;
using engine.news;
using engine.tale;
using engine.world;
using static engine.Logger;

namespace nogame.modules.tale;

/// <summary>
/// Module that bootstraps the TALE narrative system.
/// Loads the storylet library, creates the TaleManager singleton,
/// and hooks into cluster lifecycle for population management.
/// </summary>
public class TaleModule : AModule
{
    private TaleManager _taleManager;
    private ClusterList _clusterList;


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

        // Extract spatial model for location assignment
        var spatialModel = SpatialModel.ExtractFrom(clusterDesc);
        _taleManager.PopulateCluster(clusterDesc, spatialModel);

        Trace($"TALE: Populated cluster '{ev.Code}' (index {clusterDesc.Index}). " +
              $"Spatial: {spatialModel.BuildingCount} buildings, {spatialModel.ShopCount} shops, " +
              $"{spatialModel.StreetPointCount} street points.");
    }


    private void _onBeforeSaveGame(object sender, object args)
    {
        if (_taleManager == null) return;

        var deviated = _taleManager.GetAllDeviatedNpcs();
        if (deviated.Count == 0) return;

        var jArray = new JsonArray();
        foreach (var npc in deviated)
        {
            jArray.Add(SerializeNpcSchedule(npc));
        }

        var autoSave = M<nogame.modules.AutoSave>();
        autoSave.GameState.TaleDeviations = jArray.ToJsonString();

        Trace($"TALE: Saved {deviated.Count} deviated NPCs.");
    }


    private void _onAfterLoadGame(object sender, object objGameState)
    {
        if (_taleManager == null) return;
        if (objGameState is not GameState gs) return;
        if (string.IsNullOrEmpty(gs.TaleDeviations)) return;

        try
        {
            var jArray = JsonNode.Parse(gs.TaleDeviations)?.AsArray();
            if (jArray == null) return;

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
        catch (Exception e)
        {
            Warning($"TALE: Failed to load deviated NPCs: {e.Message}");
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
            Trace("TALE: RoleRegistry and InteractionTypeRegistry registered.");

            // Create and register TaleManager
            _taleManager = new TaleManager();
            _taleManager.Initialize(library);

            I.Register<TaleManager>(() => _taleManager);
            Trace("TALE: TaleManager registered.");

            // Load roles and interaction types from config
            var loader = I.Get<engine.casette.Loader>();

            loader.WhenLoaded("/roles", (path, rolesNode) =>
            {
                var registry = I.Get<RoleRegistry>();
                if (rolesNode is JsonArray rolesArray)
                {
                    foreach (JsonNode roleNode in rolesArray)
                    {
                        var def = JsonSerializer.Deserialize<RoleDefinition>(roleNode.ToJsonString());
                        if (def != null)
                            registry.Add(def.Id, def);
                    }
                    Trace($"TALE: Loaded {rolesArray.Count} role definitions.");
                }
            });

            loader.WhenLoaded("/interactions", (path, interactionsNode) =>
            {
                var registry = I.Get<InteractionTypeRegistry>();
                if (interactionsNode is JsonArray interactionsArray)
                {
                    foreach (JsonNode typeNode in interactionsArray)
                    {
                        var def = JsonSerializer.Deserialize<InteractionTypeDefinition>(typeNode.ToJsonString());
                        if (def != null)
                            registry.Add(def.Id, def);
                    }
                    registry.FinalizeOrder();
                    Trace($"TALE: Loaded {interactionsArray.Count} interaction type definitions.");
                }
            });

            // Get cluster list for lifecycle events
            _clusterList = I.Get<ClusterList>();

            // Subscribe to cluster lifecycle
            Subscribe(ClusterCompletedEvent.EVENT_TYPE, _onClusterCompleted);

            // Subscribe to save/load for deviation persistence
            M<Saver>().OnBeforeSaveGame += _onBeforeSaveGame;
            M<Saver>().OnAfterLoadGame += _onAfterLoadGame;
        }
        catch (Exception e)
        {
            Warning($"TALE: Failed to initialize: {e}");
        }
    }


    protected override void OnModuleDeactivate()
    {
        M<Saver>().OnBeforeSaveGame -= _onBeforeSaveGame;
        M<Saver>().OnAfterLoadGame -= _onAfterLoadGame;
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
        };

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
}
