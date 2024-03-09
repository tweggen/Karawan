using engine.behave;

namespace engine.world;


/**
 * Operator implementation to populate fragments with behaved objects.
 */
public interface ISpawnOperator
{
    void ConsumeNewStatistics(IBehavior behavior, PerBehaviorStats perBehaviorStats);
}