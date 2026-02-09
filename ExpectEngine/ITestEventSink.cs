namespace ExpectEngine;

/// <summary>
/// Implemented by the host application to accept injected events.
/// </summary>
public interface ITestEventSink
{
    Task InjectEventAsync(TestEvent testEvent);
}
