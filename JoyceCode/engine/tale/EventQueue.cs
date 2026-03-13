using System;
using System.Collections.Generic;

namespace engine.tale;

public enum SimEventType
{
    NodeArrival,
    EncounterCheck,
    InterruptResolution
}

public struct SimEvent : IComparable<SimEvent>
{
    public DateTime GameTime;
    public int NpcId;
    public SimEventType Type;

    public int CompareTo(SimEvent other) => GameTime.CompareTo(other.GameTime);
}

/// <summary>
/// Min-heap priority queue of SimEvents ordered by GameTime.
/// </summary>
public class EventQueue
{
    private readonly List<SimEvent> _heap = new();

    public int Count => _heap.Count;
    public bool IsEmpty => _heap.Count == 0;
    public DateTime NextTime => _heap[0].GameTime;


    public void Push(SimEvent evt)
    {
        _heap.Add(evt);
        SiftUp(_heap.Count - 1);
    }


    public SimEvent Pop()
    {
        var result = _heap[0];
        int last = _heap.Count - 1;
        _heap[0] = _heap[last];
        _heap.RemoveAt(last);
        if (_heap.Count > 0)
            SiftDown(0);
        return result;
    }


    private void SiftUp(int i)
    {
        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (_heap[i].CompareTo(_heap[parent]) >= 0) break;
            (_heap[i], _heap[parent]) = (_heap[parent], _heap[i]);
            i = parent;
        }
    }


    private void SiftDown(int i)
    {
        int count = _heap.Count;
        while (true)
        {
            int smallest = i;
            int left = 2 * i + 1;
            int right = 2 * i + 2;
            if (left < count && _heap[left].CompareTo(_heap[smallest]) < 0)
                smallest = left;
            if (right < count && _heap[right].CompareTo(_heap[smallest]) < 0)
                smallest = right;
            if (smallest == i) break;
            (_heap[i], _heap[smallest]) = (_heap[smallest], _heap[i]);
            i = smallest;
        }
    }
}
