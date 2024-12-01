using System;
using System.Collections.Generic;
using engine.world;
using static engine.Logger;

namespace engine;

internal class RegistryEntry<T>
{
    public T Object;
    public uint Id;
}



public class GenericIdRegistry<T> : IDisposable
{
    private object _lo = new();
    
    private Dictionary<T, RegistryEntry<T>> _mapObjects = new();
    private SortedDictionary<uint, RegistryEntry<T>> _mapIds = new();
    private uint _nextId = 0;
    
    public void Dispose()
    {
        return;
    }

    public uint FindId(T iCreator)
    {
        lock (_lo)
        {
            if (_mapObjects.TryGetValue(iCreator, out var ce))
            {
                return ce.Id;
            }
            else
            {
                ErrorThrow<ArgumentException>($"Unable to find owner for type {iCreator.GetType()}");
                return 0;
            }
        }
    }


    public T Get(uint creatorId)
    {
        lock (_lo)
        {
            return _mapIds[creatorId].Object;
        }
    }
    
    
    public void Unregister(T obj)
    {
        lock (_lo)
        {
            var ce = _mapObjects[obj];
            _mapObjects.Remove(ce.Object);
            _mapIds.Remove(ce.Id);
        }
    }
    
    
    public void Register(T iCreator)
    {
        lock (_lo)
        {
            var ce = new RegistryEntry<T>()
            {
                Id = _nextId++,
                Object = iCreator
            };
            _mapIds[ce.Id] = ce;
            _mapObjects[iCreator] = ce;
        }
    }

    
    public GenericIdRegistry(uint firstId)
    {
        _nextId = firstId;
    }
}