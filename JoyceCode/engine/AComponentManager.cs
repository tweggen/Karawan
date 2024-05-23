using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine;

public abstract class AComponentManager<T> : IDisposable where T : struct 
{
    protected engine.Engine _engine;

    private IDisposable? _subscriptions;

    
    internal abstract void _onComponentRemoved(
        in DefaultEcs.Entity entity,
        in T cOldComponent);

    
    internal abstract void _onComponentChanged(
        in DefaultEcs.Entity entity,
        in T cOldComponent,
        in T cNewComponent);


    internal abstract void _onComponentAdded(
        in DefaultEcs.Entity entity,
        in T cNewComponent);


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
            yield return w.SubscribeEntityComponentAdded<T>(_onComponentAdded);
            yield return w.SubscribeEntityComponentChanged<T>(_onComponentChanged);
            yield return w.SubscribeEntityComponentRemoved<T>(_onComponentRemoved);
        }
        DefaultEcs.World world = _engine.GetEcsWorld();
        if (null == world)
        {
            ErrorThrow("world must not be null.", (m) => new ArgumentException(m));
        }

        {
            var entities = world.GetEntities().With<T>().AsEnumerable();
            foreach (DefaultEcs.Entity entity in entities)
            {
                _onComponentAdded(entity, entity.Get<T>());
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