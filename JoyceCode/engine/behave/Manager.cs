using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine.behave;

public class Manager : AComponentManager<components.Behavior>
{
    internal override void _onComponentRemoved(
        in DefaultEcs.Entity entity,
        in components.Behavior cOldBehavior)
    {
        var oldProvider = cOldBehavior.Provider;
        if (oldProvider != null)
        {
            oldProvider.OnDetach(entity);
        }
    }
    

    internal override void _onComponentChanged(
        in DefaultEcs.Entity entity,
        in components.Behavior cOldBehavior,
        in components.Behavior cNewBehavior)
    {
        var oldProvider = cOldBehavior.Provider;
        var newProvider = cNewBehavior.Provider;

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
    

    internal override void _onComponentAdded(
        in DefaultEcs.Entity entity, 
        in components.Behavior cNewBehavior)
    {
        var newProvider = cNewBehavior.Provider;
        if (newProvider != null)
        {
            newProvider.OnAttach(_engine, entity);
        }
    }


}