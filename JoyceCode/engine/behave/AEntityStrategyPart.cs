
namespace engine.behave;

public abstract class AEntityStrategyPart : AStrategyPart, IEntityStrategy
{
    protected Engine _engine;
    protected DefaultEcs.Entity _entity;


    public virtual void Sync(in DefaultEcs.Entity entity)
    {
        /*
         * nothing to sync.
         */
    }

    
    public virtual void OnDetach(in DefaultEcs.Entity entity)
    {
        _entity = default;
    }


    public virtual void OnAttach(in Engine engine0, in DefaultEcs.Entity entity)
    {
        _engine = engine0;
        _entity = entity;
    }
}