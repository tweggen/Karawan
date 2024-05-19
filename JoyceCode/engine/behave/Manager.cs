using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine.behave;

public class Manager
{
    private engine.Engine _engine;

    private IDisposable? _subscriptions;

    public void _onBehaviorRemoved(in DefaultEcs.Entity entity,
        in components.Behavior cOldBehavior)
    {
        var oldProvider = cOldBehavior.Provider;
        if (oldProvider != null)
        {
            oldProvider.OnDetach(entity);
        }
    }
    

    public void _onNearbyRemoved(in DefaultEcs.Entity entity,
        in components.Nearby cOldNearby)
    {
        var oldProvider = cOldNearby.Provider;
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
                if (null != oldProvider)
                {
                    newProvider.Sync(entity);
                }
            }
        }
    }
    

    public void _onNearbyChanged(in DefaultEcs.Entity entity,
        in components.Nearby cOldNearby,
        in components.Nearby cNewNearby)
    {
        var oldProvider = cOldNearby.Provider;
        var newProvider = cNewNearby.Provider;

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
    

    public void _onBehaviorAdded(in DefaultEcs.Entity entity, 
        in components.Behavior cNewBehavior)
    {
        var newProvider = cNewBehavior.Provider;
        if (newProvider != null)
        {
            newProvider.OnAttach(_engine, entity);
        }
    }


    public void _onNearbyAdded(in DefaultEcs.Entity entity, 
        in components.Nearby cNewNearby)
    {
        var newProvider = cNewNearby.Provider;
        if (newProvider != null)
        {
            newProvider.OnAttach(_engine, entity);
        }
    }


    public void Stop()
    {
        if (_subscriptions != null)
        {
            _subscriptions.Dispose();
            _subscriptions = null;
        }
    }
    
    
    public void Manage(in engine.Engine engine)
    {
        _engine = engine;
        
        IEnumerable<IDisposable> GetSubscriptions(DefaultEcs.World w)
        {
            yield return w.SubscribeEntityComponentAdded<components.Behavior>(_onBehaviorAdded);
            yield return w.SubscribeEntityComponentAdded<components.Nearby>(_onNearbyAdded);
            yield return w.SubscribeEntityComponentChanged<components.Behavior>(_onBehaviorChanged);
            yield return w.SubscribeEntityComponentChanged<components.Nearby>(_onNearbyChanged);
            yield return w.SubscribeEntityComponentRemoved<components.Behavior>(_onBehaviorRemoved);
            yield return w.SubscribeEntityComponentRemoved<components.Nearby>(_onNearbyRemoved);
        }
        DefaultEcs.World world = _engine.GetEcsWorld();
        if (null == world)
        {
            ErrorThrow("world must not be null.", (m) => new ArgumentException(m));
        }

        {
            var entities = world.GetEntities().With<components.Behavior>().AsEnumerable();
            foreach (DefaultEcs.Entity entity in entities)
            {
                _onBehaviorAdded(entity, entity.Get<components.Behavior>());
            }
        }
        {
            var entities = world.GetEntities().With<components.Nearby>().AsEnumerable();
            foreach (DefaultEcs.Entity entity in entities)
            {
                _onNearbyAdded(entity, entity.Get<components.Nearby>());
            }
        }

        _subscriptions = GetSubscriptions(world).Merge();
    }


    public void Dispose()
    {
        Stop();
        _engine = null;
    }
}