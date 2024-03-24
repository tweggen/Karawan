using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Linq;
using static engine.Logger;

namespace engine;


internal class Node<T>
{
    public List<T> Entries = new();
}


public class PriorityMap<T>
{
    private object _lo = new();
    
    private SortedDictionary<float, Node<T>> _mapPrios = new();
    
    private List<T>? _list = null;
    private IEnumerable<T>? _enumerable = null;


    /**
     * Compile a sorted list of client objects sorted by their priority.
     */
    private void _createEnumerable_NL()
    {
        _list = new List<T>();
        foreach (var kvp in _mapPrios)
        {
            _list.AddRange(kvp.Value.Entries);
        }
        _enumerable = _list;
    }
    
    
    /**
      Return an enumerable of all of the listeners. 
     */
    public IEnumerable<T> GetEnumerable()
    {
        lock (_lo)
        {
            if (null == _enumerable)
            {
                _createEnumerable_NL();
            }
            return _enumerable;
        }
    }
    
    
    /**
     * Remove the listener "T" at the priority prio.
     */
    public void Remove(float prio, T obj)
    {
        prio *= -1f;
        lock (_lo)
        {
            Node<T> node;
            if (!_mapPrios.TryGetValue(prio, out node))
            {
                ErrorThrow<InvalidOperationException>($"Requested to remove node with priority {prio}, but the priority could not be found.");
            }

            if (!node.Entries.Remove(obj))
            {
                ErrorThrow<InvalidOperationException>($"Requested to remove node with priority {prio}, but the node could be found at that prioriuty.");
            }
            if (node.Entries.Count == 0)
            {
                _mapPrios.Remove(prio);
            }

            _list = null;
            _enumerable = null;
        }
    }


    public void Remove(T obj)
    {
        lock (_lo)
        {
            foreach (var kvp in _mapPrios)
            {
                if (kvp.Value.Entries.Remove(obj))
                {
                    if (kvp.Value.Entries.Count == 0)
                    {
                        _mapPrios.Remove(kvp.Key);
                    }
                    _list = null;
                    _enumerable = null;
                    return;
                }
            }
        }

        ErrorThrow<InvalidOperationException>(
            $"Requested to remove node without priority, but the node could not be found.");
    }
    
    
    /**
     * Add the listener "T" at priority prio.
     */
    public void Add(float prio, T obj)
    {
        prio *= -1f;
        lock (_lo)
        {
            Node<T> node;
            if (!_mapPrios.TryGetValue(prio, out node))
            {
                node = new();
                _mapPrios[prio] = node;
            }
            node.Entries.Add(obj);
            _list = null;
            _enumerable = null;
        }
    }

    public float FrontPrio()
    {
        lock (_lo)
        {
            foreach (var kvp in _mapPrios)
            {
                return -kvp.Key;
            }

            return Single.MinValue;
        }
    }
}