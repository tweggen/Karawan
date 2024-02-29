using System.Collections.Generic;

namespace engine.physics.actions;

public class HandleMapper<T>
{
    private object _lo = new();

    private SortedDictionary<T, T> _mapOldNew = new();
    private SortedDictionary<T, T> _mapNewOld = new();

    public void Add(T stored, T current)
    {
        lock (_lo)
        {
            _mapOldNew.Add(stored, current);
            _mapNewOld.Add(current, stored);
        }
    }


    public T GetNew(T old)
    {
        lock (_lo)
        {
            return _mapOldNew[old];
        }
    }


    public void Remove(T old)
    {
        lock (_lo)
        {
            T current = _mapOldNew[old];
            _mapOldNew.Remove(old);
            _mapNewOld.Remove(current);
        }
    }
    
    
    public T GetOld(T current)
    {
        lock (_lo)
        {
            return _mapNewOld[current];
        }
    }
}