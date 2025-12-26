using DefaultEcs;
using engine;
using engine.behave;
using engine.behave.strategies;

namespace nogame.characters.citizen;


/**
 * Strategy for citizen entities.
 *
 * Creates and owns the citizens behavior.
 */
public class EntityStrategy : IEntityStrategy
{
    private OneOfStrategy _strategyController = new();
    
    public void Sync(in Entity entity)
    {
        throw new System.NotImplementedException();
    }

    public void OnDetach(in Entity entity)
    {
        throw new System.NotImplementedException();
    }

    public void OnAttach(in Engine engine0, in Entity entity)
    {
        throw new System.NotImplementedException();
    }

    public EntityStrategy()
    {
        _strategyController = new()
        {
            Strategies = new()
            {
                { "walk", new WalkStrategy() },
                { "recover", new RecoverStrategy() }
            }
        };
    }
}