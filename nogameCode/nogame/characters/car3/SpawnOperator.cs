using System.Collections.Generic;
using engine;
using engine.behave;
using engine.geom;
using engine.joyce;
using engine.world;
using FbxSharp;

namespace nogame.characters.car3;


/**
 * Spawn operator for the omnipresent cars. 
 */
public class SpawnOperator : ISpawnOperator
{
    private object _lo = new();
    private engine.geom.AABB _aabb = new();
    private SpawnStatus _spawnStatus = new();
    
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
        return _spawnStatus;
    }
    

    public void SpawnCharacter(System.Type behaviorType, in Index3 idxFragment, PerFragmentStats perFragmentStats)
    {
        
    }
}