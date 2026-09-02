using System;
using System.Numerics;

namespace engine.world;


/**
 * How high an intercity line's track lies, and how high the vehicle on it flies.
 *
 * The two numbers used to be written independently, in two files, from two different
 * expressions: the track (nogame.intercity.Network) took the LOWER of the two cities'
 * average heights and laid a flat ribbon at it, while the vehicle
 * (nogame.characters.intercity.GenerateCharacterOperator) built its two route ends at
 * EACH city's own average plus 20 m and flew the straight chord between them. Wherever
 * two connected cities sit at different heights - measured over the shipped world's
 * 114 lines, |AverageHeight(A) - AverageHeight(B)| is 23.5 m at the median, 66.8 m at
 * p95 and 89.3 m at the worst - the vehicle therefore ran 20 m over its own track at one
 * end and 20 m + that difference at the other. It is the same in the flat game, because
 * ClusterDesc.AverageHeight is computed from the unflattened ground either way.
 *
 * So both heights are decided here, and the vehicle's is derived from the track's rather
 * than from the cities again. There is no per frame sampling and no navigator change,
 * because there is nothing to sample: the track is ONE height for the whole line, so the
 * track's height at the vehicle's own position is that height wherever the vehicle is.
 *
 * What the track's own SHAPE should be - a graded embankment, a viaduct on pylons, or a
 * deliberately elevated line - is an open design question and deliberately not decided
 * here. See docs/roadmap/proposed/CITY-3D-OPEN-POINTS.md item (e).
 */
public static class IntercityLine
{
    /**
     * How high the vehicle runs above its own track.
     *
     * Derived rather than chosen: for two cities of equal average height the shipped
     * expression put the vehicle at that average + 20 and the track at that average, so
     * 20 m is what the game already means by "the intercity line runs up there". A pair
     * of equal-height cities is therefore unchanged by this file, in both the flat and
     * the terrain following world.
     */
    public const float VehicleClearance = 20f;


    /**
     * The height of the flat ribbon IntercityTrackElevationOperator burns into the
     * terrain along the line.
     */
    public static float TrackHeightOf(float averageHeightA, float averageHeightB)
        => Single.Min(averageHeightA, averageHeightB);


    /**
     * The height the vehicle flies at, everywhere along the line.
     */
    public static float VehicleHeightOf(float trackHeight)
        => trackHeight + VehicleClearance;


    /**
     * The route a vehicle runs between two stations.
     *
     * Both ends take the same height, so the chord between them is level and the
     * vehicle is exactly VehicleClearance above the track at every point of it - which
     * is the property the tests drive through the real SegmentNavigator rather than
     * merely asserting about the two endpoints.
     *
     * The station positions' own Y is ignored on purpose: a station is a point on the
     * cluster's boundary rectangle, and its Y is whatever the cluster's nominal Pos.Y
     * happened to be.
     */
    public static builtin.tools.SegmentRoute RouteBetween(
        in Vector3 v3StationA, in Vector3 v3StationB, float trackHeight)
    {
        float y = VehicleHeightOf(trackHeight);
        Vector3 caPos = v3StationA with { Y = y };
        Vector3 cbPos = v3StationB with { Y = y };

        Vector3 vuAB = Vector3.Normalize(cbPos - caPos);
        Vector3 vuUp = new Vector3(0f, 1f, 0f);

        return new builtin.tools.SegmentRoute()
        {
            Segments = new()
            {
                new()
                {
                    Position = caPos,
                    Up = vuUp,
                    Right = Vector3.Cross(vuAB, vuUp)
                },
                new()
                {
                    Position = cbPos,
                    Up = vuUp,
                    Right = Vector3.Cross(-vuAB, vuUp)
                }
            },
            LoopSegments = true
        };
    }
}
