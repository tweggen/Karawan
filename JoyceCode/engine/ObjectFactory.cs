using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine;


internal class ObjectEntry<K, T> where T : class?
{
    public object Lock;
    public K Name;
    public Func<K, T>? FactoryFunction;
    public T Instance;
}


public class ObjectFactory<K, T> where T : class?
{
    private object _lo = new();
    private Dictionary<K, ObjectEntry<K, T> > _mapObjects = new();


    protected T FindAdd(K key, T referenceObject)
    {
        lock (_lo)
        {
            if (_mapObjects.TryGetValue(key, out var me))
            {
                return me.Instance;
            }

            ObjectEntry<K, T> instanceEntry = new()
            {
                Lock = new(),
                Name = key, //referenceObject.Name,
                FactoryFunction = null,
                Instance = referenceObject
            };
            _mapObjects[key] = instanceEntry;
            return referenceObject;
        }
    }


    public void RegisterFactory(K name, Func<K, T> factory)
    {
        lock (_lo)
        {
            if (_mapObjects.TryGetValue(name, out _))
            {
                return;
            }
            ObjectEntry<K, T> instanceEntry = new()
            {
                Lock = new(),
                Name = name,
                FactoryFunction = factory,
                Instance = null
            };

            _mapObjects[name] = instanceEntry;
        }
    }
    
    
    public T Get(K name)
    {
        ObjectEntry<K, T> instanceEntry;
        lock (_lo)
        {
            if (!_mapObjects.TryGetValue(name, out instanceEntry))
            {
                ErrorThrow($"Requested unknown object {name}.", (m)=>new ArgumentException(m));
            }

            if (null == instanceEntry)
            {
                ErrorThrow($"Material entry for type {name} was null.", (m)=>new InvalidOperationException(m));
            }
        }

        lock (instanceEntry.Lock)
        {
            if (null != instanceEntry.Instance)
            {
                return instanceEntry.Instance;
            }

            if (null == instanceEntry.FactoryFunction)
            {
                ErrorThrow($"No factory found for type {name}", (m) => new InvalidOperationException(m));
            }

            instanceEntry.Instance = instanceEntry.FactoryFunction(name);

            return instanceEntry.Instance;
        }
    }
}