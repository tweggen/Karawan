using System;

namespace engine.navigation;

/// <summary>
/// A repeating temporal constraint (e.g., traffic light cycle).
/// </summary>
public class CyclicConstraint : ITemporalConstraint
{
    /// <summary>
    /// Length of one complete cycle (seconds).
    /// </summary>
    public double CycleSeconds { get; set; } = 60.0;

    /// <summary>
    /// When the active phase starts within the cycle (seconds).
    /// </summary>
    public double ActivePhaseStart { get; set; } = 0.0;

    /// <summary>
    /// Duration of the active phase (seconds).
    /// </summary>
    public double ActivePhaseDuration { get; set; } = 30.0;

    /// <summary>
    /// Query the constraint state at a specific time.
    /// </summary>
    public TemporalConstraintState Query(DateTime currentTime)
    {
        var cyclePosition = (currentTime - DateTime.UnixEpoch).TotalSeconds % CycleSeconds;

        var isActive = cyclePosition >= ActivePhaseStart &&
                      cyclePosition < (ActivePhaseStart + ActivePhaseDuration);

        // Calculate time until next state change
        double untilChange;
        if (isActive)
        {
            // In active phase: time until deactivation
            untilChange = (ActivePhaseStart + ActivePhaseDuration) - cyclePosition;
        }
        else
        {
            // In inactive phase: time until next activation
            untilChange = (CycleSeconds - cyclePosition) + ActivePhaseStart;
        }

        return new TemporalConstraintState(isActive, TimeSpan.FromSeconds(untilChange));
    }

    /// <summary>
    /// Get a text representation (for debugging).
    /// </summary>
    public override string ToString()
        => $"CyclicConstraint(cycle={CycleSeconds}s, active={ActivePhaseStart}-{ActivePhaseStart + ActivePhaseDuration}s)";
}
