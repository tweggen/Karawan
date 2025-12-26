using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine.behave;

public class BehaviorManager : AComponentWatcher<components.Behavior>
{
    protected override void _onComponentRemoved(
        in DefaultEcs.Entity entity,
        in components.Behavior cOldStrategy)
    {
        var oldProvider = cOldStrategy.Provider;
        if (oldProvider != null)
        {
            oldProvider.OnDetach(entity);
        }
    }
    

    protected override void _onComponentChanged(
        in DefaultEcs.Entity entity,
        in components.Behavior cOldStrategy,
        in components.Behavior cNewStrategy)
    {
        var oldProvider = cOldStrategy.Provider;
        var newProvider = cNewStrategy.Provider;

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
                oldProvider.OnDetach(entity);
            }

            if (null != newProvider)
            {
                newProvider.OnAttach(_engine, entity);
                if (null != oldProvider)
                {
                    newProvider.Sync(entity);
                }
            }
        }
    }
    

    protected override void _onComponentAdded(
        in DefaultEcs.Entity entity, 
        in components.Behavior cNewStrategy)
    {
        var newProvider = cNewStrategy.Provider;
        if (newProvider != null)
        {
            newProvider.OnAttach(_engine, entity);
        }
    }
}