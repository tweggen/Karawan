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
public sealed class Implementations
{
    private static readonly Implementations _singleton = new Implementations();
    
    private object _lo = new();
    private Dictionary<Type, InstanceEntry> _mapInstances = new();

    public void RegisterFactory<T>(Func<Object> factory)
    {
        lock (_lo)
        {
            if (_mapInstances.TryGetValue(typeof(T), out _))
            {
                ErrorThrow($"Already registered {typeof(T).FullName}.", (m) => throw new InvalidOperationException(m));
            }
            InstanceEntry instanceEntry = new()
            {
                Lock = new(),
                InterfaceType = typeof(T),
                FactoryFunction = factory,
                Instance = null
            };

            _mapInstances[typeof(T)] = instanceEntry;
        }
    }


    public static void Register<T>(Func<Object> factory)
    {
        Instance.RegisterFactory<T>(factory);
    }
    
    
    public T GetInstance<T>()
    {
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
                return (T) instanceEntry.Instance;
            }

            if (null == instanceEntry.FactoryFunction)
            {
                ErrorThrow($"No factory found for type {typeof(T).FullName}", (m) => new InvalidOperationException(m));
            }

            instanceEntry.Instance = instanceEntry.FactoryFunction();

            return (T)instanceEntry.Instance;
        }


    }


    public static T Get<T>()
    {
        return Instance.GetInstance<T>();
    }


    static Implementations()
    {}

    
    private Implementations()
    {}


    public static Implementations Instance
    {
        get
        {
            return _singleton;
        }
    }
}