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
     * ⚠️ THE JUNCTIONS A CITY BLOCK MAY CORNER ON: THE 2-CORE OF THE BLOCK GRAPH.
     *
     * Peel every junction with fewer than two block arms, iteratively until nothing is
     * left to peel - peeling one dead-end spur can leave its own neighbour with a single
     * arm, so one pass is not enough. What survives is exactly the set of junctions that
     * lie on a cycle, i.e. the junctions a closed ring can be traced through.
     *
     * ⚠️ WHY THE TRACE NEEDS THIS AND NOT MERELY A BETTER TERMINATION CONDITION (§7t).
     * A dead-end spur cuts a SLIT into the face beside it: the face walks out along the
     * spur, round its tip and back, so it passes through the spur's root junction twice.
     * QuarterGenerator used to stop its walk on `spNext == spStart`, a VERTEX test, so
     * whenever that doubly-visited junction was the one the walk started at the walk
     * stopped half way round and the ring was closed by a straight chord across the
     * block - a chord that is not a street, median 75 m long and up to 147 m, with the
     * estate, its inset and the building on it laid across whatever it ran over. That is
     * the defect reported from play.
     *
     * Terminating on the directed edge instead makes the walk correct and produces a face
     * that the hasNullSection rule then DISCARDS, because it contains a junction with
     * fewer than two block arms: 219 blocks of the flat city and 272 of the shipped one
     * would become holes in the pavement. Peeling first is the other answer, and the one
     * chosen: a spur is not a block edge at all, so the face has no pinch, the ring closes
     * AND the block stands, going round the spur instead of across it. It also recovers
     * most of the third of all faces that were being discarded silently (§7t.4).
     *
     * ⚠️ THE SPUR IS THEN INSIDE THE BLOCK, by construction. WP-O2 subtracts it from the
     * estate - SpurCorridorsOf below names the strokes and ExcludeCarriageways takes them
     * out, which is the same rule ExcludeStructures applies to a ramp: the carriageway
     * widened by the block's own pavement width.
     */
    public static HashSet<StreetPoint> TwoCoreOf(StrokeStore store)
    {
        var degree = new Dictionary<StreetPoint, int>();
        var adjacency = new Dictionary<StreetPoint, List<StreetPoint>>();

        foreach (var sp in store.GetStreetPoints())
        {
            degree[sp] = 0;
            adjacency[sp] = new List<StreetPoint>();
        }

        foreach (var s in store.GetStrokes())
        {
            if (!IsBlockEdge(s)) continue;
            if (!degree.ContainsKey(s.A) || !degree.ContainsKey(s.B)) continue;

            ++degree[s.A];
            ++degree[s.B];
            adjacency[s.A].Add(s.B);
            adjacency[s.B].Add(s.A);
        }

        var live = new HashSet<StreetPoint>(degree.Keys);

        var peel = new Queue<StreetPoint>();
        foreach (var sp in live)
        {
            if (degree[sp] < 2) peel.Enqueue(sp);
        }

        while (peel.Count > 0)
        {
            var sp = peel.Dequeue();
            if (!live.Remove(sp)) continue;

            foreach (var neighbour in adjacency[sp])
            {
                if (!live.Contains(neighbour)) continue;

                if (--degree[neighbour] < 2) peel.Enqueue(neighbour);
            }
        }

        return live;
    }


    /**
     * Which arms a block trace may run along, given the 2-core it was peeled to.
     *
     * A block edge whose far end was peeled away is not part of any ring, so it is
     * refused here rather than being walked out along and turned round at - which is what
     * used to cut the slit the truncation then dropped. One delegate per city, handed to
     * StreetPoint.GetNextAngle and used for the trace's own start filter, so that the two
     * cannot disagree about what a block arm is.
     */
    public static Func<Stroke, bool> AcceptWithin(HashSet<StreetPoint> core)
        => s => IsBlockEdge(s) && core.Contains(s.A) && core.Contains(s.B);


    /**
     * ⚠️ THE ROADS THE PEEL TOOK OUT OF THE BLOCK GRAPH: the dead-end spur trees.
     *
     * A block edge with an end outside the 2-core is not part of any ring, so no block
     * runs along it - it stands INSIDE one. That is what makes the ring close (§7u) and
     * it is also the whole cost of WP-O1 on its own: the estate is the block's outline,
     * the outline now encloses the spur, and a building was designed across it on 3247
     * blocks flag off and 2986 flag on.
     *
     * So this is the counterpart of the peel: whatever AcceptWithin refuses as a block
     * arm, this names as a carriageway standing in a block, and ExcludeCarriageways takes
     * it out of the estate. The two are written from the same two terms so that they
     * cannot disagree about which roads those are - an arm the trace walks along is an
     * edge of the block and is never subtracted from it, and an arm the trace refuses is
     * inside the block and always is.
     *
     * A ConnectorBridge counts. It is an ordinary ground road (§7, WP-B1) and a building
     * standing on one is the reported symptom whatever the stroke's Kind says; that is
     * the opposite of ExcludeStructures' rule, which refuses to touch one because there
     * a ConnectorBridge is a road a block is BOUNDED by. Both statements are about the
     * same thing seen from two sides: a road inside the block is excluded, a road the
     * block runs along is not.
     *
     * Empty for a city whose block graph is its own 2-core.
     */
    public static List<Stroke> SpurCorridorsOf(StrokeStore store, HashSet<StreetPoint> core)
    {
        var spurs = new List<Stroke>();

        foreach (var s in store.GetStrokes())
        {
            if (!IsBlockEdge(s)) continue;
            if (core.Contains(s.A) && core.Contains(s.B)) continue;

            spurs.Add(s);
        }

        return spurs;
    }


    /**
     * ⚠️ WHETHER A TRACED FACE IS THE INSIDE OF A CITY BLOCK OR THE OUTSIDE OF THE CITY.
     *
     * A face traversal that always turns to the next arm clockwise gives every interior
     * face one winding and the single outer face of each connected component the other -
     * so one face per component is the whole city seen from outside, and it is the biggest
     * face there is. Measured over the seventy shipped cities: exactly 70 faces of 40961
     * come back with the opposite sign flag off and 70 of 37679 flag on, one per component,
     * and in every city the largest face by area is one of them.
     *
     * ⚠️ THIS RULE IS NEW AND IT IS LOAD BEARING. Until the peel, the outer face was
     * discarded for a reason that had nothing to do with being outside: it ran through a
     * dead-end spur somewhere on the city's edge, so it was refused as hasNullSection.
     * With the spurs peeled away the outer face is a perfectly good closed ring of
     * junctions that all have corners, and without this it would be stored as a city block
     * covering the entire city.
     *
     * ⚠️ NOT SidewalkRing.SignedArea2Of, which answers the same question about a
     * PAVEMENT ring. That one accumulates absolute coordinates in float, which is right
     * for a ring a few tens of metres across and not for a face ring that may run round a
     * 3800 m city: the products then reach 4e6, where a float step is a quarter of a
     * square metre and a small block's whole area is noise. This one translates to the
     * ring's own first corner and accumulates in double. The two agree in sign on every
     * block of every pinned city, which is asserted rather than assumed.
     */
    public static bool IsInteriorFace(IList<Vector2> ring)
    {
        if (ring.Count < 3) return false;

        Vector2 o = ring[0];
        double area2 = 0.0;
        for (int i = 0; i < ring.Count; ++i)
        {
            Vector2 a = ring[i], b = ring[(i + 1) % ring.Count];
            area2 += (double)(a.X - o.X) * (b.Y - o.Y) - (double)(b.X - o.X) * (a.Y - o.Y);
        }

        return area2 < 0.0;
    }


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
     * ⚠️ EVERY PIECE COMES BACK, and until WP-O2 it did not: this used to end in a
     * LargestOf that kept only the biggest, because the caller concatenated every polygon
     * of the solution into a single self-crossing ring. The caller designs one building
     * per polygon now, so a structure that cuts a block's buildable land in two leaves two
     * estates rather than one and a discarded remainder.
     *
     * @param estate
     *     The building footprint, in Clipper's tenth-metre integer coordinates.
     * @param structures
     *     Structure strokes that may lie inside it. Non-structures are ignored, so that
     *     a caller cannot widen this rule by handing it more - a ConnectorBridge is an
     *     ordinary ground road that a block is BOUNDED by, and cutting a hole out of a
     *     block for one would change every shipped flat city. The road that stands INSIDE
     *     a block is the dead-end spur, and it is named by SpurCorridorsOf instead.
     * @param margin
     *     How far outside its own carriageway a structure's footprint reaches, in
     *     metres. The block's own pavement width, so that the strip left beside a ramp
     *     is the strip left beside any other road.
     */
    public static List<List<IntPoint>> ExcludeStructures(
        List<List<IntPoint>> estate, IEnumerable<Stroke> structures, float margin)
    {
        List<Stroke> kept = null;

        foreach (var s in structures)
        {
            if (!StrokeKinds.IsStructure(s.Kind))
            {
                continue;
            }

            kept ??= new List<Stroke>();
            kept.Add(s);
        }

        return null == kept ? estate : ExcludeCarriageways(estate, kept, margin);
    }


    /**
     * ⚠️ THE ONE EXPRESSION FOR "THIS ROAD IS NOT BUILDABLE LAND": the estate less each
     * carriageway it is handed, widened by margin on every side.
     *
     * Two rules use it and they are the same rule seen twice. A grade-separated structure
     * is inside the block because the blocks either side of it merged when it left the
     * block graph (§3c, WP-B5). A dead-end spur is inside the block because the peel took
     * it out of the block graph so that the ring would close (§7u, WP-O1). Either way a
     * carriageway stands in the middle of a city block and the estate is the block's
     * outline, so without this a building is designed across a road.
     *
     * The margin is the block's own Quarter.SidewalkWidth at both call sites - the number
     * the estate is already inset by - so the strip left beside a ramp or a spur is the
     * strip left beside any other road, and no new constant enters the rule.
     *
     * ⚠️ AFTER THE INSET, NOT BEFORE IT, and the order is worth eight times the geometry:
     * measured over the seventy shipped cities, subtracting first and insetting the
     * remainder insets the notch too and splits 163 / 646 blocks against 21 / 150 this way
     * round (§7t.10.2). This is where ExcludeStructures already was, which is why WP-O2
     * put the spur exclusion here beside it rather than in front of the offset.
     *
     * ⚠️ THE OUTER CONTOURS, AND A PolyTree RATHER THAN A FLAT PATH LIST. Clipper's path
     * output puts a HOLE in the same list as the piece it is a hole in, distinguished only
     * by its winding - so "one building per polygon" over that list would design a building
     * whose outline is exactly the hole, i.e. a house standing precisely on the road that
     * made the hole. It never happened while the caller kept only the largest piece, and it
     * is one of the two things removing that choice could have got silently wrong. Measured
     * over the seventy shipped cities: 0 holes flag off and 0 flag on, asserted rather than
     * assumed - so a piece of buildable land is its whole boundary and a Building, which has
     * no way to express a hole, describes it exactly.
     *
     * Returns the SAME list it was handed when there is nothing to subtract.
     */
    public static List<List<IntPoint>> ExcludeCarriageways(
        List<List<IntPoint>> estate, IEnumerable<Stroke> strokes, float margin)
    {
        var tree = DifferenceOf(estate, strokes, margin);
        if (null == tree)
        {
            return estate;
        }

        var outers = new List<List<IntPoint>>();
        foreach (var child in tree.Childs)
        {
            outers.Add(child.Contour);
        }

        return outers;
    }


    /**
     * The same difference, as the tree Clipper builds it, so that a test can ask whether
     * any piece came back with a hole in it without a second copy of the two lines above.
     *
     * Null when there is nothing to subtract or nothing to subtract it from - the case in
     * which ExcludeCarriageways hands back the very list it was given.
     */
    internal static PolyTree DifferenceOf(
        List<List<IntPoint>> estate, IEnumerable<Stroke> strokes, float margin)
    {
        List<List<IntPoint>> clips = null;

        foreach (var s in strokes)
        {
            clips ??= new List<List<IntPoint>>();
            clips.Add(FootprintOf(s, margin));
        }

        if (null == clips || 0 == estate.Count)
        {
            return null;
        }

        var clipper = new Clipper();
        clipper.AddPaths(estate, PolyType.ptSubject, true);
        clipper.AddPaths(clips, PolyType.ptClip, true);

        var tree = new PolyTree();
        clipper.Execute(ClipType.ctDifference, tree, PolyFillType.pftNonZero,
            PolyFillType.pftNonZero);

        return tree;
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
