using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.behave;
using engine.joyce;
using engine.news;
using engine.world;
using OneOf.Types;
using static engine.Logger;

namespace nogame.characters.citizen;

/**
 * Spawn operator for the omnipresent cars. 
 */
public class SpawnOperator : ISpawnOperator
{
    private object _lo = new();
    private engine.geom.AABB _aabb = new(Vector3.Zero, engine.world.MetaGen.MaxWidth);
    private ClusterHeatMap _clusterHeatMap = I.Get<engine.behave.ClusterHeatMap>();
    private engine.world.Loader _loader = I.Get<engine.world.MetaGen>().Loader;
    private engine.Engine _engine = I.Get<engine.Engine>();
    private builtin.tools.RandomSource _rnd = new("citizen");

    private const string _spawnCitizenProperties = "nogame.characters.citizen";
    private float _citizenPerStreetPoint = 6f;
    private float _minCitizenFactor = 1f;
    private float _maxCitizenFactor = 2f;
    private bool _areParametersDirty = true;
    
    public engine.geom.AABB AABB
    {
        get
        {
            lock (_lo)
            {
                return _aabb;
            }
        }
    }


    public System.Type BehaviorType
    {
        get => typeof(nogame.characters.citizen.WalkBehavior);
    }


    private Dictionary<Index3, SpawnStatus> _mapFragmentStatus = new();

    
    private void _findSpawnStatus_nl(in Index3 idxFragment, out SpawnStatus spawnStatus)
    {
        bool recomputeStats = false;

        if (!_mapFragmentStatus.TryGetValue(idxFragment, out spawnStatus))
        {
            recomputeStats = true;
            spawnStatus = new() { InCreation = 0 };
            _mapFragmentStatus[idxFragment] = spawnStatus;
        }

        if (_areParametersDirty)
        {
            recomputeStats = true;
            _areParametersDirty = false;
        }

        if (recomputeStats)
        {
            /*
             * Read the probability for this fragment from the cluster heat map,
             * return an appropriate spawnStatus.
             */
            var cd = _clusterHeatMap.GetClusterDesc(idxFragment);
            if (null == cd)
            {
                spawnStatus.MinCharacters = 0;
                spawnStatus.MaxCharacters = 0;
            }
            else
            {
                
                var density =
                    cd.GetAttributeIntensity(idxFragment.AsVector3(), ClusterDesc.LocationAttributes.Downtown);

                /*
                 * Now, before being able to estimate the number of cars, let's see
                 * how many different possible spawning points we have.
                 */
                var ilistSPs = cd.GetStreetPointsInFragment(idxFragment);

                // TXWTODO: Filter, so that we have meaningful spawning points.
                float nMaxSpawns = _citizenPerStreetPoint * ilistSPs.Count * density;

                /*_
                 * Note that it is technically wrong to return the number of characters in creation
                 * in total. However, this only would prevent more characters compared to the offset.
                 */
                spawnStatus.MinCharacters = (ushort)(_minCitizenFactor * nMaxSpawns);
                spawnStatus.MaxCharacters = (ushort)(_maxCitizenFactor * nMaxSpawns);
            }
        }
    }


    private void _findSpawnStatus(in Index3 idxFragment, out SpawnStatus spawnStatus)
    {
        lock (_lo)
        {
            _findSpawnStatus_nl(idxFragment, out spawnStatus);
        }
    }
    
    
    public SpawnStatus GetFragmentSpawnStatus(System.Type behaviorType, in Index3 idxFragment)
    {
        _findSpawnStatus(idxFragment, out var spawnStatus);
        return spawnStatus;
    }


    public void PurgeFragment(in Index3 idxFragment)
    {
        lock (_lo)
        {
            _mapFragmentStatus.Remove(idxFragment);
        }
    }


    private int _seed = 0;
    

    public Action SpawnCharacter(System.Type behaviorType, Index3 idxFragment, PerFragmentStats perFragmentStats)
    {
        _findSpawnStatus(idxFragment, out var spawnStatus);
        spawnStatus.InCreation++;

        return async () =>
        {
            /*
             * Catch exception to keep the inCreation counter up to date.
             */
            try
            {
                ClusterDesc cd = _clusterHeatMap.GetClusterDesc(idxFragment);
                if (cd != null)
                {
                    engine.world.Fragment worldFragment;
                    if (_loader.TryGetFragment(idxFragment, out worldFragment))
                    {
                        var characterResult = await CharacterCreator.GenerateRandomCharacter(
                            _rnd, cd, worldFragment, _seed);
                        if (characterResult.Value is None)
                        {
                            spawnStatus.InCreation--;
                        }
                        else
                        {
                            _engine.QueueEntitySetupAction(CharacterCreator.EntityName,
                                e =>
                                {
                                    characterResult.AsT1(e);
                                    spawnStatus.InCreation--;
                                });
                            ++_seed;
                        }
                    }
                }
                else
                {
                    /*
                     * I don't know why we would have been called in the first place.
                     */
                }
            }
            catch (Exception e)
            {
                spawnStatus.InCreation--;
                Error($"Exception spawning character: {e}");
            }
        };
    }


    public void TerminateCharacters(List<(Index3, DefaultEcs.Entity)> listKills)
    {
        List<SpawnStatus> listSpawnStatus = new(listKills.Count);
        
        foreach (var kill in listKills)
        {
            _findSpawnStatus(kill.Item1, out var spawnStatus);
            listSpawnStatus.Add(spawnStatus);
            spawnStatus.IsDying++;
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


    private void _onCitizenPropertiesChanged(engine.news.Event ev)
    {
        _citizenPerStreetPoint = (float) engine.Props.Get($"{_spawnCitizenProperties}.citizenPerStreetPoint", 6f);
        _minCitizenFactor = (float) engine.Props.Get($"{_spawnCitizenProperties}.minCitizenFactor", 1f);
        _maxCitizenFactor = (float) engine.Props.Get($"{_spawnCitizenProperties}.maxCitizenFactor", 2f);
        _areParametersDirty = true;
    }
    

    public SpawnOperator()
    {
        _citizenPerStreetPoint = (float) engine.Props.Find($"{_spawnCitizenProperties}.citizenPerStreetPoint", 6f);
        _minCitizenFactor = (float) engine.Props.Find($"{_spawnCitizenProperties}.minCitizenFactor", 1f);
        _maxCitizenFactor = (float) engine.Props.Find($"{_spawnCitizenProperties}.maxCitizenFactor", 2f);
        _onCitizenPropertiesChanged(new Event("",""));
        I.Get<SubscriptionManager>().Subscribe(
            $"{PropertyEvent.PROPERTY_CHANGED}.{_spawnCitizenProperties}",
            _onCitizenPropertiesChanged);
    }
}