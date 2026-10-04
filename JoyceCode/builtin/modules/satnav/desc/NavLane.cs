using System;
using System.Numerics;
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
    /// The carriageway this lane runs along, in world plan coordinates, or null for a lane
    /// that is not on one - every pedestrian lane, and any junction synthesised part way
    /// along a route.
    /// </summary>
    /// <remarks>
    /// A lane's two ends carry their own junction's height and everything reading a lane
    /// interpolates linearly between them, which is what a lane IS as a routing edge. The
    /// road is not that shape: a junction cap is a flat fan at its junction's one height,
    /// so the carriageway is flat over each footprint and climbs only between the section
    /// points - flat, ramp, flat. The chord and the road therefore agree at the two
    /// junctions and nowhere in between, above the road at one end and below it at the
    /// other by the grade times the cap's reach into the stroke.
    ///
    /// That is invisible to a router, which only wants a cost, and to a car, which hovers
    /// on a raycast against the collider. It is not invisible to anything DRAWN on the
    /// road: the satnav guideline is a 4 m ribbon lying on the carriageway, and it sank
    /// into it by a median 0.07 to 0.19 m and up to 9.85 m over the four baseline cities
    /// on the shipped terrain, below the road at 43 to 50 % of positions. So the lane
    /// carries the surface the road was actually built on - the same
    /// engine.streets.generation.RoadSurface, from the same four section points - rather
    /// than the ribbon deriving a second one that has to be kept in step.
    /// </remarks>
    public engine.streets.generation.RoadSurface? Surface { get; set; }

    /// <summary>
    /// Unit vector, in plan, from the lane's centre line toward the surface a walker on it
    /// should keep to - the block whose kerb this lane runs along. Zero where there is no
    /// such side, which is every car lane and every pedestrian CROSSING: a crossing is in
    /// the carriageway by definition and belongs on its centre line.
    /// </summary>
    /// <remarks>
    /// This has to be a property of the LANE and not of the direction of travel. Lanes are
    /// created in both directions over the same ground, so a walker offsetting to a fixed
    /// hand relative to travel keeps to the pavement one way round the block and stands in
    /// the road the other - which is what PedestrianRoute.WaypointFor did, offsetting 1.5 m
    /// to the right of travel unconditionally. Measured over the block edges of the
    /// generated cities, the right of travel is outside the block 100 % of the time for the
    /// direction the blocks are traced in, so exactly one of each lane pair was in the
    /// roadway - in the flat city too.
    /// </remarks>
    public Vector3 KerbSide { get; set; } = Vector3.Zero;

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