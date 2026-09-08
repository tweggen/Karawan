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
 * ⚠️ THE SHIPPED WORLD, WITH ITS BLOCKS TRACED - and the record of the defect §7t found
 * in it.
 *
 * A CITY BLOCK'S OUTLINE WAS NOT ALWAYS MADE OF STREETS, AND A BUILDING THEN STOOD ON ONE.
 * Reported from play - "I do see a street trunk running off into a building" - and
 * measured here rather than fixed, because the fix was a design decision and the size of
 * it was what the decision needed.
 *
 * QuarterGenerator.Generate() walked a face of the street graph and stopped when it
 * arrived back at the junction it started from:
 *
 *     if (spNext == spStart) break;
 *
 * That is a VERTEX test. A face walk has to stop on the (junction, outgoing stroke) PAIR
 * it started from, because a face may pass through one junction twice - which is exactly
 * what a face does when a dead-end spur cuts a slit into a block. When the doubly-visited
 * junction happened to be the one the walk started at, the walk stopped half way round and
 * the ring was closed by a straight chord from the last delimiter back to the first. That
 * chord is not a street: median 75 m long and up to 147 m, and it can cross roads. The
 * estate was that outline, _createBuildings inset it, and the building went across
 * whatever the chord ran over.
 *
 * ⚠️ FIXED BY WP-O1 (§7u), AND THE GATES MOVED RATHER THAN BEING RE-BASELINED. The block
 * graph is peeled to its 2-core before a face is walked, so a dead-end spur is not a block
 * edge, the face has no pinch, the ring closes and the block stands. What this file
 * recorded, and where each number now lives:
 *
 *      ABlockRingIsClosedByStreetsExceptWhenItIsNot
 *          quarters 36327 / 33432, rings that do not close 219 / 274, every one of them
 *          broken at the WRAP-AROUND edge and nowhere else (219/219, 274/274) - which is
 *          what separated this from §7e, whose delimiters were wrong at every edge.
 *          -> TwoCoreBlockTests.EveryBlockRingIsClosedByStreets, 40891 / 37609 and zero.
 *
 *      ABuildingOnABrokenRingStandsOnAStreet
 *          146 of 146 and 190 of 191 broken-ring buildings over a Street carriageway,
 *          against 10 and 1 on blocks whose ring closed - the control that made it a
 *          correspondence rather than a correlation.
 *          -> TwoCoreBlockTests.TheSpurIsInsideTheBlockAndTheBuildingIsStillOnIt, where
 *          it is 3247 / 2986 and is WP-O2's positive control.
 *
 *      TheStreetThatRunsIntoTheBuildingIsADeadEndSpur
 *          9840 / 8100 spurs, 0 / 2 inside a block whose ring closed, 222 / 272 inside a
 *          broken one, 146 / 190 with the stub under a building. The 0-and-2 is what said
 *          the truncation was essentially the only mechanism.
 *          -> the same gate, where every spur inside a block is now inside a closed one.
 *
 *      AThirdOfTheBlockEdgesAreInFacesNobodyBuildsOn
 *          304150 / 228348 directed block edges, 203037 / 163558 in a stored quarter, so
 *          33.2 % / 28.4 % in faces discarded for hasNullSection.
 *          -> TwoCoreBlockTests.MostOfTheBlockGraphIsInABlockNow, 85.6 % / 86.8 % in a
 *          block and 5.0 % / 4.7 % discarded, all of it the outside of the city.
 *
 *      CompletingTheWalkWouldDiscardTheBlockAltogether
 *          the counterfactual that made this a decision: walked with the termination it
 *          should have had, 219 / 274 of those faces completed, 219 / 274 revisited a
 *          junction and 219 / 272 contained one with fewer than two block arms - i.e. a
 *          face hasNullSection DISCARDS. Terminating correctly would have deleted the
 *          blocks rather than repairing them, which is why repair (b) peels first.
 *          -> no successor: with the peel there is no such face to walk.
 *
 *      ThePinnedSeedsCarryThisMany
 *          quarters/broken per seed, 3/0, 2/0, 0/0, 10/0, 82/0, 221/2, 445/3 flag off and
 *          2/0, 3/0, 0/0, 7/0, 61/1, 165/2, 282/4, 5/1 flag on.
 *          -> TwoCoreBlockTests.ThePinnedSeedsGetTheseBlocks, which carries the old counts
 *          beside the new ones.
 *
 * What stays here is the world itself - seventy cities with their blocks traced, shared
 * with SpurBlocks and BlockSpurEstateTests - and the one measurement that is about the
 * world rather than about the defect.
 */
public class BlockRingClosureTests
{
    private static readonly object _lo = new();

    internal sealed class City
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
     * seeds - which carried only 10 of §7t's broken rings between them.
     */
    internal static List<City> World(bool gradeSeparation)
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
     * ============================================== plumbing =========================
     */

    /**
     * Whether a plan position is inside a block's outline.
     */
    private static bool _insidePlan(List<Vector2> ring, in Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            if ((ring[i].Y > p.Y) != (ring[j].Y > p.Y)
                && p.X < (ring[j].X - ring[i].X) * (p.Y - ring[i].Y) / (ring[j].Y - ring[i].Y)
                         + ring[i].X)
            {
                inside = !inside;
            }
        }

        return inside;
    }


    /**
     * The two measurements above, for the file that measures what a 2-cored block would
     * do with the same spurs (BlockSpurEstateTests). Exposed rather than copied, so that
     * "this ring does not close" and "this junction is inside that ring" mean the same
     * thing in both files - both are identity/geometry rules that a second copy could
     * quietly weaken.
     */
    internal static bool RingIsBroken(Quarter q) => _firstOpenEdge(q) >= 0;


    internal static bool InsidePlan(List<Vector2> ring, in Vector2 p)
        => _insidePlan(ring, p);


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
