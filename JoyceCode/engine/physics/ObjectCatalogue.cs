using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine.physics;

public class ObjectCatalogue
{
    private object _lo = new();
    private SortedDictionary<int, Object> _mapPhysics = new();

    public void AddObject(Object po)
    {
        lock (_lo)
        {
            if (_mapPhysics.TryGetValue(po.IntHandle, out var oldpo))
            {
                ErrorThrow<InvalidOperationException>($"Already added physics {po.IntHandle}.");
            }
            _mapPhysics.Add(po.IntHandle, po);
        }
    }


    public void RemoveObject(Object po)
    {
        lock (_lo)
        {
            if (!_mapPhysics.TryGetValue(po.IntHandle, out var oldpo))
            {
                ErrorThrow<InvalidOperationException>($"Did not add physics before for {po.IntHandle}.");
            }
            
            _mapPhysics.Remove(po.IntHandle);
        }
    }


    public bool FindObject(int handle, out physics.Object po)
    {
        lock (_lo)
        {
            if (_mapPhysics.TryGetValue(handle, out po))
            {
                return true;
            }
            else
            {
                po = null;
                return false;
            }
        }
    }
}