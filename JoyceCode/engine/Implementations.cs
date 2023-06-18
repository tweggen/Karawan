using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine;

internal class InstanceEntry
{
    public object Lock;
    public Type InterfaceType;
    public Func<Object> FactoryFunction;
    public Object Instance;
}

/**
 * Provides a global registry of implementations to particular instances.
 */
public class Implementations
{
    private object _lo = new();
    private SortedDictionary<Type, InstanceEntry> _mapInstances = new();

    public void RegisterFactory<T>(Func<Object> factory)
    {
        InstanceEntry instanceEntry = new()
        {
            Lock = new(),
            InterfaceType = typeof(T),
            FactoryFunction = factory,
            Instance = null
        };
    }

#if false
    public T Get<class T>()
    {
        return null;
#if false
        InstanceEntry instanceEntry;
        lock (_lo)
        {
            if (!_mapInstances.TryGetValue(typeof(T), out instanceEntry))
            {
                ErrorThrow($"Requested unknown type {typeof(T).FullName}", (m)=>new ArgumentException(m));
            }

            if (null == instanceEntry)
            {
                ErrorThrow($"Instance entry for type {typeof(T).FullName} was null.", (m)=>new InvalidOperationException(m));
            }
        }

        lock (instanceEntry.Lock)
        {
            if (null != instanceEntry.Instance)
            {
                return instanceEntry.Instance;
            }
        }
#endif
    }
#endif
    
    public Implementations()
    {
        
    }
}