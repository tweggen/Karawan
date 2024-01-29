using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine.news;

public class EventQueue
{
    private object _lo = new();
    private Queue<Event> _queue = new();


    public Event Pop()
    {
        lock (_lo)
        {
            if (_queue.Count == 0)
            {
                ErrorThrow("Queue is empty.", (m) => new InvalidOperationException(m));
            }

            return _queue.Dequeue();
        }
    }
    
    
    public void Push(Event ev)
    {
        lock (_lo)
        {
            _queue.Enqueue(ev);
        }
    }


    public bool IsEmpty()
    {
        lock (_lo)
        {
            return _queue.Count == 0;
        }
    }
}