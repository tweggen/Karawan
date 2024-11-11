using System;
using System.Collections.Generic;
using System.Linq;
using static engine.Logger;

namespace engine;


internal class ObjectEntry<K, T>
{
    public object Lock;
    public K Name;
    public Func<K, T>? FactoryFunction;
    public T Instance;
    public required bool HasInstance;
}


public class ObjectFactory<K, T> : IDisposable where K : IComparable
{
    private object _lo = new();
    private SortedDictionary<K, ObjectEntry<K, T> > _mapObjects = new();


    public void RemoveIf(Func<K, T, bool> predicate)
    {
        lock (_lo)
        {
            List<K> deadkeys = _mapObjects
                .Where(kvp =>
                {
                    if (kvp.Value.Instance != null)
                    {
                        return predicate(kvp.Key, kvp.Value.Instance);
                    }
                    else
                    {
                        return false;
                    }
                })
                .Select(kvp => kvp.Key)
                .ToList();
            foreach (var key in deadkeys)
            {
                _mapObjects.Remove(key);
            }
        }

    }
    
    public T FindAdd(K key, T referenceObject)
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
                Instance = referenceObject,
                HasInstance = true
            };
            _mapObjects[key] = instanceEntry;
            return referenceObject;
        }
    }


    public T FindAdd(K key, Func<K, T> referenceObjectFactory)
    {
        lock (_lo)
        {
            if (_mapObjects.TryGetValue(key, out var me))
            {
                return me.Instance;
            }

            T referenceObject = referenceObjectFactory(key);
            ObjectEntry<K, T> instanceEntry = new()
            {
                Lock = new(),
                Name = key, //referenceObject.Name,
                FactoryFunction = null,
                Instance = referenceObject,
                HasInstance = true
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
                Instance = default,
                HasInstance = false
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
            if (instanceEntry.HasInstance)
            {
                return instanceEntry.Instance;
            }

            if (null == instanceEntry.FactoryFunction)
            {
                ErrorThrow($"No factory found for type {name}", (m) => new InvalidOperationException(m));
            }

            instanceEntry.Instance = instanceEntry.FactoryFunction(name);
            instanceEntry.HasInstance = true;

            return instanceEntry.Instance;
        }
    }


    public void Dispose()
    {
        if (typeof(IDisposable).IsAssignableFrom(typeof(T)))
        {
            foreach (var kvp in _mapObjects)
            {
                var oe = kvp.Value;
                if (oe.HasInstance)
                {
                    (oe.Instance as IDisposable).Dispose();
                }
            }
        }
    }
}