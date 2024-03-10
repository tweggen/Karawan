using System;
using engine.behave;
using engine.joyce;

namespace nogame.characters.car3;

public class SpawnOperator : ISpawnOperator
{
    private object _lo = new();
    private SpawnStatus _spawnStatus = new();
    
    public System.Type BehaviorType
    {
        get => typeof(Behavior);
    }

    
    public SpawnStatus SpawnStatus
    {
        get
        {
            lock (_lo)
            {
                return _spawnStatus;
            }
        }
    }
    

    public void SpawnCharacter(Type behaviorType, in Index3 idxFragment, PerFragmentStats perFragmentStats)
    {
        
    }
}