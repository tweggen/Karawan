using DefaultEcs;
using engine;

namespace engine.behave;

public class StrategyManager : AComponentWatcher<components.Strategy>
{
    protected override void _onComponentRemoved(
        in DefaultEcs.Entity entity,
        in components.Strategy cOldStrategy)
    {
        var oldProvider = cOldStrategy.EntityStrategy;
        if (oldProvider != null)
        {
            IStrategyPart? sp = oldProvider as IStrategyPart;
            if (sp != null) sp.OnExit();

            oldProvider.OnDetach(entity);
        }
    }
    

    protected override void _onComponentChanged(
        in DefaultEcs.Entity entity,
        in components.Strategy cOldStrategy,
        in components.Strategy cNewBehavior)
    {
        var oldProvider = cOldStrategy.EntityStrategy;
        var newProvider = cNewBehavior.EntityStrategy;

        if (oldProvider == newProvider)
        {
            if (null == oldProvider)
            {
                return;
            }
            else
            {
                newProvider.Sync(entity);
            }
        }
        else
        {
            if (null != oldProvider)
            {
                IStrategyPart? sp = newProvider as IStrategyPart;
                if (sp != null) sp.OnExit();

                oldProvider.OnDetach(entity);
            }

            if (null != newProvider)
            {
                newProvider.OnAttach(_engine, entity);
                if (null != oldProvider)
                {
                    newProvider.Sync(entity);
                }
                IStrategyPart? sp = newProvider as IStrategyPart;
                if (sp != null) sp.OnEnter();
            }
        }
    }
    

    protected override void _onComponentAdded(
        in DefaultEcs.Entity entity, 
        in components.Strategy cNewStrategy)
    {
        var newProvider = cNewStrategy.EntityStrategy;
        if (newProvider != null)
        {
            newProvider.OnAttach(_engine, entity);
            IStrategyPart? sp = newProvider as IStrategyPart;
            if (sp != null) sp.OnEnter();
        }
    }
}