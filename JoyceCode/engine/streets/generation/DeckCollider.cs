namespace engine.streets.generation;


/**
 * Which strokes need a collider of their own in the physics world.
 *
 * Ground streets of a flat city are covered by a single floor plane per fragment, which
 * is cheap and exact because they are all at one height. A deck is not: it is a slab in
 * the air with nothing under it, and a ramp is a tilted one. Once the city follows its
 * terrain there is no single height at all. Each such stroke gets its own collider -
 * the shape is CarriagewayCollider, built on the carriageway the mesh draws (it was a box
 * from junction centre to junction centre until KAR-465).
 */
internal static class DeckCollider
{
    /**
     * Does this stroke need a collider of its own?
     *
     * Two ways to escape the fragment's floor plane, and both need one:
     *
     * - the stroke is on a raised deck, so the floor is 8 m below it;
     * - the city follows its terrain, so there IS no single height a floor plane could
     *   sit at, and every street needs its own surface.
     *
     * A flat city on the ground is the case the floor plane was built for, and there
     * adding colliders would be pure cost.
     *
     * @param groundIsFlat
     *     Whether the whole city sits at one height - IStreetHeightSource.IsFlat.
     */
    internal static bool IsNeededFor(in Stroke stroke, bool groundIsFlat)
    {
        return !groundIsFlat || 0 != stroke.A.Level || 0 != stroke.B.Level;
    }
}
