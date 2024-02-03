using System;
using System.Collections.Generic;
using System.Security.Cryptography;
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
public sealed class I
{
    private static readonly I _singleton = new I();
    
    private object _lo = new();
    private Dictionary<Type, InstanceEntry> _mapInstances = new();


    public IEnumerable<Type> GetTypes()
    {
        lock (_lo)
        {
            return new List<Type>(_mapInstances.Keys);
        }
    }


    public void RegisterFactory(Type type, Func<Object> factory)
    {
        lock (_lo)
        {
            if (_mapInstances.TryGetValue(type, out _))
            {
                ErrorThrow($"Already registered {type.FullName}.", (m) => throw new InvalidOperationException(m));
            }
            InstanceEntry instanceEntry = new()
            {
                Lock = new(),
                InterfaceType = type,
                FactoryFunction = factory,
                Instance = null
            };

            _mapInstances[type] = instanceEntry;
        }
    }
    
    
    public void RegisterFactory<T>(Func<Object> factory)
    {
        RegisterFactory(typeof(T), factory);
    }


    public static void Register<T>(Func<Object> factory)
    {
        Instance.RegisterFactory<T>(factory);
    }


    private Object _getInstance(InstanceEntry instanceEntry)
    {
        lock (instanceEntry.Lock)
        {
            if (null != instanceEntry.Instance)
            {
                return instanceEntry.Instance;
            }

            if (null == instanceEntry.FactoryFunction)
            {
                ErrorThrow($"No factory found for type {instanceEntry.InterfaceType.FullName}", (m) => new InvalidOperationException(m));
            }

            instanceEntry.Instance = instanceEntry.FactoryFunction();

            return instanceEntry.Instance;
        }
    }
    
    
    public object GetInstance(Type t)
    {
        InstanceEntry instanceEntry;
        lock (_lo)
        {
            if (!_mapInstances.TryGetValue(t, out instanceEntry))
            {
                ErrorThrow($"Requested unknown type {t.FullName}", (m)=>new ArgumentException(m));
            }

            if (null == instanceEntry)
            {
                ErrorThrow($"Instance entry for type {t.FullName} was null.", (m)=>new InvalidOperationException(m));
            }
        }

        return _getInstance(instanceEntry);
    }


    public T GetInstance<T>()
    {
        return (T)GetInstance(typeof(T));
    }



    public static T Get<T>()
    {
        return Instance.GetInstance<T>();
    }


    //static Implementations()
    //{}

    
    private I()
    {}


    public static I Instance
    {
        get
        {
            return _singleton;
        }
    }
}