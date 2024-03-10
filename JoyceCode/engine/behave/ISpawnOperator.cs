using System;
using engine.joyce;

namespace engine.behave;


public struct SpawnStatus
{
    /**
     * The minimum of characters per fragment.
     */
    public int MinCharacters;

    /**
     * The maximum of characters per fragment.
     */
    public int MaxCharacters;

    /**
     * The number of characters in creation.
     */
    public int InCreation;
}

/**
 * Operator implementation to populate fragments with behaved objects.
 */
public interface ISpawnOperator
{
    /**
     * The behavior type we shall control.
     */
    Type BehaviorType { get;  }
    
    SpawnStatus SpawnStatus { get; }
    
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
    void SpawnCharacter(Type behaviorType, in Index3 idxFragment, PerFragmentStats perFragmentStats);
}