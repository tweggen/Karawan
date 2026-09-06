using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ClipperLib;
using engine.streets;
using engine.streets.generation;
using engine.world;
using JoyceCode.Tests.engine.world;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * ⚠️ A CITY BLOCK'S OUTLINE IS NOT ALWAYS MADE OF STREETS, AND A BUILDING THEN STANDS ON
 * ONE.
 *
 * Reported from play - "I do see a street trunk running off into a building" - and
 * measured here rather than fixed, because the fix is a design decision and the size of it
 * is what the decision needs (see CompletingTheWalkWouldDiscardTheBlockAltogether below).
 *
 * ⚠️ NOT grade separation, and not the terrain-following city either. Both were the
 * obvious suspects and both are refuted by measurement in this file: the counts are
 * recorded with joyce.EnableGradeSeparation BOTH ways, and TheFlagOffNetworkIsTheFlatCity
 * shows the flag-off plan network is the same graph on all seventy cities whether the
 * ground is flat or the shipped terrain - so everything measured flag-off is the flat city
 * and predates the whole three-dimensional-city work stream.
 *
 * WHAT IS WRONG. QuarterGenerator.Generate() walks a face of the street graph and stops
 * when it arrives back at the junction it started from:
 *
 *     if (spNext == spStart) break;
 *
 * That is a VERTEX test. A face walk has to stop on the (junction, outgoing stroke) PAIR it
 * started from, because a face may pass through one junction twice - which is exactly what
 * a face does when a dead-end spur cuts a slit into a block. When the doubly-visited
 * junction happens to be the one the walk started at, the walk stops half way round, and
 * the ring is then closed by a straight chord from the last delimiter back to the first.
 * That chord is not a street: it is a line across the block, median 75 m long and up to
 * 147 m, and it can cross roads. The estate is that outline, _createBuildings insets it by
 * the pavement width, and the building is put across whatever the chord ran over.
 *
 * ⚠️ THE ODD PART IS THAT SUCH A FACE IS SUPPOSED TO BE THROWN AWAY. A junction with
 * fewer than two block arms has no corner, the quarter is tagged hasNullSection and
 * discarded - which is what happens to a THIRD of all faces (see
 * AThirdOfTheBlockEdgesAreInFacesNobodyBuildsOn). The early termination cuts the spur out
 * of the ring before the trace can see it, so the discard never happens. It is §11.4's
 * shape inverted: not a refusal that looks like a result, but a result that should have
 * been a refusal.
 */
public class BlockRingClosureTests
{
    private static readonly object _lo = new();

    private sealed class City
    {
        internal ClusterDesc Cluster;
        internal StrokeStore Store;
        internal QuarterStore Quarters;
    }

    private static readonly Dictionary<bool, List<City>> _worlds = new();


    /**
     * Every city of the shipped world, with its blocks traced.
     *
     * The cluster list is GenerateClustersOperator's own, seeded as MetaGen seeds it, so
     * this is the seventy cities the game lays out rather than the seven pinned fixture
     * seeds - which carry only 10 of these between them (ThePinnedSeedsCarryThisMany).
     */
    private static List<City> World(bool gradeSeparation)
    {
        lock (_lo)
        {
            if (_worlds.TryGetValue(gradeSeparation, out var cached)) return cached;

            var world = new List<City>();
            foreach (var cd in IntercityWorldHarness.Clusters())
            {
                StrokeStore store;
                if (gradeSeparation)
                {
                    (store, _) = StreetHarness.GenerateHeavyFirstReporting(
                        cd.IdString, cd.Size,
                        sp => ShippedTerrain.HeightAt(
                            cd.Pos.X + sp.Pos.X, cd.Pos.Z + sp.Pos.Y));
                }
                else
                {
                    store = StreetHarness.Generate(cd.IdString, cd.Size);
                }

                world.Add(new City
                {
                    Cluster = cd,
                    Store = store,
                    Quarters = StreetHarness.GenerateQuarters(cd, store, cd.IdString)
                });
            }

            _worlds[gradeSeparation] = world;
            return world;
        }
    }


    /**
     * The index of the first delimiter whose own stroke does not join it to the next one,
     * or -1 when the ring closes.
     *
     * delims[i] carries the corner at junction i and the stroke LEAVING it, so the two ends
     * of delims[i].Stroke must be delims[i].StreetPoint and delims[i+1].StreetPoint. This
     * is asked on IDENTITY - the two junctions of the stroke, not their positions - because
     * two junctions of one city can be metres apart (§7e) and a distance test could not
     * tell a skipped junction from a short street.
     */
    private static int _firstOpenEdge(Quarter q)
    {
        var d = q.GetDelims();
        for (int i = 0; i < d.Count; ++i)
        {
            var a = d[i].StreetPoint;
            var b = d[(i + 1) % d.Count].StreetPoint;
            var s = d[i].Stroke;

            if (!((s.A == a && s.B == b) || (s.B == a && s.A == b))) return i;
        }

        return -1;
    }


    /*
     * ============================================== the bisections ===================
     */

    /**
     * ⚠️ THE FIRST MEASUREMENT, AND IT REFUTES THE OBVIOUS SUSPECT.
     *
     * The flag-off plan network is the same graph whether the ground under it is flat or
     * the shipped terrain, on every one of the seventy cities - the height source reaches
     * the generator only through GroundHeightOf, which nothing but the structure placement
     * pass reads, and that pass does not run with the flag off.
     *
     * So every count in this file taken with the flag off is a count taken on the FLAT
     * city, and this defect is older than joyce.DisableClusterFlattening.
     */
    [Fact]
    public void TheFlagOffNetworkIsTheFlatCity()
    {
        int nSame = 0;
        foreach (var cd in IntercityWorldHarness.Clusters())
        {
            var flat = StreetHarness.Generate(cd.IdString, cd.Size);
            var terrain = StreetHarness.Generate(
                cd.IdString, cd.Size, null, false, null);

            Assert.Equal(flat.GetStrokes().Count, terrain.GetStrokes().Count);

            var a = flat.GetStrokes().OrderBy(s => s.Sid).ToList();
            var b = terrain.GetStrokes().OrderBy(s => s.Sid).ToList();
            for (int i = 0; i < a.Count; ++i)
            {
                Assert.Equal(a[i].A.Pos, b[i].A.Pos);
                Assert.Equal(a[i].B.Pos, b[i].B.Pos);
            }

            ++nSame;
        }

        Assert.Equal(70, nSame);
    }


    /*
     * ============================================== the defect =======================
     */

    /**
     * ⚠️ HOW MANY BLOCK OUTLINES ARE NOT MADE OF STREETS, over the world the game builds.
     *
     * Recorded as exact equalities, in the idiom of
     * ShippedWorldStructureTests.TheShippedWorldGetsThisManyStructures: these are the
     * numbers a fix has to move, and a ruleset change that moves them should be read
     * rather than silently tolerated.
     *
     * ⚠️ AND EVERY ONE OF THEM IS BROKEN AT THE WRAP-AROUND EDGE AND NOWHERE ELSE - 219 of
     * 219 and 274 of 274. That is what says the cause is the termination condition and not,
     * say, a delimiter written from two steps of the trace (§7e, which looked exactly like
     * this and was broken at every edge).
     */
    [Theory]
    //                       quarters  broken
    [InlineData(false, 36327, 219)]
    [InlineData(true, 33432, 274)]
    public void ABlockRingIsClosedByStreetsExceptWhenItIsNot(
        bool gradeSeparation, int expectedQuarters, int expectedBroken)
    {
        int nQuarters = 0, nBroken = 0, nAtWrap = 0;

        foreach (var city in World(gradeSeparation))
        {
            foreach (var q in city.Quarters.GetQuarters())
            {
                ++nQuarters;
                int open = _firstOpenEdge(q);
                if (open < 0) continue;

                ++nBroken;
                if (open == q.GetDelims().Count - 1) ++nAtWrap;
            }
        }

        Assert.Equal(expectedQuarters, nQuarters);
        Assert.Equal(expectedBroken, nBroken);

        /*
         * The whole diagnosis in one number: the only edge that is ever open is the one
         * that closes the ring, i.e. the one the walk never traversed.
         */
        Assert.Equal(nBroken, nAtWrap);
    }


    /**
     * ⚠️ WHAT IT COSTS: the building on that block stands on a street.
     *
     * Of the broken rings that carry a building at all, every one puts it across an
     * ordinary Street's carriageway - 146 of 146 with the flag off and 190 of 191 with it
     * on - by a median 622 and 1503 square metres.
     *
     * ⚠️ THE CONTROL IS THE POINT. Without it "every broken ring's building is on a
     * street" would also be satisfied by a city in which every building is on a street.
     * Measured over all 24293 / 22036 buildings of the world, the blocks whose ring DOES
     * close contribute 10 and 1 - and the ten flag-off ones are 1 to 13 square metre
     * slivers at a corner, three orders of magnitude below the broken-ring cases. The one
     * flag-on outlier at 1881 square metres is recorded and NOT explained; it is a second,
     * far smaller mechanism and it is left open.
     */
    [Theory]
    //                     broken-ring buildings   of which on a street   closed-ring cases
    [InlineData(false, 146, 146, 10)]
    [InlineData(true, 191, 190, 1)]
    public void ABuildingOnABrokenRingStandsOnAStreet(
        bool gradeSeparation, int onBroken, int onBrokenOverStreet, int onClosed)
    {
        int nOnBroken = 0, nOnBrokenOverStreet = 0, nOnClosedOverStreet = 0;

        foreach (var city in World(gradeSeparation))
        {
            var boxes = city.Store.GetStrokes()
                .Select(s => (Poly: BlockGraph.FootprintOf(s, 0f), s.A.Pos, s.B.Pos))
                .Select(t => (t.Poly, Box: _boxOf(t.Poly)))
                .ToList();

            foreach (var q in city.Quarters.GetQuarters())
            {
                bool broken = _firstOpenEdge(q) >= 0;

                foreach (var e in q.GetEstates())
                foreach (var b in e.GetBuildings())
                {
                    if (broken) ++nOnBroken;

                    var pts = b.GetPoints();
                    var bbox = _boxOf(pts.Select(
                        v => new IntPoint((int)(v.X * 10f), (int)(v.Z * 10f))).ToList());

                    bool over = false;
                    foreach (var (poly, box) in boxes)
                    {
                        if (!_hit(bbox, box)) continue;
                        if (_overlapArea(pts, poly) > 0.5) { over = true; break; }
                    }

                    if (!over) continue;
                    if (broken) ++nOnBrokenOverStreet; else ++nOnClosedOverStreet;
                }
            }
        }

        Assert.Equal(onBroken, nOnBroken);
        Assert.Equal(onBrokenOverStreet, nOnBrokenOverStreet);
        Assert.Equal(onClosed, nOnClosedOverStreet);
    }


    /**
     * ⚠️ AND IT IS NOT A ONE-LINE FIX, WHICH IS WHY THIS IS RECORDED RATHER THAN REPAIRED.
     *
     * Walk the same face again with the termination the walk should have - the starting
     * (junction, stroke) pair rather than the starting junction - and look at what comes
     * back. Every one of them closes, every one of them visits a junction twice, and
     * essentially every one of them contains a junction with fewer than two block arms:
     * a dead-end spur. Such a face is exactly what the existing hasNullSection rule
     * DISCARDS.
     *
     * So terminating correctly does not repair these blocks, it deletes them: 219 blocks
     * of the flag-off world and 272 of the flag-on one would stop existing, leaving no
     * estate, no building and no pavement where one is drawn today. Whether that or
     * something larger - peeling the block graph to its 2-core so a spur is not a block
     * edge at all, and then excluding it from the estate the way a ramp already is - is
     * the right answer is a decision about what the city should look like.
     */
    [Theory]
    //                    broken  completed  revisit a junction  would be discarded
    [InlineData(false, 219, 219, 219, 219)]
    [InlineData(true, 274, 274, 274, 272)]
    public void CompletingTheWalkWouldDiscardTheBlockAltogether(
        bool gradeSeparation, int broken, int completed, int revisiting, int discarded)
    {
        int nBroken = 0, nCompleted = 0, nRevisiting = 0, nDiscarded = 0;

        foreach (var city in World(gradeSeparation))
        {
            foreach (var q in city.Quarters.GetQuarters())
            {
                var d = q.GetDelims();
                if (_firstOpenEdge(q) < 0) continue;

                ++nBroken;

                /*
                 * The walk started at the LAST delimiter's junction - that is where
                 * spNext == spStart fired - and left it along the stroke whose far end is
                 * the first delimiter's junction.
                 */
                var spStart = d[^1].StreetPoint;
                var second = d[0].StreetPoint;
                var first = spStart.GetAngleArray().FirstOrDefault(
                    s => BlockGraph.IsBlockEdge(s)
                         && ((s.A == spStart && s.B == second)
                             || (s.B == spStart && s.A == second)));

                Assert.NotNull(first);

                var path = _walkFace(spStart, first);
                Assert.NotNull(path);

                ++nCompleted;
                if (path.Count != path.Distinct().Count()) ++nRevisiting;
                if (path.Any(sp => BlockGraph.ArmCountOf(sp) < 2)) ++nDiscarded;

                /*
                 * ...and the completed face is strictly bigger than the truncated ring,
                 * which is what says the truncation lost something rather than merely
                 * naming it differently.
                 */
                Assert.True(path.Count > d.Count);
            }
        }

        Assert.Equal(broken, nBroken);
        Assert.Equal(completed, nCompleted);
        Assert.Equal(revisiting, nRevisiting);
        Assert.Equal(discarded, nDiscarded);
    }


    /**
     * The context this sits in, and it is larger than the defect by two orders of
     * magnitude: a THIRD of the street graph's directed block edges are in faces that get
     * thrown away for hasNullSection, so a third of the city has no block, no estate, no
     * building and no pavement. 8.8 % of junctions are dead-end spurs and every face
     * touching one is discarded.
     *
     * Pre-existing, untouched here, and recorded because the fix chosen for the ring will
     * be a decision about this number.
     */
    [Theory]
    //                   directed block edges  in stored quarters   one-armed junctions
    [InlineData(false, 304150, 203037, 9840)]
    [InlineData(true, 228348, 163558, 8100)]
    public void AThirdOfTheBlockEdgesAreInFacesNobodyBuildsOn(
        bool gradeSeparation, int directed, int stored, int spurs)
    {
        int nDirected = 0, nStored = 0, nSpurs = 0;

        foreach (var city in World(gradeSeparation))
        {
            nDirected += 2 * city.Store.GetStrokes().Count(BlockGraph.IsBlockEdge);
            foreach (var q in city.Quarters.GetQuarters()) nStored += q.GetDelims().Count;
            foreach (var sp in city.Store.GetStreetPoints())
            {
                if (1 == BlockGraph.ArmCountOf(sp)) ++nSpurs;
            }
        }

        Assert.Equal(directed, nDirected);
        Assert.Equal(stored, nStored);
        Assert.Equal(spurs, nSpurs);
    }


    /**
     * ⚠️ WHAT A FIX WOULD MOVE IN THE RECORDED BASELINES.
     *
     * The seven pinned seeds are a fixture, not the world, and between them they carry ten
     * of these - so reading the defect off them would have found three cities in seven and
     * called it rare. street-geometry.json pins block geometry for seed000@500,
     * seed011@500, Yelukhdidru@800, seed000@1500 and seed008@500: of those, only
     * seed008@500 carries one at all, and only with the flag on.
     *
     * Recorded per seed so that whoever repairs this knows, before starting, exactly which
     * recorded baseline moves.
     */
    public static IEnumerable<object[]> PinnedSeeds => new List<object[]>
    {
        //                                 quarters/broken off    quarters/broken on
        new object[] { "seed000", 500f, 3, 0, 2, 0 },
        new object[] { "seed011", 500f, 2, 0, 3, 0 },
        new object[] { "Yelukhdidru", 400f, 0, 0, 0, 0 },
        new object[] { "Yelukhdidru", 800f, 10, 0, 7, 0 },
        new object[] { "seed000", 1500f, 82, 0, 61, 1 },
        new object[] { "seed017", 2400f, 221, 2, 165, 2 },
        new object[] { "Yelukhdidru", 3000f, 445, 3, 282, 4 },
        new object[] { "seed008", 500f, 4, 0, 5, 1 },
    };


    [Theory]
    [MemberData(nameof(PinnedSeeds))]
    public void ThePinnedSeedsCarryThisMany(
        string idString, float size,
        int quartersOff, int brokenOff, int quartersOn, int brokenOn)
    {
        foreach (bool gradeSeparation in new[] { false, true })
        {
            var cluster = StreetHarness.MakeCluster(idString, size);
            var store = gradeSeparation
                ? StreetHarness.GenerateHeavyFirst(idString, size)
                : StreetHarness.Generate(idString, size);
            var quarters = StreetHarness.GenerateQuarters(cluster, store, idString);

            int nBroken = quarters.GetQuarters().Count(q => _firstOpenEdge(q) >= 0);

            Assert.Equal(gradeSeparation ? quartersOn : quartersOff,
                quarters.GetQuarters().Count);
            Assert.Equal(gradeSeparation ? brokenOn : brokenOff, nBroken);
        }
    }


    /*
     * ============================================== plumbing =========================
     */

    /**
     * One face of the block graph, walked from (spStart, first) and terminating on that
     * DIRECTED EDGE rather than on spStart alone. Returns the junctions in order, or null
     * if it ran away.
     *
     * A copy of QuarterGenerator's own walk, deliberately: the point is to measure what a
     * different termination condition would produce, and that cannot be asked of the
     * production code without changing it.
     */
    private static List<StreetPoint> _walkFace(StreetPoint spStart, Stroke first)
    {
        var visited = new List<StreetPoint>();
        var spCurr = spStart;
        var strokeCurrent = first;

        for (int n = 0; n < 4000; ++n)
        {
            StreetPoint spNext;
            bool isAB;
            if (strokeCurrent.A == spCurr) { isAB = true; spNext = strokeCurrent.B; }
            else if (strokeCurrent.B == spCurr) { isAB = false; spNext = strokeCurrent.A; }
            else return null;

            float followAngle = global::engine.geom.Angles.Snorm(
                strokeCurrent.Angle + (!isAB ? (float)Math.PI : 0f));

            var strokeNext = spNext.GetNextAngle(
                strokeCurrent, followAngle, true, BlockGraph.Accept) ?? strokeCurrent;

            visited.Add(spNext);

            if (spNext == spStart && strokeNext == first) return visited;

            strokeCurrent = strokeNext;
            spCurr = spNext;
        }

        return null;
    }


    private static (int, int, int, int) _boxOf(List<IntPoint> poly)
    {
        int x0 = Int32.MaxValue, x1 = Int32.MinValue, y0 = Int32.MaxValue, y1 = Int32.MinValue;
        foreach (var p in poly)
        {
            x0 = Math.Min(x0, (int)p.X); x1 = Math.Max(x1, (int)p.X);
            y0 = Math.Min(y0, (int)p.Y); y1 = Math.Max(y1, (int)p.Y);
        }

        return (x0, y0, x1, y1);
    }


    private static bool _hit((int, int, int, int) a, (int, int, int, int) b)
        => !(a.Item3 < b.Item1 || a.Item1 > b.Item3 || a.Item4 < b.Item2 || a.Item2 > b.Item4);


    private static double _overlapArea(List<Vector3> building, List<IntPoint> footprint)
    {
        var poly = building
            .Select(v => new IntPoint((int)(v.X * 10f), (int)(v.Z * 10f))).ToList();

        var clipper = new Clipper();
        clipper.AddPath(poly, PolyType.ptSubject, true);
        clipper.AddPath(footprint, PolyType.ptClip, true);

        var solution = new List<List<IntPoint>>();
        clipper.Execute(ClipType.ctIntersection, solution,
            PolyFillType.pftNonZero, PolyFillType.pftNonZero);

        double area = 0.0;
        foreach (var p in solution) area += Math.Abs(Clipper.Area(p)) / 100.0;
        return area;
    }
}
