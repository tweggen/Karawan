using System.Threading.Tasks;
using engine.news;
using ExpectEngine;

namespace engine.testing;

/// <summary>
/// Injects TestEvents into the engine's EventQueue.
/// EventQueue.Push is already thread-safe, so this can be called from any thread.
/// </summary>
public sealed class JoyceTestEventSink : ITestEventSink
{
    public Task InjectEventAsync(TestEvent testEvent)
    {
        var engineEvent = new Event(testEvent.Type, testEvent.Code ?? "");
        I.Get<EventQueue>().Push(engineEvent);
        return Task.CompletedTask;
    }
}
