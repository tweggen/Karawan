namespace engine.streets;


/**
 * How a deck level maps to a height above the ground surface.
 *
 * Kept apart from StreetPoint.Pos3 on purpose. Pos3 is the coordinate the stroke and
 * point octrees are keyed on, and every neighbourhood query in StrokeStore is
 * expressed in it: "within 30 m" means 30 m in PLAN, and the duplicate-point guard in
 * AddPoint relies on two points at the same plan position colliding. Folding deck
 * height into Pos3 would quietly turn all of that three-dimensional - radii would stop
 * meaning what callers think, cross-level neighbours would drop out of range in a way
 * that overlaps confusingly with the explicit level filtering that already separates
 * decks, and two junctions stacked on different levels would stop being detected as
 * coincident.
 *
 * So: Pos3 is INDEX space and stays planar. This is WORLD space, and only geometry,
 * navigation and rendering have any business with it.
 */
public static class StreetLevels
{
    /**
     * Vertical separation between adjacent decks, in metres. Enough for a street to
     * pass under a bridge.
     */
    public const float DeckHeight = 8f;


    /**
     * Height of a deck above the ground surface at that spot. Callers add it to
     * whatever terrain height they compute; the terrain is not this type's business.
     */
    public static float ElevationOf(sbyte level)
    {
        return level * DeckHeight;
    }
}
