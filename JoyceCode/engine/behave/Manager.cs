using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine.behave;

public class Manager
{
    private engine.Engine _engine;


    public void _onBehaviorRemoved(in DefaultEcs.Entity entity,
        in components.Behavior cOldBehavior)
    {
        var oldProvider = cOldBehavior.Provider;
        if (oldProvider != null)
        {
            oldProvider.OnDetach(entity);
        }
    }
    

    public void _onBehaviorChanged(in DefaultEcs.Entity entity,
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
                newProvider.Sync(entity);
            }
        }
    }
    

    public void _onBehaviorAdded(in DefaultEcs.Entity entity, 
        in components.Behavior cNewBehavior)
    {
        var newProvider = cNewBehavior.Provider;
        if (newProvider != null)
        {
            newProvider.OnAttach(_engine, entity);
        }
    }
    
    public IDisposable Manage(in engine.Engine engine)
    {
        _engine = engine;
        
        IEnumerable<IDisposable> GetSubscriptions(DefaultEcs.World w)
        {
            yield return w.SubscribeEntityComponentAdded<components.Behavior>(_onBehaviorAdded);
            yield return w.SubscribeEntityComponentChanged<components.Behavior>(_onBehaviorChanged);
            yield return w.SubscribeEntityComponentRemoved<components.Behavior>(_onBehaviorRemoved);
        }
        DefaultEcs.World world = _engine.GetEcsWorld();
        if (null == world)
        {
            ErrorThrow("world must not be null.", (m) => new ArgumentException(m));
        }
        
        
        var entities = world.GetEntities().With<components.Behavior>().AsEnumerable();
        foreach (DefaultEcs.Entity entity in entities)
        {
            _onBehaviorAdded(entity, entity.Get<components.Behavior>());
        }

        return GetSubscriptions(world).Merge();
    }
}