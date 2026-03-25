using System;
using engine.navigation;

namespace builtin.modules.satnav.desc;


/**
 * This is a navigable lane.
 * It describes one edge of the navigation graph.
 * It is directed by nature and may contain further conditions that
 * more closely specify the way things may navigate within.
 */
public class NavLane
{
    public NavJunction Start;
    public NavJunction End;

    public float MaxSpeed;
    public float Length;

    /// <summary>
    /// Which transportation types can use this lane.
    /// Default: Pedestrian only.
    /// </summary>
    public TransportationTypeFlags AllowedTypes { get; set; } =
        new TransportationTypeFlags(TransportationType.Pedestrian);

    /// <summary>
    /// Temporal constraint on this lane (e.g., traffic light).
    /// Null means no constraint (always accessible).
    /// </summary>
    public ITemporalConstraint? Constraint { get; set; }

    /// <summary>
    /// Get the movement cost for this lane for a specific transportation type.
    /// Cost = Distance / Speed (in seconds).
    /// </summary>
    public float GetCost(TransportationType type)
    {
        if (!AllowedTypes.HasFlag(type))
            return float.MaxValue;  // Type not allowed on this lane

        // Get base speed for this type (m/s)
        var baseSpeed = type switch
        {
            TransportationType.Pedestrian => 1.5f,   // ~3.4 mph
            TransportationType.Car => 13.4f,         // ~30 mph
            TransportationType.Bicycle => 5.0f,      // ~11 mph
            TransportationType.Bus => 11.0f,         // ~25 mph
            _ => 1.5f
        };

        // Use MaxSpeed if available, otherwise use type-based speed
        if (MaxSpeed > 0)
            baseSpeed = baseSpeed < MaxSpeed ? baseSpeed : MaxSpeed;

        if (Length <= 0)
            return 0;

        return Length / baseSpeed;  // Time to traverse
    }

    /// <summary>
    /// Query the constraint state at a specific time.
    /// </summary>
    public TemporalConstraintState QueryConstraint(DateTime currentTime)
    {
        if (Constraint == null)
            return new TemporalConstraintState(true, TimeSpan.MaxValue);

        return Constraint.Query(currentTime);
    }
}