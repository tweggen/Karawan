using System;

namespace engine.tale;

/// <summary>
/// Represents a signal emitted in response to an InteractionRequest.
/// Signals flow back to the requester to inform about outcome.
/// Examples: request_fulfilled, request_failed, request_cancelled, etc.
/// </summary>
public class InteractionSignal
{
    public int Id;                          // Unique signal ID (auto-assigned by pool)
    public int RequestId;                   // Links back to the request this signal answers
    public DateTime EmittedAt;              // When signal was emitted
    public string SignalType;               // "request_fulfilled", "request_failed", "request_cancelled"
    public int SourceNpcId;                 // Who emitted signal (usually claimer, or -1 for abstract/system)
}
