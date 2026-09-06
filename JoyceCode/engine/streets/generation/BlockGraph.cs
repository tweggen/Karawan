using System;
using System.Collections.Generic;
using System.Numerics;
using ClipperLib;

namespace engine.streets.generation;


/**
 * Which part of the street network a city block is traced over, and what land inside
 * one a grade-separated structure takes away from it.
 *
 * ⚠️ A RAMP, BRIDGE OR TUNNEL IS ABSENT FROM THE BLOCK GRAPH (§3c). A city block is a
 * face of a PLANAR graph, and the moment a structure exists the street network is not
 * planar: two roads cross at one place and do not meet there. Trace over that and the
 * faces stop being blocks - measured on the fixture that settled §3c, a ground
 * crossroads' five clean quarters became two, one of them a sixteen-corner face
 * carrying six delimiters on structure strokes, four of them at Level = 1, running
 * along the ground road underneath the deck on BOTH sides within the same face.
 *
 * With the deck and its ramps out of the graph the remainder is planar again by itself
 * and no pseudo-vertex is needed. The blocks either side of a lifted corridor MERGE
 * into one, which is physically right: a viaduct does not bound a city block.
 *
 * ⚠️ A ConnectorBridge STAYS IN. It is an ordinary ground road - one to three exist in
 * every shipped flat city - and a rule phrased as "anything that is not a Street" would
 * change the default city. StrokeKinds.IsStructure is the one expression that names the
 * three kinds, and this is its only caller here.
 */
public static class BlockGraph
{
    /**
     * Whether a city block may run along this stroke.
     */
    public static bool IsBlockEdge(Stroke s)
        => !StrokeKinds.IsStructure(s.Kind);


    /**
     * The same predicate as a delegate, allocated once, for StreetPoint.GetNextAngle's
     * filter. One instance so that the block trace does not allocate per junction.
     */
    public static readonly Func<Stroke, bool> Accept = IsBlockEdge;


    /**
     * How many of this junction's arms a city block may run along.
     *
     * Fewer than two is a dead end AS FAR AS BLOCKS ARE CONCERNED, and the trace treats
     * it exactly as it has always treated a one-armed junction: no section point, so the
     * face is discarded. A deck end has none at all - every arm it carries is a
     * structure member - so nothing ever starts a trace there and nothing ever arrives.
     */
    public static int ArmCountOf(StreetPoint sp)
    {
        int n = 0;
        foreach (var s in sp.GetAngleArray())
        {
            if (IsBlockEdge(s)) ++n;
        }

        return n;
    }


    /**
     * Whether anything may stand at this junction.
     *
     * A deck end carries nothing but structure members - a ramp and the deck it holds up -
     * so it is a point in the air with no pavement, no block cornering on it and no way
     * up to it on foot. Every other junction has at least one ordinary road leaving it and
     * is a place in the city.
     *
     * ⚠️ Not only the deck ends, measured: one junction of Yelukhdidru@3000 is a ramp FOOT
     * on the ground whose single ordinary street was the corridor arm the lift took away,
     * so it too carries nothing a block can run along. It is refused for the same reason
     * and it is the same statement - nothing corners here - which is why this is phrased
     * about arms and not about height.
     *
     * Phrased as "has at least one block arm" rather than "Level == 0" deliberately: it is
     * the same question the block trace asks, so a junction is buildable-on exactly when a
     * block can corner on it, and the two cannot drift apart. It is also independent of
     * which level the ground happens to be, which Level == 0 is not.
     *
     * TRUE FOR EVERY JUNCTION OF EVERY CITY WITH joyce.EnableGradeSeparation OFF, since
     * such a city has no structure member in it at all and PolishStreetPoints has already
     * removed every junction with no stroke.
     */
    public static bool StandsOnTheGround(StreetPoint sp) => ArmCountOf(sp) > 0;


    /**
     * ⚠️ THE LAND A STRUCTURE TAKES OUT OF THE BLOCK IT NOW STANDS IN.
     *
     * Merging the blocks either side of a lifted corridor leaves the structure INSIDE
     * the merged block, in plan. The estate is that block's outline inset by the
     * pavement width, ClipperOffset turns it into the building footprint, and
     * QuarterGenerator._createBuildings then puts a building on it - a median 24 m
     * building standing under a deck 8 m up, or straddling a ramp.
     *
     * So the structure's own carriageway, widened by the same pavement width the block
     * outline is inset by, is subtracted from the estate before anything is built on it.
     * A tunnel bore counts too: the three kinds are not distinguished here, because
     * nothing in the tree draws what is under a deck or over a bore, and a rule that
     * built on one but not the other would be guessing.
     *
     * Returns the SAME list it was given when nothing intersects, so a city with no
     * structure in it - which is every city with joyce.EnableGradeSeparation off - runs
     * exactly the code it ran before.
     *
     * ⚠️ At most one polygon comes back. The caller concatenates every polygon of the
     * solution into a single ring and has done so since it was written (its own TXWTODO
     * says so), which is harmless while an inset of a simple polygon gives at most one
     * piece and is nonsense the moment a subtraction splits it in two. So the largest
     * piece is chosen here, where the split happens.
     *
     * @param estate
     *     The building footprint, in Clipper's tenth-metre integer coordinates.
     * @param structures
     *     Structure strokes that may lie inside it. Non-structures are ignored, so that
     *     a caller cannot widen this rule by handing it more.
     * @param margin
     *     How far outside its own carriageway a structure's footprint reaches, in
     *     metres. The block's own pavement width, so that the strip left beside a ramp
     *     is the strip left beside any other road.
     */
    public static List<List<IntPoint>> ExcludeStructures(
        List<List<IntPoint>> estate, IEnumerable<Stroke> structures, float margin)
    {
        List<List<IntPoint>> clips = null;

        foreach (var s in structures)
        {
            if (!StrokeKinds.IsStructure(s.Kind))
            {
                continue;
            }

            clips ??= new List<List<IntPoint>>();
            clips.Add(FootprintOf(s, margin));
        }

        if (null == clips)
        {
            return estate;
        }

        var clipper = new Clipper();
        clipper.AddPaths(estate, PolyType.ptSubject, true);
        clipper.AddPaths(clips, PolyType.ptClip, true);

        var solution = new List<List<IntPoint>>();
        clipper.Execute(ClipType.ctDifference, solution, PolyFillType.pftNonZero,
            PolyFillType.pftNonZero);

        return LargestOf(solution);
    }


    /**
     * The one piece of a footprint that a building is designed on.
     *
     * ⚠️ QuarterGenerator._createBuildings concatenates EVERY polygon it is given into one
     * ring - its own TXWTODO says so and it has done it since it was written. That is
     * harmless while the answer is one polygon and nonsense the moment it is two: the
     * result is a single self-crossing outline, and a building is designed on it.
     *
     * Two things produce two pieces. A structure subtracted from the estate can split it -
     * that is ExcludeStructures above. And the pavement inset can split a block on its own,
     * where a block is pinched to less than two pavement widths somewhere across its
     * middle: measured, that happens on 0 of 763 estates over the seven pinned cities with
     * joyce.EnableGradeSeparation off, and on 4 of 714 with it on, in blocks with no
     * structure anywhere near them. So it is a pre-existing defect that the flag makes
     * reachable, and the same answer serves both.
     *
     * Returns the SAME list it was handed whenever there is nothing to choose between, so
     * a city that never splits an inset - which is every flag-off city measured - runs
     * exactly the code it ran before rather than an equal-looking rebuild of it.
     */
    public static List<List<IntPoint>> LargestOf(List<List<IntPoint>> solution)
    {
        if (solution.Count <= 1)
        {
            return solution;
        }

        List<IntPoint> largest = null;
        double largestArea = 0.0;
        foreach (var polygon in solution)
        {
            double area = Math.Abs(Clipper.Area(polygon));
            if (null == largest || area > largestArea)
            {
                largest = polygon;
                largestArea = area;
            }
        }

        var result = new List<List<IntPoint>>();
        if (null != largest && largest.Count > 0)
        {
            result.Add(largest);
        }

        return result;
    }


    /**
     * The plan footprint of one structure member: its carriageway, widened by margin on
     * every side.
     *
     * In Clipper's tenth-metre integer coordinates, matching what
     * QuarterGenerator._createBuildings feeds it, and taken from Stroke.Unit and
     * Stroke.StreetWidth so that it is the same carriageway the road mesh draws.
     */
    public static List<IntPoint> FootprintOf(Stroke s, float margin)
    {
        Vector2 u = s.Unit;
        Vector2 n = new Vector2(-u.Y, u.X);

        float half = s.StreetWidth() / 2f + margin;

        Vector2 a = s.A.Pos - u * margin;
        Vector2 b = s.B.Pos + u * margin;

        var poly = new List<IntPoint>();
        foreach (var p in new[] { a + n * half, b + n * half, b - n * half, a - n * half })
        {
            poly.Add(new IntPoint((int)(p.X * 10f), (int)(p.Y * 10f)));
        }

        return poly;
    }
}
