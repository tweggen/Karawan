using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using builtin.tools;
using engine;
using engine.behave;
using engine.joyce;
using engine.tale;
using engine.world;
using builtin.modules.satnav.desc;
using static engine.Logger;

namespace nogame.characters.citizen;

/// <summary>
/// Spawn operator for TALE-driven NPCs. Materializes Tier 3 NPC schedules
/// from TaleManager into Tier 2/1 ECS entities with TaleEntityStrategy.
/// </summary>
public class TaleSpawnOperator : ISpawnOperator
{
    private static readonly engine.Dc _dc = engine.Dc.TaleSpawn;

    private object _lo = new();
    private engine.geom.AABB _aabb = new(Vector3.Zero, MetaGen.MaxWidth);
    private ClusterHeatMap _clusterHeatMap = I.Get<ClusterHeatMap>();
    private engine.world.Loader _loader = I.Get<MetaGen>().Loader;
    private engine.Engine _engine = I.Get<engine.Engine>();
    private builtin.tools.RandomSource _rnd = new("tale_citizen");

    private TaleManager _taleManager;

    private Dictionary<Index3, SpawnStatus> _mapFragmentStatus = new();

    /*
     * S0 W3 (KAR-447): which fragment owns each materialised NPC's entity.
     * The entity dies with that fragment (EntityCreator sets it as Owner),
     * wherever the NPC has walked since, so despawn follows this map, not the
     * schedule position. Guarded by _lo; liveness is read on the logical thread.
     */
    private TaleEntityOwnership<DefaultEcs.Entity> _ownership = new(e => e.IsAlive);


    public TaleSpawnOperator(TaleManager taleManager)
    {
        // Every method here dereferences the manager, and the first one runs from
        // the spawn controller's per-fragment poll, far away from this call site.
        // Fail here, where the caller can still be identified.
        _taleManager = taleManager
                       ?? throw new ArgumentNullException(nameof(taleManager),
                           "TaleSpawnOperator requires a TaleManager; TaleModule likely failed to activate.");
    }


    public engine.geom.AABB AABB
    {
        get
        {
            lock (_lo) return _aabb;
        }
    }


    public Type BehaviorType => typeof(IdleBehavior);


    private void _ensureClusterPopulated(ClusterDesc clusterDesc)
    {
        if (clusterDesc == null) return;
        if (_taleManager.IsClusterPopulated(clusterDesc.Index)) return;

        // Get NavCluster if available for proper street_segment positioning
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
            Logger.Trace(_dc, $"Failed to find NavCluster for spawn operator: {e.Message}");
        }

        var spatialModel = SpatialModel.ExtractFrom(clusterDesc, navClusterForCluster);
        _taleManager.PopulateCluster(clusterDesc, spatialModel, _tryGetGameNow());
    }


    private static DateTime? _tryGetGameNow()
    {
        try { return I.Get<nogame.modules.daynite.Controller>().GameNow; }
        catch { return null; }
    }


    private static async Task<Vector3?> _tryComputeRoadAwarePosition(
        NpcSchedule schedule, SpatialModel spatialModel, DateTime gameTime, ClusterDesc cd)
    {
        if (spatialModel == null) return null;
        if ((schedule.TransitEnd - schedule.TransitStart).TotalSeconds <= 0) return null;

        var fromLoc = spatialModel.GetLocation(schedule.TransitFromLocationId);
        var toLoc = spatialModel.GetLocation(schedule.TransitToLocationId);
        if (fromLoc == null || toLoc == null) return null;

        // Same per-NPC corner choice as NpcSchedule.PositionAt, so the road-aware
        // sample and the chord fallback aim at the same points.
        var fromPos = fromLoc.EntryPositionFor(schedule.NpcId);
        var toPos = toLoc.EntryPositionFor(schedule.NpcId);

        NavMap navMap;
        try { navMap = I.Get<NavMap>(); }
        catch { return null; }
        if (navMap == null) return null;

        var startPod = new PositionDescription { Position = fromPos, ClusterDesc = cd };
        SegmentRoute route;
        try
        {
            route = await StreetRouteBuilder.BuildAsync(fromPos, toPos, navMap, startPod);
        }
        catch { return null; }
        if (route == null || route.Segments.Count < 2) return null;

        float t = (float)((gameTime - schedule.TransitStart).TotalSeconds
                          / (schedule.TransitEnd - schedule.TransitStart).TotalSeconds);
        t = Math.Clamp(t, 0f, 1f);

        return _sampleRouteAt(route, t);
    }


    private static Vector3 _sampleRouteAt(SegmentRoute route, float t)
    {
        float total = 0f;
        for (int i = 1; i < route.Segments.Count; i++)
            total += Vector3.Distance(route.Segments[i - 1].Position, route.Segments[i].Position);

        if (total <= 0f) return route.Segments[0].Position;

        float target = total * t;
        float traveled = 0f;
        for (int i = 1; i < route.Segments.Count; i++)
        {
            var a = route.Segments[i - 1].Position;
            var b = route.Segments[i].Position;
            float segLen = Vector3.Distance(a, b);
            if (traveled + segLen >= target)
            {
                float u = segLen > 0f ? (target - traveled) / segLen : 0f;
                return Vector3.Lerp(a, b, u);
            }
            traveled += segLen;
        }
        return route.Segments[^1].Position;
    }


    private void _findSpawnStatus_nl(in Index3 idxFragment, out SpawnStatus spawnStatus)
    {
        if (!_mapFragmentStatus.TryGetValue(idxFragment, out spawnStatus))
        {
            spawnStatus = new SpawnStatus { InCreation = 0 };
            _mapFragmentStatus[idxFragment] = spawnStatus;
        }

        // Ensure cluster is populated on-demand (avoids race with ClusterCompletedEvent)
        var cd = _clusterHeatMap.GetClusterDesc(idxFragment);
        _ensureClusterPopulated(cd);

        /*
         * Entities the wiper (or anything else) killed while their owner fragment
         * stays loaded: no purge will come for them, so dematerialise them here.
         */
        foreach (var npcId in _ownership.RemoveDeadOwnedBy(idxFragment))
        {
            Trace(_dc, $"NPC {npcId}'s entity died while fragment {idxFragment} is loaded; dematerialising.");
            _taleManager.OnNpcDematerialized(npcId, null);
        }

        var npcsInFragment = _taleManager.GetNpcsInFragment(idxFragment);
        int npcCount = npcsInFragment.Count;
        spawnStatus.MinCharacters = (ushort)npcCount;
        spawnStatus.MaxCharacters = (ushort)npcCount;

        /*
         * The spawn system never counts TALE entities (it counts IdleBehavior,
         * which they never carry), so report the ones that stand here as
         * resident. Together with InCreation this makes the controller queue
         * spawns only for NPCs that are actually missing. NPCs still in creation
         * have no ownership entry yet and are counted by InCreation alone.
         */
        spawnStatus.Dead = (ushort)_ownership.CountAlive(npcsInFragment.Select(n => n.NpcId));
    }


    public SpawnStatus GetFragmentSpawnStatus(Type behaviorType, in Index3 idxFragment)
    {
        lock (_lo)
        {
            _findSpawnStatus_nl(idxFragment, out var ss);
            return ss;
        }
    }


    /**
     * The fragment unloads and takes the entities it owns with it. Dematerialise
     * exactly those NPCs, whichever fragment their schedule now points into,
     * and remember where each entity stood. The loader calls this before it
     * removes the fragment's entities, so they are still readable here.
     */
    public void PurgeFragment(in Index3 idxFragment)
    {
        bool canReadEntities = DefaultEcs.Entity.OkThread == null
                               || DefaultEcs.Entity.OkThread == System.Threading.Thread.CurrentThread;
        lock (_lo)
        {
            foreach (var (npcId, entity) in _ownership.RemoveOwnedBy(idxFragment))
            {
                Vector3? worldPos = null;
                if (canReadEntities
                    && entity.IsAlive
                    && entity.Has<engine.joyce.components.Transform3ToWorld>())
                {
                    worldPos = entity.Get<engine.joyce.components.Transform3ToWorld>().Matrix.Translation;
                }
                Trace(_dc, $"Fragment {idxFragment} purged; dematerialising NPC {npcId} at {worldPos}.");
                _taleManager.OnNpcDematerialized(npcId, worldPos);
            }

            _mapFragmentStatus.Remove(idxFragment);
        }
    }


    public Action SpawnCharacter(Type behaviorType, Index3 idxFragment, PerFragmentStats perFragmentStats)
    {
        // Find the next unmaterialized NPC in this fragment
        NpcSchedule targetNpc = null;
        lock (_lo)
        {
            var npcsInFragment = _taleManager.GetNpcsInFragment(idxFragment);
            foreach (var npc in npcsInFragment)
            {
                if (!_taleManager.IsMaterialized(npc.NpcId))
                {
                    targetNpc = npc;
                    break;
                }
            }
        }

        if (targetNpc == null)
        {
            // With the resident count (S0 W3) this should be rare: only while an
            // NPC owned elsewhere has just walked in and the bucket is stale.
            Trace(_dc, $"No unmaterialized NPC found in fragment {idxFragment}; spawn is a no-op.");
            return () => { };
        }

        int npcId = targetNpc.NpcId;
        Trace(_dc, $"Spawning NPC {npcId} (role={targetNpc.Role}) in fragment {idxFragment}.");
        _taleManager.SetMaterialized(npcId);

        SpawnStatus spawnStatus;
        lock (_lo)
        {
            _findSpawnStatus_nl(idxFragment, out spawnStatus);
        }
        spawnStatus.InCreation++;

        return async () =>
        {
            try
            {
                ClusterDesc cd = _clusterHeatMap.GetClusterDesc(idxFragment);
                if (cd == null)
                {
                    Warning(_dc, $"No ClusterDesc for fragment {idxFragment}, aborting NPC {npcId}.");
                    spawnStatus.InCreation--;
                    _taleManager.SetDematerialized(npcId);
                    return;
                }

                if (!_loader.TryGetFragment(idxFragment, out var worldFragment))
                {
                    Warning(_dc, $"No WorldFragment for {idxFragment}, aborting NPC {npcId}.");
                    spawnStatus.InCreation--;
                    _taleManager.SetDematerialized(npcId);
                    return;
                }

                // Get NPC schedule and compute time-appropriate spawn position
                var schedule = _taleManager.GetSchedule(npcId);
                if (schedule == null)
                {
                    Warning(_dc, $"Schedule disappeared for NPC {npcId}, aborting.");
                    spawnStatus.InCreation--;
                    _taleManager.SetDematerialized(npcId);
                    return;
                }

                // Get current game time (prefer daynite controller if available)
                DateTime gameTime = _tryGetGameNow() ?? DateTime.Now;

                // The warmup in PopulateCluster anchored schedules at the daynite clock,
                // so most NPCs are still inside their current storylet's window. Only
                // advance when the current storylet has actually ended — otherwise we'd
                // overshoot the transit/activity the warmup set up and snap the NPC to
                // its destination's discrete entry position. When still in-window,
                // PositionAt will interpolate or return the right discrete position.
                if (gameTime >= schedule.CurrentEnd)
                {
                    _taleManager.AdvanceNpc(npcId, gameTime);
                    schedule = _taleManager.GetSchedule(npcId);
                }

                // Compute spawn position based on NPC's schedule at current game time
                var spatialModel = _taleManager.GetSpatialModel(schedule.ClusterIndex);
                var spawnPosition = schedule.PositionAt(gameTime, spatialModel);
                Trace(_dc, $"NPC {npcId} spawn position computed for game time {gameTime}: {spawnPosition}");

                // Detect if NPC is mid-transit at spawn time
                bool spawnInTravel = schedule.IsInTransit
                    && gameTime >= schedule.TransitStart
                    && gameTime < schedule.TransitEnd;

                if (spawnInTravel)
                {
                    Trace(_dc, $"NPC {npcId} spawning in TRANSIT (game time {gameTime:HH:mm}, " +
                          $"transit {schedule.TransitStart:HH:mm}-{schedule.TransitEnd:HH:mm})");

                    // Replace the chord-lerp position from PositionAt with a point sampled
                    // along an actual road path, so mid-transit NPCs appear on a sidewalk
                    // rather than along a straight line through buildings. Falls back to
                    // the chord position if pathfinding is unavailable.
                    var roadPos = await _tryComputeRoadAwarePosition(schedule, spatialModel, gameTime, cd);
                    if (roadPos.HasValue)
                    {
                        spawnPosition = roadPos.Value;
                        Trace(_dc, $"NPC {npcId} placed on road path at {spawnPosition}");
                    }
                }
                else if (schedule.IsInTransit)
                {
                    Trace(_dc, $"NPC {npcId} IS in transit but NOT in spawn window " +
                          $"(game time {gameTime:HH:mm}, transit {schedule.TransitStart:HH:mm}-{schedule.TransitEnd:HH:mm})");
                }

                var pod = new PositionDescription { Position = spawnPosition, ClusterDesc = cd };

                // Create character model deterministically from NPC seed
                var npcRnd = new builtin.tools.RandomSource(cd.GetKey() + "-npc-" + schedule.NpcIndex + "-model");

                // Look up the role's model pool (if defined). If registry not ready yet, abort and retry.
                var roleRegistry = I.Get<engine.tale.RoleRegistry>();
                if (!roleRegistry.Has(schedule.Role))
                {
                    Warning(_dc, $"Role '{schedule.Role}' not in registry for NPC {npcId}, will retry.");
                    spawnStatus.InCreation--;
                    _taleManager.SetDematerialized(npcId);
                    return;
                }
                var roleModels = roleRegistry.Get(schedule.Role).Models;
                var cmd = CharacterModelDescriptionFactory.CreateCitizen(npcRnd, roleModels);

                // Create TALE strategy using the existing schedule
                if (!TaleEntityStrategy.TryCreate(schedule, _taleManager, pod, cmd, out var taleStrategy))
                {
                    Warning(_dc, $"TaleEntityStrategy.TryCreate failed for NPC {npcId}.");
                    spawnStatus.InCreation--;
                    _taleManager.SetDematerialized(npcId);
                    return;
                }

                Trace(_dc, $"Creating entity for NPC {npcId} at {pod.Position}.");

                // Create entity
                EntityCreator creator = new()
                {
                    EntityStrategyFactory = entity => taleStrategy,
                    CharacterModelDescription = cmd,
                    PhysicsName = CharacterCreator.EntityName,
                    Fragment = worldFragment,
                    Position = pod.Position
                };
                var model = await creator.CreateAsync();

                _engine.QueueEntitySetupAction(CharacterCreator.EntityName, e =>
                {
                    /*
                     * The owner fragment was purged while this NPC was in creation:
                     * it has already removed its entities and would never take this
                     * one along. Drop it; the NPC respawns wherever it stands.
                     */
                    if (!_loader.TryGetFragment(idxFragment, out _))
                    {
                        Trace(_dc, $"Fragment {idxFragment} went away while NPC {npcId} was in creation; dropping it.");
                        e.Disable();
                        I.Get<HierarchyApi>().Delete(ref e);
                        _taleManager.SetDematerialized(npcId);
                        spawnStatus.InCreation--;
                        return;
                    }

                    creator.CreateLogical(e);
                    CharacterCreator.AddMapIcon(e, schedule.Role);

                    // Stamp entity with NPC ID for position sync during dematerialization
                    e.Set(new engine.tale.components.TaleNpcId { NpcId = npcId });

                    // Behavior component is already set by the strategy's OnEnter
                    // (TaleConversationBehavior handles both "E to Talk" and Tier 2→1 promotion)

                    // Post-spawn health check: verify conversation behavior survived setup
                    if (e.Has<engine.behave.components.Behavior>())
                    {
                        var beh = e.Get<engine.behave.components.Behavior>();
                        if (beh.Provider is not nogame.tools.ANearbyBehavior)
                        {
                            Warning(_dc, $"NPC {npcId} behavior is {beh.Provider?.GetType().Name ?? "null"}, expected ANearbyBehavior! E-to-Talk will not work.");
                        }
                    }
                    else if (!spawnInTravel)
                    {
                        Warning(_dc, $"NPC {npcId} has no Behavior component after setup!");
                    }

                    I.Get<engine.joyce.TransformApi>().SetTransforms(
                        e,
                        true,
                        0x0000ffff,
                        Quaternion.Identity,
                        pod.Position
                    );

                    // If NPC is mid-transit at spawn, enter travel state
                    if (spawnInTravel)
                    {
                        taleStrategy.SpawnInTravel();
                    }

                    // Ownership and InCreation-- in the same action, so the NPC is
                    // counted exactly once at every poll (S0 W3).
                    lock (_lo)
                    {
                        _ownership.Record(npcId, idxFragment, e);
                    }
                    spawnStatus.InCreation--;
                });
            }
            catch (Exception e)
            {
                spawnStatus.InCreation--;
                _taleManager.SetDematerialized(npcId);
                Error(_dc, $"Exception spawning character: {e}");
            }
        };
    }


    /**
     * Never called for TALE NPCs: the spawn system counts entities by exact
     * behaviour type, this operator registers IdleBehavior, and TALE NPCs never
     * carry it, so no fragment is ever "too full". TALE entities die with their
     * owner fragment (PurgeFragment) or through the wiper (the liveness check in
     * _findSpawnStatus_nl). The former Tier-2 freeze and 30-minute forget path
     * here was dead code and was removed in S0 W3 (KAR-447); an NPC the player
     * knows is never forgotten.
     */
    public void TerminateCharacters(List<(Index3, DefaultEcs.Entity)> listKills)
    {
    }
}
