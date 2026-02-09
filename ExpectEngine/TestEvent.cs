namespace ExpectEngine;

/// <summary>
/// A host-agnostic test event. Mirrors the structure of typical engine events
/// but with no dependency on any specific engine.
/// </summary>
public sealed class TestEvent
{
    public string Type { get; }
    public string Code { get; }
    public DateTime Timestamp { get; }
    public object Payload { get; }

    public TestEvent(string type, string code = null, object payload = null)
    {
        Type = type;
        Code = code;
        Timestamp = DateTime.UtcNow;
        Payload = payload;
    }

    public override string ToString() => Code != null ? $"TestEvent({Type}, {Code})" : $"TestEvent({Type})";
}
