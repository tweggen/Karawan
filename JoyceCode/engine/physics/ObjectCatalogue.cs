using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine.physics;

public class ObjectCatalogue
{
    private object _lo = new();
    private SortedDictionary<uint, Object> _mapPhysics = new();

    private uint _fullHandle(Object po)
    {
        if ((po.Flags & Object.IsStatic) == 0)
        {
            return (uint)po.IntHandle;
        }
        else
        {
            return 0x80000000 | (uint)po.IntHandle;
        }
    }

    public void AddObject(Object po)
    {
        lock (_lo)
        {
            if (_mapPhysics.TryGetValue(_fullHandle(po), out var oldpo))
            {
                ErrorThrow<InvalidOperationException>($"Already added physics {po.IntHandle}.");
            }
            _mapPhysics.Add(_fullHandle(po), po);
        }
    }


    public void RemoveObject(Object po)
    {
        lock (_lo)
        {
            if (!_mapPhysics.TryGetValue(_fullHandle(po), out var oldpo))
            {
                ErrorThrow<InvalidOperationException>($"Did not add physics before for {po.IntHandle}.");
            }
            
            _mapPhysics.Remove(_fullHandle(po));
        }
    }


    public bool FindObject(int handle, out physics.Object po)
    {
        lock (_lo)
        {
            if (_mapPhysics.TryGetValue((uint)handle, out po))
            {
                if ((po.Flags & Object.IsStatic) == 0)
                {
                    return true;
                }
            }
            if (_mapPhysics.TryGetValue(0x80000000|(uint)handle, out po))
            {
                if ((po.Flags & Object.IsStatic) != 0)
                {
                    return true;
                }
            }
            po = null;
            return false;
        }
    }
}