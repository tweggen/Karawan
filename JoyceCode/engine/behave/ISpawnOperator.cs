using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using engine.behave.components;
using engine.joyce;

namespace engine.behave;


public struct SpawnInfo
{
    public DefaultEcs.Entity Entity; 
    public Vector3 Position;
    public Behavior CBehavior;
}


/**
 * Operator implementation to populate fragments with behaved objects.
 */
public interface ISpawnOperator
{
    geom.AABB AABB { get; }
    
    
    /**
     * The behavior type we shall control.
     */
    Type BehaviorType { get;  }

    
    /**
     * Return the requirements for the given fragment. This method should
     * be rather fast.
     */
    SpawnStatus GetFragmentSpawnStatus(Type behaviorType, in Index3 idxFragment);

    
    /**
     * Purge all cached information about the given fragment.
     * This is called when a fragment is unloaded.
     */
    public void PurgeFragment(in Index3 idxFragment);
    
    
    /**
     * Trigger creation of a new behavior.
     *
     * The caller expects that pending yet unfinished creation processes are reflected in the
     * InCreation member of SpawnStatus.
     *
     * @param behavior
     *    The type of the behavior we need to spawn.
     * @param idxFragment
     *    The fragment where we shall spawn the character.
     * @param perFragmentStats
     *    The result of counting the characters.
     */
    public Action SpawnCharacter(System.Type behaviorType, Index3 idxFragment, PerFragmentStats perFragmentStats);

    public void TerminateCharacters(List<(Index3,DefaultEcs.Entity)> listKills);
}