using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using engine;
using engine.behave;
using engine.joyce;
using engine.news;
using engine.streets;
using engine.world;
using static engine.Logger;

namespace nogame.characters.car3;


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
    private builtin.tools.RandomSource _rnd = new("car3");
    
    private const string _spawnCarsProperties = "nogame.characters.car3";
    private float _carsPerStreetPoint = 1f;
    private float _minCarsFactor = 1f;
    private float _maxCarsFactor = 2f;
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
        get => typeof(nogame.characters.car3.Behavior);
    }


    private Dictionary<Index3, SpawnStatus> _mapFragmentStatus = new();

    private void _findSpawnStatus_nl(in Index3 idxFragment, out SpawnStatus spawnStatus)
    {
        if (_mapFragmentStatus.TryGetValue(idxFragment, out spawnStatus))
        {
            return;
        }
        
        /*
         * Read the probability for this fragment from the cluster heat map,
         * return an appropriate spawnStatus.
         */
        // float density = _clusterHeatMap.GetDensity(idxFragment);
        
        var cd = _clusterHeatMap.GetClusterDesc(idxFragment);
        if (null == cd)
        {
            spawnStatus = new()
            {
                MinCharacters = 0,
                MaxCharacters = 0,
                InCreation = 0
            };
        }
        else
        {

            /*
             * Now, before being able to estimate the number of cars, let's see
             * how many different possible spawning points we have.
             */
            var ilistSPs = cd.GetStreetPointsInFragment(idxFragment);

            // TXWTODO: Filter, so that we have meaningful spawning points.
            float nMaxSpawns = ilistSPs.Count * _carsPerStreetPoint;

            /*_
             * Note that it is technically wrong to return the number of characters in creation
             * in total. However, this only would prevent more characters compared to the offset.
             */
            spawnStatus = new()
            {
                MinCharacters = (ushort)(_minCarsFactor * nMaxSpawns),
                MaxCharacters = (ushort)(_maxCarsFactor * nMaxSpawns),
                InCreation = (ushort)0
            };
        }

        _mapFragmentStatus[idxFragment] = spawnStatus;
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
                if (null == cd)
                {
                    /*
                     * I don't know why we would have been called in the first place.
                     */
                }
                else
                {
                    engine.world.Fragment worldFragment;
                    if (_loader.TryGetFragment(idxFragment, out worldFragment))
                    {
                        StreetPoint? chosenStreetPoint = CharacterCreator.ChooseStreetPoint(_rnd,  worldFragment, cd);
                        if (chosenStreetPoint != null)
                        {
                            var actCreateEntity = await CharacterCreator.GenerateRandomCharacter(
                                _rnd, cd, worldFragment, chosenStreetPoint, _seed);
                            _engine.QueueEntitySetupAction(CharacterCreator.EntityName,
                                e =>
                                {
                                    actCreateEntity(e);
                                    if (spawnStatus.InCreation == 0)
                                    {
                                        int a = 1;
                                    }
                                    spawnStatus.InCreation--;
                                });
                            ++_seed;
                        }
                        else
                        {
                            spawnStatus.InCreation--;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                spawnStatus.InCreation--;
                Error($"Exception spawning character: {e}");
            }
        };
    }


    private void _onCarsPropertiesChanged(engine.news.Event ev)
    {
        _carsPerStreetPoint = (float) engine.Props.Get($"{_spawnCarsProperties}.carsPerStreetPoint", 6f);
        _minCarsFactor = (float) engine.Props.Get($"{_spawnCarsProperties}.minCarsFactor", 1f);
        _maxCarsFactor = (float) engine.Props.Get($"{_spawnCarsProperties}.maxCarsFactor", 2f);
        _areParametersDirty = true;
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


    public SpawnOperator()
    {
        _carsPerStreetPoint = (float) engine.Props.Find($"{_spawnCarsProperties}.carsPerStreetPoint", 1f);
        _minCarsFactor = (float) engine.Props.Find($"{_spawnCarsProperties}.minCarsFactor", 1f);
        _maxCarsFactor = (float) engine.Props.Find($"{_spawnCarsProperties}.maxCarsFactor", 2f);
        _onCarsPropertiesChanged(new engine.news.Event("",""));
        I.Get<SubscriptionManager>().Subscribe(
            $"{PropertyEvent.PROPERTY_CHANGED}.{_spawnCarsProperties}",
            _onCarsPropertiesChanged);
    }
}