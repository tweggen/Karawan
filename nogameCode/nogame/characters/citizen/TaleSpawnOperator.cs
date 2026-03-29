using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.behave;
using engine.joyce;
using engine.tale;
using engine.world;
using static engine.Logger;

namespace nogame.characters.citizen;

/// <summary>
/// Spawn operator for TALE-driven NPCs. Materializes Tier 3 NPC schedules
/// from TaleManager into Tier 2/1 ECS entities with TaleEntityStrategy.
/// </summary>
public class TaleSpawnOperator : ISpawnOperator
{
    private object _lo = new();
    private engine.geom.AABB _aabb = new(Vector3.Zero, MetaGen.MaxWidth);
    private ClusterHeatMap _clusterHeatMap = I.Get<ClusterHeatMap>();
    private engine.world.Loader _loader = I.Get<MetaGen>().Loader;
    private engine.Engine _engine = I.Get<engine.Engine>();
    private builtin.tools.RandomSource _rnd = new("tale_citizen");

    private TaleManager _taleManager;

    private Dictionary<Index3, SpawnStatus> _mapFragmentStatus = new();


    public TaleSpawnOperator(TaleManager taleManager)
    {
        _taleManager = taleManager;
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

        var spatialModel = SpatialModel.ExtractFrom(clusterDesc);
        _taleManager.PopulateCluster(clusterDesc, spatialModel);
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

        var npcsInFragment = _taleManager.GetNpcsInFragment(idxFragment);
        int npcCount = npcsInFragment.Count;
        spawnStatus.MinCharacters = (ushort)npcCount;
        spawnStatus.MaxCharacters = (ushort)npcCount;
    }


    public SpawnStatus GetFragmentSpawnStatus(Type behaviorType, in Index3 idxFragment)
    {
        lock (_lo)
        {
            _findSpawnStatus_nl(idxFragment, out var ss);
            return ss;
        }
    }


    public void PurgeFragment(in Index3 idxFragment)
    {
        lock (_lo)
        {
            // Dematerialize all NPCs in this fragment
            var npcsInFragment = _taleManager.GetNpcsInFragment(idxFragment);
            foreach (var npc in npcsInFragment)
            {
                // Clear Tier 2 flag if set (entity will be destroyed)
                if (_taleManager.IsTier2(npc.NpcId))
                    _taleManager.ClearTier2(npc.NpcId);

                _taleManager.SetDematerialized(npc.NpcId);
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
            // Trace($"TALE SPAWN: No unmaterialized NPC found in fragment {idxFragment}.");
            return () => { };
        }

        int npcId = targetNpc.NpcId;
        Trace($"TALE SPAWN: Spawning NPC {npcId} (role={targetNpc.Role}) in fragment {idxFragment}.");
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
                    Warning($"TALE SPAWN: No ClusterDesc for fragment {idxFragment}, aborting NPC {npcId}.");
                    spawnStatus.InCreation--;
                    _taleManager.SetDematerialized(npcId);
                    return;
                }

                if (!_loader.TryGetFragment(idxFragment, out var worldFragment))
                {
                    Warning($"TALE SPAWN: No WorldFragment for {idxFragment}, aborting NPC {npcId}.");
                    spawnStatus.InCreation--;
                    _taleManager.SetDematerialized(npcId);
                    return;
                }

                // Get NPC schedule and compute time-appropriate spawn position
                var schedule = _taleManager.GetSchedule(npcId);
                if (schedule == null)
                {
                    Warning($"TALE SPAWN: Schedule disappeared for NPC {npcId}, aborting.");
                    spawnStatus.InCreation--;
                    _taleManager.SetDematerialized(npcId);
                    return;
                }

                // Mark this NPC as noticed by the player (will promote map icon visibility)
                schedule.IsNoticedByPlayer = true;

                // Get current game time (prefer daynite controller if available)
                DateTime gameTime = DateTime.Now;
                try
                {
                    var dayNiteController = I.Get<nogame.modules.daynite.Controller>();
                    if (dayNiteController != null)
                        gameTime = dayNiteController.GameNow;
                }
                catch (Exception)
                {
                    // Fallback to DateTime.Now if daynite not available
                }

                // Re-advance NPC to current game time to refresh transit phases
                // This ensures transit windows align with actual spawn time (not stale from population)
                _taleManager.AdvanceNpc(npcId, gameTime);
                schedule = _taleManager.GetSchedule(npcId); // Refresh schedule after re-advance

                // Compute spawn position based on NPC's schedule at current game time
                var spatialModel = _taleManager.GetSpatialModel(schedule.ClusterIndex);
                var spawnPosition = schedule.PositionAt(gameTime, spatialModel);
                Trace($"TALE SPAWN: NPC {npcId} spawn position computed for game time {gameTime}: {spawnPosition}");

                // Detect if NPC is mid-transit at spawn time
                bool spawnInTravel = schedule.IsInTransit
                    && gameTime >= schedule.TransitStart
                    && gameTime < schedule.TransitEnd;

                if (spawnInTravel)
                {
                    Trace($"TALE SPAWN: NPC {npcId} spawning in TRANSIT (game time {gameTime:HH:mm}, " +
                          $"transit {schedule.TransitStart:HH:mm}-{schedule.TransitEnd:HH:mm})");
                }
                else if (schedule.IsInTransit)
                {
                    Trace($"TALE SPAWN: NPC {npcId} IS in transit but NOT in spawn window " +
                          $"(game time {gameTime:HH:mm}, transit {schedule.TransitStart:HH:mm}-{schedule.TransitEnd:HH:mm})");
                }

                var pod = new PositionDescription { Position = spawnPosition, ClusterDesc = cd };

                // Create character model deterministically from NPC seed
                var npcRnd = new builtin.tools.RandomSource(cd.GetKey() + "-npc-" + schedule.NpcIndex + "-model");

                // Look up the role's model pool (if defined). If registry not ready yet, abort and retry.
                var roleRegistry = I.Get<engine.tale.RoleRegistry>();
                if (!roleRegistry.Has(schedule.Role))
                {
                    Warning($"TALE SPAWN: Role '{schedule.Role}' not in registry for NPC {npcId}, will retry.");
                    spawnStatus.InCreation--;
                    _taleManager.SetDematerialized(npcId);
                    return;
                }
                var roleModels = roleRegistry.Get(schedule.Role).Models;
                var cmd = CharacterModelDescriptionFactory.CreateCitizen(npcRnd, roleModels);

                // Create TALE strategy using the existing schedule
                if (!TaleEntityStrategy.TryCreate(schedule, _taleManager, pod, cmd, out var taleStrategy))
                {
                    Warning($"TALE SPAWN: TaleEntityStrategy.TryCreate failed for NPC {npcId}.");
                    spawnStatus.InCreation--;
                    _taleManager.SetDematerialized(npcId);
                    return;
                }

                Trace($"TALE SPAWN: Creating entity for NPC {npcId} at {pod.Position}.");

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
                    creator.CreateLogical(e);
                    CharacterCreator.AddMapIcon(e, schedule.Role);

                    // Stamp entity with NPC ID for position sync during dematerialization
                    e.Set(new engine.tale.components.TaleNpcId { NpcId = npcId });

                    // Set up behavior provider for Tier 2 ↔ Tier 1 transitions
                    var taleEntityBehavior = new TaleEntityBehavior(taleStrategy);
                    e.Set(new engine.behave.components.Behavior { Provider = taleEntityBehavior });

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

                    spawnStatus.InCreation--;
                });
            }
            catch (Exception e)
            {
                spawnStatus.InCreation--;
                _taleManager.SetDematerialized(npcId);
                Error($"Exception spawning TALE character: {e}");
            }
        };
    }


    public void TerminateCharacters(List<(Index3, DefaultEcs.Entity)> listKills)
    {
        List<SpawnStatus> listSpawnStatus = new(listKills.Count);
        List<(Index3 fragment, DefaultEcs.Entity entity)> listToDestroy = new();

        foreach (var kill in listKills)
        {
            lock (_lo)
            {
                _findSpawnStatus_nl(kill.Item1, out var spawnStatus);
                listSpawnStatus.Add(spawnStatus);
                spawnStatus.IsDying++;
            }
        }

        // Sync entity positions to schedule before destroying / demoting
        foreach (var kill in listKills)
        {
            var entity = kill.Item2;
            if (entity.IsAlive
                && entity.Has<engine.tale.components.TaleNpcId>()
                && entity.Has<engine.joyce.components.Transform3ToWorld>())
            {
                int npcId = entity.Get<engine.tale.components.TaleNpcId>().NpcId;
                var worldPos = entity.Get<engine.joyce.components.Transform3ToWorld>().Matrix.Translation;
                var schedule = _taleManager.GetSchedule(npcId);
                if (schedule != null)
                {
                    schedule.CurrentWorldPosition = worldPos;
                    Trace($"TALE SPAWN: Synced NPC {npcId} position to {worldPos} before dematerialization.");

                    // Demote to Tier 2 if noticed: keep entity alive, freeze strategy
                    if (schedule.IsNoticedByPlayer)
                    {
                        _taleManager.SetTier2(npcId);
                        Trace($"TALE SPAWN: Demoting NPC {npcId} to Tier 2 (noticed). Map icon remains visible.");

                        // Freeze strategy by entering Tier 2 mode
                        if (entity.Has<engine.behave.components.Strategy>())
                        {
                            var strategy = entity.Get<engine.behave.components.Strategy>().EntityStrategy;
                            if (strategy is TaleEntityStrategy tes)
                                tes.EnterTier2Mode();
                        }
                        // Skip the destroy path for this entity
                        continue;
                    }
                }
            }

            // Entity was not noticed or schedule missing: queue for destruction
            listToDestroy.Add(kill);
        }

        _engine.QueueCleanupAction(() =>
        {
            foreach (var kill in listToDestroy)
            {
                var entity = kill.entity;
                entity.Disable();
                I.Get<HierarchyApi>().Delete(ref entity);
            }

            foreach (var spawnStatus in listSpawnStatus)
            {
                spawnStatus.IsDying--;
            }
        });
    }
}
