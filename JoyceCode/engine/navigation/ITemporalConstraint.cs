using System;

namespace engine.navigation;

/// <summary>
/// Interface for time-dependent access control.
/// Can be used for traffic lights, elevators, doors, transit, etc.
/// </summary>
public interface ITemporalConstraint
{
    /// <summary>
    /// Query the constraint state at a given time.
    /// </summary>
    TemporalConstraintState Query(DateTime currentTime);
}
