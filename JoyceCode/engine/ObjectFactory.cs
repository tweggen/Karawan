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


/// <summary>
/// Thin IReadOnlyList wrapper that caches keys from a SortedDictionary once,
/// avoiding re-enumeration on repeated access.
/// </summary>
internal class KeyListView<K> : IReadOnlyList<K> where K : IComparable
{
    private readonly List<K> _cachedKeys;

    public KeyListView(IEnumerable<K> keys)
    {
        _cachedKeys = new List<K>(keys);
    }

    public K this[int index] => _cachedKeys[index];
    public int Count => _cachedKeys.Count;

    public IEnumerator<K> GetEnumerator() => _cachedKeys.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _cachedKeys.GetEnumerator();
}


public class ObjectFactory<K, T> : AModule, IDisposable where K : IComparable
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


    public void Add(K key, T obj)
    {
        lock (_lo)
        {
            if (_mapObjects.ContainsKey(key))
            {
                ErrorThrow<InvalidOperationException>($"Already contained object {key}");
                return;
            }
            ObjectEntry<K, T> instanceEntry = new()
            {
                Lock = new(),
                Name = key, //referenceObject.Name,
                FactoryFunction = null,
                Instance = obj,
                HasInstance = true
            };
            _mapObjects[key] = instanceEntry;
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


    public virtual void RegisterFactory(K name, Func<K, T> factory)
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


    public bool Has(K name)
    {
        lock (_lo)
        {
            return _mapObjects.ContainsKey(name);
        }
    }


    /// <summary>
    /// Get a readonly list of all keys in sorted order. Keys are cached at call time,
    /// so multiple calls may return different list instances if the registry changes,
    /// but individual list instances are immutable from the caller's perspective.
    /// </summary>
    public IReadOnlyList<K> GetKeys()
    {
        lock (_lo)
        {
            return new KeyListView<K>(_mapObjects.Keys);
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

    
    public override void Dispose()
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

        base.Dispose();
    }
}