namespace ExpectEngine;

/// <summary>
/// Implemented by the host application to provide events to the test session.
/// The host calls the subscribed action for every event that occurs.
/// </summary>
public interface ITestEventSource
{
    IDisposable Subscribe(Action<TestEvent> onEvent);
}
