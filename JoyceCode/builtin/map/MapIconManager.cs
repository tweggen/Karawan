using System;
using System.Collections.Generic;
using DefaultEcs;
using engine.joyce;
using static engine.Logger;

namespace Splash;

/**
 * Compile and destroy meshes from MapIcon components.
 */
public class MapIconManager : IDisposable
{
    private sealed class Resource<ValueType>
    {
        public readonly ValueType Value;

        private int _referencesCount = 0;

        public Resource(ValueType value)
        {
            Value = value;
        }

        public void AddReference() => ++_referencesCount;

        public bool RemoveReference() => (--_referencesCount) == 0;
    }


    private readonly object _lo;
    private readonly Dictionary<engine.world.components.MapIcon, Resource<InstanceDesc>> _instanceDescResources;


    private void _unloadInstanceDesc(engine.world.components.MapIcon mapIcon, Resource<InstanceDesc> instanceDescEntry)
    {
        // TXWTODO: Do me.
    }


    private InstanceDesc _loadInstanceDesc(in engine.world.components.MapIcon jMapIcon)
    {
        // TXWTODO: Write me
        return null;
    }


    private void _onAdded(in Entity entity, in engine.world.components.MapIcon value)
    {
        _add(entity, value);
    }


    private void _onChanged(in Entity entity,
        in engine.world.components.MapIcon oldValue,
        in engine.world.components.MapIcon newValue)
    {
        _remove(entity, oldValue);
        _add(entity, newValue);
    }

    private void _onRemoved(in Entity entity, in engine.world.components.MapIcon value)
    {
        _remove(entity, value);
    }


    private void _add(in Entity entity, in engine.world.components.MapIcon value)
    {
        Resource<InstanceDesc> instanceDescResource;
        engine.world.components.MapIcon mapIcon = value;
        if (!_instanceDescResources.TryGetValue(mapIcon, out instanceDescResource))
        {
            try
            {
                InstanceDesc jInstanceDesc = _loadInstanceDesc(mapIcon);
                instanceDescResource = new Resource<InstanceDesc>(jInstanceDesc);
                _instanceDescResources.Add(mapIcon, instanceDescResource);
            }
            catch (Exception e)
            {
                Error("Exception creating mapIcon mesh: {e}");
            }
        }

        if (null != instanceDescResource)
        {
            instanceDescResource.AddReference();
            entity.Set(new engine.joyce.components.Instance3(instanceDescResource.Value));
        }
    }


    private void _remove(in Entity entity, in engine.world.components.MapIcon value)
    {
        Resource<InstanceDesc> instanceDescResource;
        if (!_instanceDescResources.TryGetValue(value, out instanceDescResource))
        {
            Error($"Unknown mapIcon to unreference.");
        }
        else
        {
            if (instanceDescResource.RemoveReference())
            {
                try
                {
                    _unloadInstanceDesc(value, instanceDescResource);
                }
                finally
                {
                    _instanceDescResources.Remove(value);
                }
            }
        }

        if (entity.Has<engine.joyce.components.Instance3>())
        {
            entity.Remove<engine.joyce.components.Instance3>();
        }
    }


    public IDisposable Manage(World world)
    {
        IEnumerable<IDisposable> GetSubscriptions(World w)
        {
            yield return w.SubscribeEntityComponentAdded<engine.world.components.MapIcon>(_onAdded);
            yield return w.SubscribeEntityComponentChanged<engine.world.components.MapIcon>(_onChanged);
            yield return w.SubscribeEntityComponentRemoved<engine.world.components.MapIcon>(_onRemoved);
        }

        if (null == world)
        {
            ErrorThrow("world must not be null.", (m) => new ArgumentException(m));
        }
        
        int nInitialEntites = 0;
        var entities = world.GetEntities().With<engine.world.components.MapIcon>().AsEnumerable();
        foreach (DefaultEcs.Entity entity in entities)
        {
            _onAdded(entity, entity.Get<engine.world.components.MapIcon>());
            ++nInitialEntites;
        }
        
        return GetSubscriptions(world).Merge();
    }


    public void Dispose()
    {
        GC.SuppressFinalize(this);

        foreach (KeyValuePair<engine.world.components.MapIcon, Resource<InstanceDesc>> pair in _instanceDescResources)
        {
            _unloadInstanceDesc(pair.Key, pair.Value);
        }

        _instanceDescResources.Clear();
    }


    public MapIconManager()
    {
        _lo = new object();
        _instanceDescResources = new Dictionary<engine.world.components.MapIcon, Resource<InstanceDesc>>();
    }
}