using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using engine;
using engine.behave;
using engine.joyce;
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
        get => typeof(Behavior);
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
            float nMaxSpawns = ilistSPs.Count;

            /*_
             * Note that it is technically wrong to return the number of characters in creation
             * in total. However, this only would prevent more characters compared to the offset.
             */
            spawnStatus = new()
            {
                MinCharacters = (ushort)(0.2f * nMaxSpawns),
                MaxCharacters = (ushort)(0.4f * nMaxSpawns),
                InCreation = (ushort)0
            };
        }

        _mapFragmentStatus[idxFragment] = spawnStatus;
    }


    public SpawnStatus GetFragmentSpawnStatus(System.Type behaviorType, in Index3 idxFragment)
    {
        SpawnStatus spawnStatus;
        lock (_lo)
        {
            _findSpawnStatus_nl(idxFragment, out spawnStatus);
        }
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
    

    public void SpawnCharacter(System.Type behaviorType, Index3 idxFragment, PerFragmentStats perFragmentStats)
    {
        lock (_lo)
        {
            _findSpawnStatus_nl(idxFragment, out var spawnStatus);
            spawnStatus.InCreation++;
            _mapFragmentStatus[idxFragment] = spawnStatus;
        }

        Task.Run(async () =>
        {
            DefaultEcs.Entity eCharacter = default;

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
                        StreetPoint? chosenStreetPoint = GenerateCharacterOperator.ChooseStreetPoint(cd, worldFragment);
                        if (chosenStreetPoint != null)
                        {
                            eCharacter = await GenerateCharacterOperator.GenerateCharacter(
                                cd, worldFragment, chosenStreetPoint, _seed);
                            ++_seed;
                        }
                        else
                        {
                            lock (_lo)
                            {
                                /*
                                 * Act as if the thing still is in creation.
                                 */
                                _findSpawnStatus_nl(idxFragment, out var spawnStatus);
                                spawnStatus.Dead++;
                                _mapFragmentStatus[idxFragment] = spawnStatus;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Error($"Exception spawning character: {e}");
            }

            lock (_lo)
            {
                _findSpawnStatus_nl(idxFragment, out var spawnStatus);
                spawnStatus.InCreation--;
                _mapFragmentStatus[idxFragment] = spawnStatus;
            }

            return eCharacter;
        });
    }


    public void TerminateCharacters(PerBehaviorStats perBehaviorStats, int totalCharactersFound)
    {
        CameraInfo cami = _engine.CameraInfo;
        
        /*
         * bring the fragments into a descending order
         */
    }
}