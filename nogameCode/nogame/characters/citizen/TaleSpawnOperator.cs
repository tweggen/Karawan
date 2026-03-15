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


    private void _findSpawnStatus_nl(in Index3 idxFragment, out SpawnStatus spawnStatus)
    {
        if (!_mapFragmentStatus.TryGetValue(idxFragment, out spawnStatus))
        {
            spawnStatus = new SpawnStatus { InCreation = 0 };
            _mapFragmentStatus[idxFragment] = spawnStatus;

            // Count how many TALE NPCs are registered for this fragment
            var npcsInFragment = _taleManager.GetNpcsInFragment(idxFragment);
            int npcCount = npcsInFragment.Count;

            spawnStatus.MinCharacters = (ushort)npcCount;
            spawnStatus.MaxCharacters = (ushort)npcCount;
        }
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

        if (targetNpc == null) return () => { };

        int npcId = targetNpc.NpcId;
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
                    spawnStatus.InCreation--;
                    _taleManager.SetDematerialized(npcId);
                    return;
                }

                if (!_loader.TryGetFragment(idxFragment, out var worldFragment))
                {
                    spawnStatus.InCreation--;
                    _taleManager.SetDematerialized(npcId);
                    return;
                }

                // Use the NPC's home position from its schedule
                var schedule = _taleManager.GetSchedule(npcId);
                if (schedule == null)
                {
                    spawnStatus.InCreation--;
                    _taleManager.SetDematerialized(npcId);
                    return;
                }

                var pod = new PositionDescription { Position = schedule.HomePosition };

                // Create character model deterministically from NPC seed
                var npcRnd = new builtin.tools.RandomSource(cd.GetKey() + "-npc-" + schedule.NpcIndex + "-model");
                var cmd = CharacterModelDescriptionFactory.CreateCitizen(npcRnd);

                // Create TALE strategy using the existing schedule
                if (!TaleEntityStrategy.TryCreate(schedule, _taleManager, pod, cmd, out var taleStrategy))
                {
                    spawnStatus.InCreation--;
                    _taleManager.SetDematerialized(npcId);
                    return;
                }

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

                    I.Get<engine.joyce.TransformApi>().SetTransforms(
                        e,
                        true,
                        0x0000ffff,
                        Quaternion.Identity,
                        pod.Position
                    );

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

        foreach (var kill in listKills)
        {
            lock (_lo)
            {
                _findSpawnStatus_nl(kill.Item1, out var spawnStatus);
                listSpawnStatus.Add(spawnStatus);
                spawnStatus.IsDying++;
            }
        }

        _engine.QueueCleanupAction(() =>
        {
            foreach (var kill in listKills)
            {
                var entity = kill.Item2;
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
