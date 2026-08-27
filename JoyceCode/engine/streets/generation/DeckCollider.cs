using System;
using System.Numerics;

namespace engine.streets.generation;


/**
 * The box that stands in for one raised road surface in the physics world.
 *
 * Ground streets are covered by a single floor plane per fragment, which is cheap and
 * good enough because they are all at one height. A deck is not: it is a slab in the
 * air with nothing under it, and a ramp is a tilted one. Each therefore needs its own
 * collider, or a vehicle drives straight through a bridge.
 *
 * Kept as a plain computation, separate from the Bepu call that consumes it, so that
 * the part with the arithmetic in it can be tested without a physics simulation.
 */
internal readonly struct DeckCollider
{
    /**
     * Centre of the box in world space, already lowered so that its TOP face is the
     * road surface.
     */
    internal readonly Vector3 Position;

    /**
     * Maps the box's local axes onto the road: local X runs along the stroke, local Y
     * is the surface normal, local Z is across the carriageway.
     */
    internal readonly Quaternion Orientation;

    /**
     * Along the stroke. The 3D length, so a ramp's collider is as long as the ramp
     * actually is rather than as long as its shadow.
     */
    internal readonly float Length;

    internal readonly float Width;
    internal readonly float Thickness;


    private DeckCollider(
        Vector3 position, Quaternion orientation, float length, float width, float thickness)
    {
        Position = position;
        Orientation = orientation;
        Length = length;
        Width = width;
        Thickness = thickness;
    }


    /**
     * Does this stroke need a collider of its own?
     *
     * Only what leaves the ground: everything on level 0 is already covered by the
     * fragment floor, and adding boxes for it would be pure cost.
     */
    internal static bool IsNeededFor(in Stroke stroke)
    {
        return 0 != stroke.A.Level || 0 != stroke.B.Level;
    }


    /**
     * @param worldA, worldB
     *     The stroke's endpoints in world space, at their respective deck heights.
     * @param width
     *     Carriageway width.
     * @param thickness
     *     How deep the slab is. Only its top face matters for driving on.
     */
    internal static DeckCollider For(
        in Vector3 worldA, in Vector3 worldB, float width, float thickness)
    {
        Vector3 along = worldB - worldA;
        float length = along.Length();

        if (length < 0.001f)
        {
            return new DeckCollider(worldA, Quaternion.Identity, 0f, width, thickness);
        }

        Vector3 xAxis = along / length;

        /*
         * Across the carriageway, level with the horizon: a road banks along its
         * length, never sideways.
         */
        Vector3 zAxis = Vector3.Cross(xAxis, Vector3.UnitY);
        if (zAxis.LengthSquared() < 1e-8f)
        {
            /*
             * Dead vertical, which no road is. Pick any lateral axis rather than
             * normalising a zero vector.
             */
            zAxis = Vector3.UnitZ;
        }
        zAxis = Vector3.Normalize(zAxis);

        /*
         * The surface normal follows from the other two, so a ramp's collider is
         * tilted by exactly the slope of the ramp.
         */
        Vector3 yAxis = Vector3.Normalize(Vector3.Cross(zAxis, xAxis));

        /*
         * Right handed by construction: xAxis cross yAxis is zAxis. Taking the lateral
         * axis the other way round produces a left handed basis whose "up" points
         * DOWN, which lowers the slab through the road instead of under it.
         */

        var basis = new Matrix4x4(
            xAxis.X, xAxis.Y, xAxis.Z, 0f,
            yAxis.X, yAxis.Y, yAxis.Z, 0f,
            zAxis.X, zAxis.Y, zAxis.Z, 0f,
            0f, 0f, 0f, 1f);

        /*
         * Lowered along the surface normal rather than straight down, so that the TOP
         * face lands on the road for a tilted slab too.
         */
        Vector3 centre = (worldA + worldB) * 0.5f - yAxis * (thickness * 0.5f);

        return new DeckCollider(
            centre, Quaternion.CreateFromRotationMatrix(basis), length, width, thickness);
    }
}
