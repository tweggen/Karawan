using System;
using System.Collections.Generic;
using engine.news;
using ExpectEngine;

namespace engine.testing;

/// <summary>
/// Bridges the engine's SubscriptionManager to ExpectEngine's ITestEventSource.
/// Subscribes at the root path ("") to receive all events and forwards them
/// as TestEvent instances.
/// </summary>
public sealed class JoyceTestEventSource : ITestEventSource, IDisposable
{
    private readonly object _lo = new();
    private readonly List<Action<TestEvent>> _subscribers = new();
    private readonly Action<Event> _handler;

    public JoyceTestEventSource()
    {
        _handler = _onEngineEvent;
        I.Get<SubscriptionManager>().Subscribe("", _handler);
    }

    private void _onEngineEvent(Event ev)
    {
        var testEvent = new TestEvent(ev.Type, ev.Code, ev);
        List<Action<TestEvent>> subs;
        lock (_lo)
        {
            subs = new List<Action<TestEvent>>(_subscribers);
        }

        foreach (var sub in subs)
        {
            try
            {
                sub(testEvent);
            }
            catch
            {
                // test infrastructure should not crash the engine
            }
        }
    }

    public IDisposable Subscribe(Action<TestEvent> onEvent)
    {
        lock (_lo)
        {
            _subscribers.Add(onEvent);
        }

        return new Unsubscriber(() =>
        {
            lock (_lo)
            {
                _subscribers.Remove(onEvent);
            }
        });
    }

    public void Dispose()
    {
        I.Get<SubscriptionManager>().Unsubscribe("", _handler);
    }

    private sealed class Unsubscriber : IDisposable
    {
        private readonly Action _action;
        public Unsubscriber(Action action) => _action = action;
        public void Dispose() => _action();
    }
}
