using System;

namespace engine.navigation;

/// <summary>
/// State of a temporal constraint at a specific moment in time.
/// </summary>
public record TemporalConstraintState(bool CanAccess, TimeSpan UntilChange)
{
    /// <summary>
    /// Can entities access the constrained resource right now?
    /// </summary>
    public bool CanAccess { get; } = CanAccess;

    /// <summary>
    /// How long until the state changes? (minimum time to recheck)
    /// </summary>
    public TimeSpan UntilChange { get; } = UntilChange;
}
