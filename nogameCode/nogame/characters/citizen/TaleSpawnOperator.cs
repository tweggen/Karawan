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
/// Spawn operator for TALE-driven NPCs. Creates citizen entities that are
/// controlled by TaleEntityStrategy instead of the default patrol-loop EntityStrategy.
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
    private int _seed = 0;

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

            // Count how many TALE NPCs should be in this fragment
            var cd = _clusterHeatMap.GetClusterDesc(idxFragment);
            if (cd == null)
            {
                spawnStatus.MinCharacters = 0;
                spawnStatus.MaxCharacters = 0;
            }
            else
            {
                // For now, TALE NPCs are a fixed small count per fragment
                // This will be tuned as the TALE system populates
                var ilistSPs = cd.GetStreetPointsInFragment(idxFragment);
                float density = cd.GetAttributeIntensity(
                    idxFragment.AsVector3(), ClusterDesc.LocationAttributes.Downtown);
                float nMax = 2f * ilistSPs.Count * density;
                spawnStatus.MinCharacters = (ushort)Math.Max(0, nMax * 0.5f);
                spawnStatus.MaxCharacters = (ushort)(nMax);
            }
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
            _mapFragmentStatus.Remove(idxFragment);
        }
    }


    public Action SpawnCharacter(Type behaviorType, Index3 idxFragment, PerFragmentStats perFragmentStats)
    {
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
                    return;
                }

                if (!_loader.TryGetFragment(idxFragment, out var worldFragment))
                {
                    spawnStatus.InCreation--;
                    return;
                }

                // Choose start position
                EntityStrategy._chooseStartPosition(_rnd, worldFragment, cd, out var pod);
                if (pod == null)
                {
                    spawnStatus.InCreation--;
                    return;
                }

                // Create character model
                var cmd = CharacterModelDescriptionFactory.CreateCitizen(_rnd);

                // Create TALE strategy
                int npcId = _seed;
                if (!TaleEntityStrategy.TryCreate(npcId, _taleManager, pod, cmd, out var taleStrategy))
                {
                    spawnStatus.InCreation--;
                    return;
                }

                // Create entity
                EntityCreator creator = new()
                {
                    EntityStrategyFactory = entity => taleStrategy,
                    CharacterModelDescription = cmd,
                    PhysicsName = CharacterCreator.EntityName,
                    Fragment = worldFragment
                };
                var model = await creator.CreateAsync();

                _engine.QueueEntitySetupAction(CharacterCreator.EntityName, e =>
                {
                    creator.CreateLogical(e);
                    spawnStatus.InCreation--;
                });
                ++_seed;
            }
            catch (Exception e)
            {
                spawnStatus.InCreation--;
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
