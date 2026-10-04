using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ClipperLib;
using engine.streets;
using engine.streets.generation;
using engine.world;

namespace JoyceCode.Tests.engine.streets;


/**
 * WHAT A CITY BLOCK WOULD LOOK LIKE IF THE BLOCK GRAPH WERE PEELED TO ITS 2-CORE FIRST,
 * and what its estate would then do with the dead-end spur standing inside it.
 *
 * This is the measurement §7t.5(b) needs and nothing else: no production file is touched
 * and nothing here is wired into the game. §7t established that a face pinched at a
 * junction by a dead-end spur is truncated into a ring closed by a chord, that the estate
 * is that ring and that the building goes across whatever the chord ran over. Option (b)
 * is to peel every junction with fewer than two block arms out of the block graph, so a
 * spur is not a block edge at all: the face then has no pinch, the ring closes, and the
 * block stands - with the spur INSIDE it, which is why the estate has to have the spur
 * subtracted out of it the way BlockGraph.ExcludeStructures already subtracts a ramp.
 *
 * ⚠️ THE QUESTION THIS ANSWERS is whether that leaves one estate with a notch in it or
 * two estates either side of the spur, and whether deciding between them needs a
 * threshold. It should not: the estate is the block inset by Quarter.SidewalkWidth, and a
 * spur corridor widened by the same SidewalkWidth leaves a land bridge round its tip of
 * exactly (tip to block boundary) minus 2 x SidewalkWidth. Where that is negative Clipper
 * returns two polygons by itself. So "inset, then count polygons" is the whole heuristic -
 * IF the structural claim holds, which is what BlockSpurEstateTests asserts.
 *
 * ⚠️ BlockGraph.ExcludeStructures CANNOT BE CALLED HERE, and that is the one place this
 * departs from the production expression. It filters its clips on StrokeKinds.IsStructure,
 * so handing it a Street returns the estate untouched - which is right for what it is for
 * and useless for measuring what a spur would do. The two lines it would run are
 * replicated below over BlockGraph.FootprintOf, the production expression for "a
 * carriageway widened by a margin", with the same ClipType and the same fill rules. What
 * is NOT replicated is BlockGraph.LargestOf, deliberately: throwing away every piece but
 * the biggest is exactly the answer this measurement is asking about.
 */
internal static class SpurBlocks
{
    /**
     * One face of the 2-cored block graph that has at least one dead-end spur standing
     * inside it.
     */
    internal sealed class BlockInfo
    {
        internal string City;
        internal int RingCount;
        internal float SidewalkWidth;
        internal float Downtownness;

        /** The spur tips inside this block, and the block edges their trees are made of. */
        internal List<StreetPoint> Tips = new();
        internal List<Stroke> Corridors = new();

        /** How many junctions of the ring the spur trees hang off, and how many of those
         * the ring passes through twice. */
        internal int Roots;
        internal int RootsTwice;
        internal bool RingRevisits;

        /** Pieces the estate comes back in: inset, then the spur corridors subtracted. */
        internal int Pieces;
        internal int Paths;
        internal int InsetPieces;

        /** The same two steps in the other order, as a sensitivity. */
        internal int AltPieces;

        /**
         * How many pieces the block outline itself comes back in when the spur corridors
         * are subtracted and NOTHING is inset - the first half of the structural claim,
         * that a dead-end corridor cannot split a polygon on its own because the land
         * wraps round its tip.
         */
        internal int SubtractOnlyPieces;

        /**
         * ...and the same with the corridor at its BARE carriageway, margin zero, which is
         * the claim in its purest form: a corridor that stops at its tip leaves the land
         * connected round it.
         */
        internal int BareSubtractPieces;

        /**
         * Where the two estates are separated, for a block that splits: how far the
         * narrowest gate between the two pieces is from the nearest spur tip. Small means
         * the split is round the tip - the mechanism the structural claim names. Large
         * means it is somewhere else along the corridor and the tip had nothing to do
         * with it.
         */
        internal float GateToTip = -1f;

        /**
         * ...and whether that gate lies BEYOND the tip along the spur's own direction,
         * which is the land bridge the structural claim is about. False means the corridor
         * pinched the estate somewhere along its flank instead, and the tip had nothing to
         * do with it.
         */
        internal bool GateBeyondTip;

        /**
         * The land bridge round the narrowest spur tip, in metres: how far the block
         * boundary is from the tip along the spur's own direction, less the two pavement
         * widths the estate inset and the corridor margin each take out of it.
         */
        internal float AxialGap = Single.MaxValue;

        /**
         * How much this block's pavement width would have to change before the verdict
         * flips: positive from one piece to two, negative from two to one. Both the estate
         * inset and the corridor margin ARE the pavement width, so this is the whole
         * sensitivity of the rule, whatever part of the block the split happens at.
         */
        internal float FlipMargin = Single.MaxValue;

        /** Whether the spurs in this block stand in a stored quarter today, and whether
         * that quarter's ring closes. */
        internal bool InAStoredBlock;
        internal bool InABrokenStoredBlock;

        internal float SmallArea, LargeArea, SmallMinSide, LargeMinSide;

        /** How much lower the block-wide lowest corner is than the lowest corner of the
         * highest piece - i.e. what BuildingFooting.BaseHeightOf's block-wide bound costs
         * the higher of two estates. */
        internal float ExtraBurial = -1f;
    }


    internal sealed class Report
    {
        internal int CoreFaces;
        internal int SpurFreeFaces;
        internal int Tips;
        internal int TipsInsideAFace;
        internal List<BlockInfo> Blocks = new();
    }


    /**
     * Every 2-cored face of one city, with the spurs inside it measured.
     *
     * @param heights
     *     Relaxed ground height per junction id for the Q6 burial measurement, or null to
     *     skip it. Read directly rather than through Quarter.CornerGroundHeightAt, which
     *     would need ClusterDesc.StreetHeightSource written on the SHARED cluster objects
     *     the world harness caches; the quantity is the same one BuildingFooting.MinGroundOf
     *     minimises, which is that source at each corner's own junction.
     */
    internal static Report Analyse(
        BlockRingClosureTests.City city, Dictionary<int, float> heights)
    {
        var report = new Report();
        var store = city.Store;
        var core = BlockGraphTests.TwoCoreOf(store);

        bool Accept(Stroke s)
            => BlockGraph.IsBlockEdge(s) && core.Contains(s.A.Id) && core.Contains(s.B.Id);

        /*
         * Which way round an interior face comes out is taken from the city's OWN stored
         * quarters rather than assumed: they are traced by the same walk, so the sign that
         * most of them carry is the sign an interior face has. The one face per connected
         * component that carries the other sign is the outside.
         */
        int nProduction = 0;
        foreach (var q in city.Quarters.GetQuarters())
        {
            nProduction += _signedArea2(
                q.GetDelims().Select(d => d.StartPoint).ToList()) > 0.0 ? 1 : -1;
        }

        double innerSign = nProduction >= 0 ? 1.0 : -1.0;

        var faces = _facesOf(store, Accept);
        var inner = new List<List<QuarterDelim>>();
        foreach (var f in faces)
        {
            double a = _signedArea2(f.Select(d => d.StartPoint).ToList());
            if (a * innerSign > 0.0) inner.Add(f);
        }

        report.CoreFaces = inner.Count;

        /*
         * Everything the peel drops: a junction with at least one block arm that is not on
         * a cycle. The tips of those trees are the dead-end spurs §7t counts.
         */
        var peeled = store.GetStreetPoints()
            .Where(sp => !core.Contains(sp.Id) && BlockGraph.ArmCountOf(sp) >= 1)
            .ToList();
        var tips = peeled.Where(sp => 1 == BlockGraph.ArmCountOf(sp)).ToList();
        report.Tips = tips.Count;

        var peeledIds = new HashSet<int>(peeled.Select(sp => sp.Id));

        /*
         * The blocks the game stores TODAY, so that "how many blocks does option (b)
         * touch" can be answered against what is drawn rather than against §7t's counts
         * in another file. §7t asked the same containment question of the stored quarters
         * and found 222 / 272 spurs inside one; the difference between that and the count
         * here is §7t.4's third of the city, where no block exists at all.
         */
        var stored = city.Quarters.GetQuarters()
            .Select(q => (
                Ring: q.GetDelims().Select(d => d.StartPoint).ToList(),
                Broken: BlockRingClosureTests.RingIsBroken(q)))
            .Select(t => (t.Ring, t.Broken, Bounds: _boundsOf(t.Ring)))
            .ToList();

        foreach (var f in inner)
        {
            var ring = f.Select(d => d.StartPoint).ToList();
            var (x0, y0, x1, y1) = _boundsOf(ring);

            var inside = new List<StreetPoint>();
            foreach (var sp in peeled)
            {
                if (sp.Pos.X < x0 || sp.Pos.X > x1 || sp.Pos.Y < y0 || sp.Pos.Y > y1) continue;
                if (BlockRingClosureTests.InsidePlan(ring, sp.Pos)) inside.Add(sp);
            }

            var insideTips = inside.Where(sp => 1 == BlockGraph.ArmCountOf(sp)).ToList();
            if (0 == insideTips.Count)
            {
                ++report.SpurFreeFaces;
                continue;
            }

            report.TipsInsideAFace += insideTips.Count;

            var info = _measure(city, f, ring, inside, insideTips, peeledIds, heights);

            foreach (var tip in insideTips)
            {
                foreach (var (sRing, broken, b) in stored)
                {
                    if (tip.Pos.X < b.Item1 || tip.Pos.X > b.Item3
                        || tip.Pos.Y < b.Item2 || tip.Pos.Y > b.Item4) continue;
                    if (!BlockRingClosureTests.InsidePlan(sRing, tip.Pos)) continue;

                    info.InAStoredBlock = true;
                    if (broken) info.InABrokenStoredBlock = true;
                }
            }

            report.Blocks.Add(info);
        }

        return report;
    }


    private static BlockInfo _measure(
        BlockRingClosureTests.City city, List<QuarterDelim> f, List<Vector2> ring,
        List<StreetPoint> inside, List<StreetPoint> insideTips,
        HashSet<int> peeledIds, Dictionary<int, float> heights)
    {
        var info = new BlockInfo
        {
            City = city.Cluster.IdString,
            RingCount = ring.Count,
            Tips = insideTips
        };

        /*
         * A real Quarter, so that SidewalkWidth is the block's own number rather than a
         * second copy of the downtown ladder - it is read off the AABB the delimiters
         * build, exactly as the production block does.
         */
        var quarter = new Quarter { ClusterDesc = city.Cluster };
        foreach (var d in f) quarter.AddQuarterDelim(d);

        float sw = quarter.SidewalkWidth;
        info.SidewalkWidth = sw;

        var centre = quarter.GetCenterPoint();
        info.Downtownness = city.Cluster.GetAttributeIntensity(
            city.Cluster.Pos + new Vector3(centre.X, 0f, centre.Y),
            ClusterDesc.LocationAttributes.Downtown);

        var insideIds = new HashSet<int>(inside.Select(sp => sp.Id));
        foreach (var s in city.Store.GetStrokes())
        {
            if (!BlockGraph.IsBlockEdge(s)) continue;
            if (insideIds.Contains(s.A.Id) || insideIds.Contains(s.B.Id)) info.Corridors.Add(s);
        }

        /*
         * Which junctions of the ring the spur trees hang off, and whether the ring passes
         * through one of them twice - the shape §7t.2's pinch has, asked of the 2-cored
         * ring instead.
         */
        var roots = new HashSet<int>();
        foreach (var s in info.Corridors)
        {
            if (!peeledIds.Contains(s.A.Id)) roots.Add(s.A.Id);
            if (!peeledIds.Contains(s.B.Id)) roots.Add(s.B.Id);
        }

        var ringIds = f.Select(d => d.StreetPoint.Id).ToList();
        info.Roots = roots.Count;
        info.RootsTwice = roots.Count(id => ringIds.Count(x => x == id) > 1);
        info.RingRevisits = ringIds.Count != ringIds.Distinct().Count();

        /*
         * The estate, exactly as QuarterGenerator._createBuildings builds it: the ring in
         * Clipper's tenth metres, offset inwards by the block's own pavement width.
         */
        var poly = ring.Select(v => new IntPoint((int)(v.X * 10f), (int)(v.Y * 10f))).ToList();
        var inset = _insetBy(poly, sw);
        info.InsetPieces = inset.Count;

        var clips = info.Corridors.Select(s => BlockGraph.FootprintOf(s, sw)).ToList();

        var tree = _difference(inset, clips);
        info.Pieces = tree.Childs.Count;
        info.Paths = Clipper.PolyTreeToPaths(tree).Count;

        /*
         * The other order - subtract from the block outline, then inset what is left - as
         * a sensitivity, because it is the order the question could equally have been
         * asked in and it widens the notch by a further pavement width.
         */
        var altTree = _difference(new List<List<IntPoint>> { poly }, clips);
        info.SubtractOnlyPieces = altTree.Childs.Count;
        info.BareSubtractPieces = _difference(
            new List<List<IntPoint>> { poly },
            info.Corridors.Select(s => BlockGraph.FootprintOf(s, 0f)).ToList()).Childs.Count;

        int alt = 0;
        foreach (var child in altTree.Childs)
        {
            alt += _insetBy(child.Contour, sw).Count;
        }

        info.AltPieces = alt;

        var pieces = tree.Childs
            .Select(c => (Poly: c.Contour, Area: _areaOf(c), Side: _minSideOf(c.Contour)))
            .OrderByDescending(t => t.Area)
            .ToList();

        if (pieces.Count >= 2)
        {
            info.LargeArea = pieces[0].Area;
            info.LargeMinSide = pieces[0].Side;
            info.SmallArea = pieces[1].Area;
            info.SmallMinSide = pieces[1].Side;

            if (null != heights) info.ExtraBurial = _extraBurial(f, pieces, heights);

            var (gateDistance, beyond) = _gateOf(pieces[0].Poly, pieces[1].Poly, insideTips);
            info.GateToTip = gateDistance;
            info.GateBeyondTip = beyond;
        }

        info.AxialGap = _axialGapOf(ring, insideTips, sw);
        info.FlipMargin = _flipMarginOf(poly, info.Corridors, sw, info.Pieces);

        return info;
    }


    /**
     * How much land wraps round the narrowest spur tip in this block, in metres.
     *
     * Cast a ray from the tip along the spur's own direction to the block's outline: that
     * distance, less the pavement width the estate is inset by and the pavement width the
     * corridor is widened by, is what is left for the land to go round through. Negative
     * means the two have met and the estate is in two pieces - which is the structural
     * claim §7t.5(b) rests on, and it is asserted rather than assumed.
     *
     * A ray rather than a nearest-point search, because "the block boundary BEYOND the
     * tip" has to mean beyond it along the spur; the nearest boundary point in the forward
     * half plane is often beside a different spur's corridor in a block that has several.
     */
    private static float _axialGapOf(
        List<Vector2> ring, List<StreetPoint> tips, float sidewalkWidth)
    {
        float best = Single.MaxValue;

        foreach (var tip in tips)
        {
            var stub = tip.GetAngleArray().FirstOrDefault(BlockGraph.IsBlockEdge);
            if (null == stub) continue;

            Vector2 u = stub.B == tip ? stub.Unit : -stub.Unit;
            float d = _rayToRing(ring, tip.Pos, u);
            if (Single.MaxValue == d) continue;

            best = Single.Min(best, d - 2f * sidewalkWidth);
        }

        return best;
    }


    private static float _rayToRing(List<Vector2> ring, in Vector2 o, in Vector2 u)
    {
        float best = Single.MaxValue;

        for (int i = 0; i < ring.Count; ++i)
        {
            var a = ring[i];
            var b = ring[(i + 1) % ring.Count];
            var e = b - a;

            float denominator = u.X * e.Y - u.Y * e.X;
            if (Single.Abs(denominator) < 1e-9f) continue;

            var w = a - o;
            float t = (w.X * e.Y - w.Y * e.X) / denominator;
            float s = (w.X * u.Y - w.Y * u.X) / denominator;

            if (t > 1e-4f && s >= 0f && s <= 1f) best = Single.Min(best, t);
        }

        return best;
    }


    /**
     * How far this block's pavement width would have to move for the verdict to change.
     *
     * ⚠️ THE POINT OF ASKING IT THIS WAY. Both halves of the rule are the same number -
     * the estate is inset by Quarter.SidewalkWidth and the spur corridor is widened by
     * Quarter.SidewalkWidth - so perturbing that one number perturbs the whole rule, and
     * it does so wherever the block is narrowest rather than only round the tip. A verdict
     * that flips on a decimetre of it is a verdict decided by rounding.
     *
     * Positive: how much WIDER the pavement would have to be to turn one estate into two.
     * Negative: how much narrower to turn two back into one. Bisected to a millimetre and
     * capped, since a block that never splits however wide the pavement gets is not near
     * the decision at all.
     */
    internal const float FlipMarginCap = 32f;


    private static float _flipMarginOf(
        List<IntPoint> poly, List<Stroke> corridors, float sidewalkWidth, int pieces)
    {
        bool SplitAt(float w)
        {
            var inset = _insetBy(poly, w);
            if (0 == inset.Count) return false;

            return _difference(
                inset, corridors.Select(s => BlockGraph.FootprintOf(s, w)).ToList())
                .Childs.Count >= 2;
        }

        if (1 == pieces)
        {
            /*
             * ⚠️ Coarse first, and the reason matters: past some width the estate stops
             * existing at all rather than becoming two, and a bisection on "is it still
             * one piece" cannot tell those apart - it would report the width at which the
             * block VANISHES as the width at which it splits. The predicate here is "are
             * there two or more", which 0 does not satisfy.
             */
            float found = -1f;
            for (float w = 0.5f; w <= FlipMarginCap + 1e-3f; w += 0.5f)
            {
                if (!SplitAt(sidewalkWidth + w)) continue;

                found = w;
                break;
            }

            if (found < 0f) return FlipMarginCap;

            float lo = found - 0.5f, hi = found;
            for (int i = 0; i < 12; ++i)
            {
                float mid = 0.5f * (lo + hi);
                if (SplitAt(sidewalkWidth + mid)) hi = mid; else lo = mid;
            }

            return hi;
        }

        {
            float lo = -sidewalkWidth + 0.05f, hi = 0f;
            if (SplitAt(sidewalkWidth + lo)) return lo;

            for (int i = 0; i < 16; ++i)
            {
                float mid = 0.5f * (lo + hi);
                if (SplitAt(sidewalkWidth + mid)) hi = mid; else lo = mid;
            }

            return lo;
        }
    }


    /*
     * ============================================== the 2-cored face walk ============
     */

    /**
     * Every face of the block graph restricted to accept, walked exactly as
     * QuarterGenerator walks one - with the termination §7t.5(a) says it should have had,
     * on the (junction, outgoing stroke) PAIR rather than on the junction.
     *
     * The corner of each delimiter is StreetPoint.SectionPointBetween, which is what the
     * production trace files it from, and it depends only on the two arms the block turns
     * between - so a peeled spur arm changes no corner of the 2-cored ring.
     */
    private static List<List<QuarterDelim>> _facesOf(
        StrokeStore store, Func<Stroke, bool> accept)
    {
        var seen = new HashSet<(int, bool)>();
        var faces = new List<List<QuarterDelim>>();

        foreach (var sp in store.GetStreetPoints())
        {
            foreach (var s in sp.GetAngleArray())
            {
                if (!accept(s)) continue;

                bool isAB = s.A == sp;
                if (seen.Contains((s.Sid, isAB))) continue;

                var face = _walk(sp, s, accept, seen);
                if (null != face) faces.Add(face);
            }
        }

        return faces;
    }


    private static List<QuarterDelim> _walk(
        StreetPoint spStart, Stroke first, Func<Stroke, bool> accept,
        HashSet<(int, bool)> seen)
    {
        var delims = new List<QuarterDelim>();
        var spCurr = spStart;
        var strokeCurrent = first;

        for (int n = 0; n < 20000; ++n)
        {
            bool isAB;
            StreetPoint spNext;
            if (strokeCurrent.A == spCurr) { isAB = true; spNext = strokeCurrent.B; }
            else if (strokeCurrent.B == spCurr) { isAB = false; spNext = strokeCurrent.A; }
            else return null;

            if (!seen.Add((strokeCurrent.Sid, isAB))) return null;

            float followAngle = global::engine.geom.Angles.Snorm(
                strokeCurrent.Angle + (!isAB ? (float)Math.PI : 0f));

            var strokeNext = spNext.GetNextAngle(strokeCurrent, followAngle, true, accept);
            if (null == strokeNext) return null;

            var d = new QuarterDelim();
            d.SetEdge(spNext.SectionPointBetween(strokeCurrent, strokeNext), spNext, strokeNext);
            delims.Add(d);

            if (spNext == spStart && strokeNext == first) return delims;

            strokeCurrent = strokeNext;
            spCurr = spNext;
        }

        return null;
    }


    /*
     * ============================================== the geometry =====================
     */

    private static List<List<IntPoint>> _insetBy(List<IntPoint> poly, float widthMetres)
    {
        var offset = new ClipperOffset();
        offset.AddPath(poly, JoinType.jtMiter, EndType.etClosedPolygon);

        var solution = new List<List<IntPoint>>();
        offset.Execute(ref solution, -widthMetres * 10f);
        return solution;
    }


    private static PolyTree _difference(
        List<List<IntPoint>> subject, List<List<IntPoint>> clips)
    {
        var clipper = new Clipper();
        var tree = new PolyTree();

        if (0 == subject.Count) return tree;

        clipper.AddPaths(subject, PolyType.ptSubject, true);
        if (clips.Count > 0) clipper.AddPaths(clips, PolyType.ptClip, true);

        clipper.Execute(ClipType.ctDifference, tree,
            PolyFillType.pftNonZero, PolyFillType.pftNonZero);

        return tree;
    }


    /**
     * Net area of one piece in square metres: its outer contour less the holes in it.
     */
    private static float _areaOf(PolyNode node)
    {
        double a = Math.Abs(Clipper.Area(node.Contour));
        foreach (var hole in node.Childs) a -= Math.Abs(Clipper.Area(hole.Contour));
        return (float)(a / 100.0);
    }


    /**
     * The shortest side of a piece, in metres - QuarterGenerator._createBuildings'
     * minHouseSide, which is what decides whether a building may be more than one storey.
     */
    private static float _minSideOf(List<IntPoint> poly)
    {
        float min = Single.MaxValue;
        for (int i = 0; i < poly.Count; ++i)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            float dx = (b.X - a.X) / 10f;
            float dy = (b.Y - a.Y) / 10f;
            min = Single.Min(min, MathF.Sqrt(dx * dx + dy * dy));
        }

        return poly.Count > 0 ? min : 0f;
    }


    /**
     * How far the narrowest gate between two estate pieces is from the nearest spur tip.
     *
     * The two pieces were one polygon before the corridor was taken out of it, so the
     * place where they come closest is the place the corridor cut through. If the land
     * bridge that closed was the one round the tip, that place is beside the tip.
     */
    private static (float, bool) _gateOf(
        List<IntPoint> a, List<IntPoint> b, List<StreetPoint> tips)
    {
        var pa = _sample(a);
        var pb = _sample(b);

        float best = Single.MaxValue;
        Vector2 gate = Vector2.Zero;

        foreach (var p in pa)
        foreach (var q in pb)
        {
            float d = Vector2.DistanceSquared(p, q);
            if (d >= best) continue;

            best = d;
            gate = 0.5f * (p + q);
        }

        if (Single.MaxValue == best) return (-1f, false);

        float nearest = Single.MaxValue;
        bool beyond = false;

        foreach (var tip in tips)
        {
            float d = Vector2.Distance(gate, tip.Pos);
            if (d >= nearest) continue;

            nearest = d;

            var stub = tip.GetAngleArray().FirstOrDefault(BlockGraph.IsBlockEdge);
            Vector2 u = null == stub ? Vector2.Zero
                : stub.B == tip ? stub.Unit : -stub.Unit;
            beyond = Vector2.Dot(gate - tip.Pos, u) > 0f;
        }

        return (nearest, beyond);
    }


    private static List<Vector2> _sample(List<IntPoint> poly)
    {
        var pts = new List<Vector2>();
        for (int i = 0; i < poly.Count; ++i)
        {
            var a = new Vector2(poly[i].X / 10f, poly[i].Y / 10f);
            var b = new Vector2(
                poly[(i + 1) % poly.Count].X / 10f, poly[(i + 1) % poly.Count].Y / 10f);

            int steps = Math.Max(1, (int)((b - a).Length() * 2f));
            for (int k = 0; k < steps; ++k) pts.Add(Vector2.Lerp(a, b, k / (float)steps));
        }

        return pts;
    }


    private static float _distanceTo(List<Vector2> poly, in Vector2 p)
    {
        float d = Single.MaxValue;
        for (int i = 0; i < poly.Count; ++i)
        {
            d = Single.Min(d, _segmentDistance(poly[i], poly[(i + 1) % poly.Count], p));
        }

        return d;
    }


    private static float _segmentDistance(in Vector2 a, in Vector2 b, in Vector2 p)
    {
        var ab = b - a;
        float l2 = ab.LengthSquared();
        if (l2 < 1e-9f) return (p - a).Length();

        float t = Math.Clamp(Vector2.Dot(p - a, ab) / l2, 0f, 1f);
        return (p - (a + ab * t)).Length();
    }


    /**
     * How much lower the block-wide lowest corner is than the lowest corner of the highest
     * piece.
     *
     * BuildingFooting.BaseHeightOf answers the block's lowest corner and everything on the
     * block stands on it, justified by "a block carries exactly one estate and at most one
     * building". Split it in two and the higher piece is buried by the difference.
     * Corners are assigned to the piece they are nearest to.
     */
    private static float _extraBurial(
        List<QuarterDelim> f, List<(List<IntPoint> Poly, float Area, float Side)> pieces,
        Dictionary<int, float> heights)
    {
        var mins = new float[pieces.Count];
        for (int i = 0; i < mins.Length; ++i) mins[i] = Single.MaxValue;

        float blockMin = Single.MaxValue;

        foreach (var d in f)
        {
            if (!heights.TryGetValue(d.StreetPoint.Id, out float h)) continue;

            blockMin = Single.Min(blockMin, h);

            int best = -1;
            float bestD = Single.MaxValue;
            for (int i = 0; i < pieces.Count; ++i)
            {
                var poly = pieces[i].Poly.Select(p => new Vector2(p.X / 10f, p.Y / 10f)).ToList();
                float dist = _distanceTo(poly, d.StartPoint);
                if (dist < bestD) { bestD = dist; best = i; }
            }

            if (best >= 0) mins[best] = Single.Min(mins[best], h);
        }

        float worst = 0f;
        foreach (float m in mins)
        {
            if (m < Single.MaxValue && blockMin < Single.MaxValue)
            {
                worst = Single.Max(worst, m - blockMin);
            }
        }

        return worst;
    }


    private static double _signedArea2(List<Vector2> ring)
    {
        double a = 0.0;
        for (int i = 0; i < ring.Count; ++i)
        {
            var p = ring[i];
            var q = ring[(i + 1) % ring.Count];
            a += (double)p.X * q.Y - (double)q.X * p.Y;
        }

        return a;
    }


    private static (float, float, float, float) _boundsOf(List<Vector2> ring)
    {
        float x0 = Single.MaxValue, y0 = Single.MaxValue;
        float x1 = Single.MinValue, y1 = Single.MinValue;
        foreach (var p in ring)
        {
            x0 = Single.Min(x0, p.X); x1 = Single.Max(x1, p.X);
            y0 = Single.Min(y0, p.Y); y1 = Single.Max(y1, p.Y);
        }

        return (x0, y0, x1, y1);
    }
}
