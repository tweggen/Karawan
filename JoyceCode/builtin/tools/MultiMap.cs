#if false
using System;
using System.Collections;
using System.Collections.Generic;

namespace Joyce.builtin.tools;

public class MultiMap<K,V> : System.Collections.Generic.IDictionary<K,V>
{
    private SortedDictionary<K, List<V>> _dictionary = new();
    private int _count = 0;
    private bool _isReadOnly = false;
    
    public bool ContainsKey(K key)
    {
        return _dictionary.ContainsKey(key);
    }

    
    public bool Remove(K key)
    {
        throw new System.NotImplementedException();
    }

    
    public bool TryGetValue(K key, out V value)
    {
        if (_dictionary.TryGetValue(key, out List<V> list))
        {
            if (list.Count > 0)
            {
                value = list[0];
                return true;
            }
            else
            {
                value = default;
            }
        }
        else
        {
            value = default;
        }

        return false;
    }

    
    public V this[K key]
    {
        get {
            if (TryGetValue(key, out var value))
            {
                return value;
            }
            else
            {
                throw new ArgumentException("key not found.");
            }
        }
        
        set {
            
        }
    }

    public ICollection<K> Keys { get; }
    public ICollection<V> Values { get; }

    public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
    {
        throw new System.NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Add(KeyValuePair<K, V> item)
    {
        throw new System.NotImplementedException();
    }

    public void Clear()
    {
        throw new System.NotImplementedException();
    }

    public bool Contains(KeyValuePair<K, V> item)
    {
        throw new System.NotImplementedException();
    }

    public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
    {
        throw new System.NotImplementedException();
    }

    public bool Remove(KeyValuePair<K, V> item)
    {
        throw new System.NotImplementedException();
    }

    public int Count { get => _count; }
    public bool IsReadOnly { get => _isReadOnly; }
}
#endif