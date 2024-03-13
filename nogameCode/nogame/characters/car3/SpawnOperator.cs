using System.Numerics;
using DefaultEcs;
using engine;
using engine.behave;
using engine.joyce;
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
    private int _inCreation = 0;
    private engine.world.Loader _loader = I.Get<engine.world.MetaGen>().Loader;
    
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


    public SpawnStatus GetFragmentSpawnStatus(System.Type behaviorType, in Index3 idxFragment)
    {
        /*
         * Read the probability for this fragment from the cluster heat map,
         * return an appropriate spawnStatus.
         */
        float density = _clusterHeatMap.GetDensity(idxFragment);
        
        /*_
         * Note that it is technically wrong to return the number of characters in creation
         * in total. However, this only would prevent more characters compared to the offset.
         */
        return new SpawnStatus()
        {
            MinCharacters = (ushort)(20f * density),
            MaxCharacters = (ushort)(5f * density),
            InCreation = (ushort) _inCreation
        };
    }
    

    public void SpawnCharacter(System.Type behaviorType, in Index3 idxFragment, PerFragmentStats perFragmentStats)
    {
        ClusterDesc cd = _clusterHeatMap.GetClusterDesc(idxFragment);
        if (null == cd)
        {
            /*
             * I don't know why we would have been called in the first place.
             */
            
            return;
        }

        engine.world.Fragment worldFragment;
        if (_loader.TryGetFragment(idxFragment, out worldFragment))
        {
            var chosenStreetPoint = GenerateCharacterOperator.ChooseStreetPoint(cd, worldFragment);
            GenerateCharacterOperator.GenerateCharacter(cd, worldFragment);
        }
    }
}