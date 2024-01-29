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
            if (ev.Type == Event.INPUT_MOUSE_RELEASED || ev.Type == Event.INPUT_TOUCH_RELEASED)
            {
                int a = 1;
            }
            if (ev.Type == Event.INPUT_MOUSE_PRESSED || ev.Type == Event.INPUT_TOUCH_PRESSED)
            {
                int a = 1;
            }
            if (ev.Type == Event.INPUT_MOUSE_MOVED)
            {
                int a = 1;
            }
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