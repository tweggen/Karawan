using System;

namespace engine;

public struct IdHolder : IComparable<IdHolder>
{
    private static object _classLock = new();
    private static long _nextId = 0;
    public readonly long Id;

    public IdHolder()
    {
        lock (_classLock)
        {
            Id = _nextId++;
        }
    }

    public int CompareTo(IdHolder other)
    {
        return Id.CompareTo(other.Id);
    }
}