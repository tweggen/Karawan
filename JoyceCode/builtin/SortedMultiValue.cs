using System;
using System.Collections;
using System.Collections.Generic;

namespace builtin;

public class SortedMultiValue<TKey, TValue> : IEnumerable<TValue>
{
    private SortedDictionary<TKey, List<TValue>> _data;

    private int _count;
    
    public int Count
    {
        get => _count;
    }
    
    public SortedMultiValue()
    {
        _data = new SortedDictionary<TKey, System.Collections.Generic.List<TValue>>();
        _count = 0;
    }

    public void Clear()
    {
        _count = 0;
        _data.Clear();
    }


    public TValue TakeFirst()
    {
        if (_count == 0)
        {
            throw new InvalidOperationException("list was empty.");
        }

        foreach (var kvp in _data)
        {
            if (kvp.Value.Count > 0)
            {
                var first = kvp.Value[0];
                kvp.Value.RemoveAt(0);
                return first;
            }
        }
        
        throw new InvalidOperationException("corrupt data structure.");
    }

    public void Add(TKey key, TValue value)
    {
        if (!_data.TryGetValue(key, out List<TValue> items))
        {
            items = new List<TValue>();
            _data.Add(key, items);
        }

        items.Add(value);
        _count++;
    }


    public void Remove(TKey key, TValue value)
    {
        if (!_data.TryGetValue(key, out List<TValue> items))
        {
            return;
        }

        if (items.Remove(value))
        {
            _count--;
        }
    }
    
    
    public IEnumerable<TValue> Get(TKey key)
    {
        if (_data.TryGetValue(key, out List<TValue> items))
        {
            return items;
        }

        throw new KeyNotFoundException();
    }

    public IEnumerator<TValue> GetEnumerator()
    {
        return CreateEnumerable().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return CreateEnumerable().GetEnumerator();
    }

    IEnumerable<TValue> CreateEnumerable()
    {
        foreach (IEnumerable<TValue> values in _data.Values)
        {
            foreach (TValue value in values)
            {
                yield return value;
            }
        }
    }
}
