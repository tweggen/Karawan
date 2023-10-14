using System;
using System.Collections.Generic;
using DefaultEcs;
using Splash.components;
using static engine.Logger;

namespace Splash;

public class CameraManager : IDisposable
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
    private readonly IThreeD _threeD;
    private readonly Dictionary<engine.joyce.Renderbuffer, Resource<ARenderbufferEntry>> _renderbufferResources;


    private void _unloadRenderbuffer(engine.joyce.Renderbuffer jRenderbuffer, Resource<ARenderbufferEntry> renderbufferEntry)
    {
        _threeD.UnloadRenderbuffer(renderbufferEntry.Value);
    }


    private ARenderbufferEntry _loadRenderbuffer(in engine.joyce.Renderbuffer jRenderbuffer)
    {
        return _threeD.CreateRenderbuffer(jRenderbuffer);
    }


    private void _onAdded(in Entity entity,
        in PfRenderbuffer value)
    {
        _add(entity, value);
    }


    private void _onChanged(in Entity entity,
        in PfRenderbuffer oldValue,
        in PfRenderbuffer newValue)
    {
        _add(entity, newValue);
        _remove(entity, oldValue);
    }

    private void _onRemoved(in Entity entity, in PfRenderbuffer value)
    {
        _remove(entity, value);
    }


    private void _add(in Entity entity, in PfRenderbuffer value)
    {
        Resource<ARenderbufferEntry> renderbufferResource;
        engine.joyce.Renderbuffer jRenderbuffer = value.Renderbuffer;
        if (!_renderbufferResources.TryGetValue(jRenderbuffer, out renderbufferResource))
        {
            try
            {
                ARenderbufferEntry aRenderbufferEntry = _loadRenderbuffer(jRenderbuffer);
                renderbufferResource = new Resource<ARenderbufferEntry>(aRenderbufferEntry);
                _renderbufferResources.Add(jRenderbuffer, renderbufferResource);
            }
            catch (Exception e)
            {
                Error("Exception loading mesh: {e}");
            }
        }

        if (null != renderbufferResource)
        {
            renderbufferResource.AddReference();
        }
    }


    private void _remove(in Entity entity, in PfRenderbuffer value)
    {
        Resource<ARenderbufferEntry> renderbufferResource;
        engine.joyce.Renderbuffer jRenderbuffer = value.RenderbufferEntry.JRenderbuffer;
        if (!_renderbufferResources.TryGetValue(jRenderbuffer, out renderbufferResource))
        {
            Error($"Unknown mesh to unreference.");
        }
        else
        {
            if (renderbufferResource.RemoveReference())
            {
                try
                {
                    _unloadRenderbuffer(jRenderbuffer, renderbufferResource);
                }
                finally
                {
                    _renderbufferResources.Remove(jRenderbuffer);
                }
            }
        }
    }
    

    /**
     * If the user replaces the new instance3 specifying the
     * mesh to use, we remove the pre-compiled PfInstance.
     */
    private void _onChanged(in DefaultEcs.Entity entity,
        in engine.joyce.components.Camera3 cOldCamera,
        in engine.joyce.components.Camera3 cNewCamera)
    {
        entity.Remove<ARenderbufferEntry>();
    }


    /**
     * If the user removes the new instance3 specifying the
     * mesh to use, we remove the pre-compiled PfInstance.
     */
    private void _onRemoved(in DefaultEcs.Entity entity,
        in engine.joyce.components.Camera3 cOldCamera)
    {
        entity.Remove<ARenderbufferEntry>();
    }

    
    public IDisposable Manage(World world)
    {
        IEnumerable<IDisposable> GetSubscriptions(World w)
        {
            yield return w.SubscribeEntityComponentAdded<components.PfRenderbuffer>(_onAdded);
            yield return w.SubscribeEntityComponentChanged<components.PfRenderbuffer>(_onChanged);
            yield return w.SubscribeEntityComponentRemoved<components.PfRenderbuffer>(_onRemoved);
            yield return w.SubscribeEntityComponentChanged<engine.joyce.components.Camera3>(_onChanged);
            yield return w.SubscribeEntityComponentRemoved<engine.joyce.components.Camera3>(_onRemoved);
        }

        if (null == world)
        {
            ErrorThrow("world must not be null.", (m) => new ArgumentException(m));
        }
        
        int nInitialEntites = 0;
        var entities = world.GetEntities().With<components.PfRenderbuffer>().AsEnumerable();
        foreach (DefaultEcs.Entity entity in entities)
        {
            _onAdded(entity, entity.Get<Splash.components.PfRenderbuffer>());
            ++nInitialEntites;
        }
        
        return GetSubscriptions(world).Merge();
    }


    public void Dispose()
    {
        GC.SuppressFinalize(this);

        foreach (KeyValuePair<engine.joyce.Renderbuffer, Resource<ARenderbufferEntry>> pair in _renderbufferResources)
        {
            _unloadRenderbuffer(pair.Key, pair.Value);
        }

        _renderbufferResources.Clear();
    }


    public CameraManager(in IThreeD threeD)
    {
        _lo = new object();
        _threeD = threeD;
        _renderbufferResources = new Dictionary<engine.joyce.Renderbuffer, Resource<ARenderbufferEntry>>();
    }
}