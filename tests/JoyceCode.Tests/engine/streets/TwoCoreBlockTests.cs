using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ClipperLib;
using engine.streets;
using engine.streets.generation;
using engine.world;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * ⚠️ WP-O1 — THE BLOCK TRACE RUNS OVER THE BLOCK GRAPH'S 2-CORE, AND A FACE WALK STOPS ON
 * THE DIRECTED EDGE IT SET OUT ALONG.
 *
 * Ledger item (o), repair (b), first of three work packages. §7t established that
 * QuarterGenerator stopped its face walk on `spNext == spStart` - a VERTEX test - so a
 * face pinched at its own starting junction by a dead-end spur was truncated and its ring
 * closed by a chord across the block, with the estate and the building laid over whatever
 * that chord ran over. §7t.5 established that merely terminating correctly DELETES those
 * blocks, because the completed face contains a spur and hasNullSection discards it.
 *
 * The repair is to peel first: every junction with fewer than two block arms leaves the
 * block graph, iteratively, so a spur is not a block edge at all. The face then has no
 * pinch, the ring closes AND the block stands, going round the spur.
 *
 * ⚠️ THIS WORK PACKAGE ALONE MADE THE REPORTED SYMPTOM WORSE AND THAT WAS DELIBERATE. A
 * 2-cored block contains its spurs by construction and WP-O1 does not subtract them from
 * the estate, so a building stood over a street on twenty times as many blocks as before -
 * 3247 against 156 flag off, 2986 against 191 flag on. That was measured and asserted here
 * rather than left to be discovered, and it was WP-O2's positive control; WP-O2 (§7v) has
 * since driven it to zero, and the Theory that recorded it is spent and deleted, its text
 * kept where it stood.
 *
 * ⚠️ THE OUTER FACE NEEDED A RULE OF ITS OWN, which nothing in the plan anticipated. It
 * used to be discarded for a reason unrelated to being outside - it ran through some
 * dead-end spur on the city's edge and was refused as hasNullSection. Peel the spurs away
 * and it becomes a perfectly good closed ring of junctions that all have corners. Without
 * BlockGraph.IsInteriorFace the generator would store one "city block" per connected
 * component covering the entire city.
 */
public class TwoCoreBlockTests
{
    private const float ClusterSize = 5000f;


    /*
     * ============================================== the peel =========================
     */

    /**
     * ⚠️ THE PEEL IS ITERATED, and one pass is not enough: dropping a dead-end spur can
     * leave the junction it hung off with a single arm of its own.
     *
     * The fixture is a triangle with a two-stroke tail on it. One pass removes the tip;
     * the junction between tip and triangle then has one arm and has to go too. A
     * single-pass peel leaves it in the core, which is what this fixture is for.
     */
    [Fact]
    public void ThePeelRepeatsUntilNothingIsLeftToPeel()
    {
        var store = new StrokeStore(ClusterSize);

        var a = _pointAt(0f, 0f);
        var b = _pointAt(100f, 0f);
        var c = _pointAt(50f, 90f);
        var mid = _pointAt(0f, -80f);
        var tip = _pointAt(0f, -160f);

        _street(store, a, b);
        _street(store, b, c);
        _street(store, c, a);
        _street(store, a, mid);
        _street(store, mid, tip);

        var core = BlockGraph.TwoCoreOf(store);

        Assert.Contains(a, core);
        Assert.Contains(b, core);
        Assert.Contains(c, core);
        Assert.DoesNotContain(mid, core);
        Assert.DoesNotContain(tip, core);
    }


    /**
     * The property the peel exists to establish, over the cities the game builds: every
     * junction left in the core has at least two arms to other junctions left in it.
     *
     * Asked about arms WITHIN the core rather than about BlockGraph.ArmCountOf, which
     * counts an arm to a peeled junction too - that is the whole difference between the
     * 2-core and "has two block arms", and a peel that stopped one round early would
     * satisfy the weaker statement.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryJunctionOfTheCoreHasTwoArmsInsideIt(string idString, float size)
    {
        foreach (var (label, store) in _cities(idString, size))
        {
            var core = BlockGraph.TwoCoreOf(store);
            var accept = BlockGraph.AcceptWithin(core);

            foreach (var sp in core)
            {
                int n = sp.GetAngleArray().Count(accept);
                Assert.True(n >= 2, $"{idString}@{size} {label}: junction {sp.Id} at "
                                    + $"{sp.Pos} is in the core with {n} core arms");
            }

            /*
             * Not vacuous, and not the whole graph either: a city has a core, and it has
             * junctions outside it.
             */
            if (size >= 500f)
            {
                Assert.True(core.Count > 5);
                Assert.True(core.Count < store.GetStreetPoints().Count);
            }
        }
    }


    /*
     * ============================================== the faces ========================
     */

    /**
     * ⚠️ THE DEFECT, CLOSED. Every block ring is made of streets - 219 of 36327 were not
     * flag off and 274 of 33432 flag on (§7t.2), and every one of them was broken at the
     * wrap-around edge, i.e. at the chord the truncated walk invented.
     *
     * The test is on IDENTITY - the two StreetPoint objects of delims[i].Stroke against
     * delims[i].StreetPoint and delims[i+1].StreetPoint - not on distance, for §7e's
     * reason: two junctions of one city can be metres apart, so a metric test cannot tell
     * a skipped junction from a short street.
     */
    [Theory]
    //                       quarters
    [InlineData(false, 40891)]
    [InlineData(true, 37609)]
    public void EveryBlockRingIsClosedByStreets(bool gradeSeparation, int expectedQuarters)
    {
        int nQuarters = 0, nBroken = 0;

        foreach (var city in BlockRingClosureTests.World(gradeSeparation))
        {
            foreach (var q in city.Quarters.GetQuarters())
            {
                ++nQuarters;
                if (BlockRingClosureTests.RingIsBroken(q)) ++nBroken;
            }
        }

        Assert.Equal(expectedQuarters, nQuarters);
        Assert.Equal(0, nBroken);
    }


    /**
     * ⚠️ THE FACE DECOMPOSITION IS COMPLETE, AND EXACTLY ONE FACE PER COMPONENT IS THROWN
     * AWAY.
     *
     * Euler's formula on the 2-core: a connected planar graph with V junctions and E
     * edges has E - V + 2 faces, one of which is the outside. So the number of blocks a
     * component contributes is E - V + 1, and over a city with C components it is
     * E - V + C. This is an ARITHMETIC statement about the graph and knows nothing about
     * how QuarterGenerator walks it, which is what makes it worth asserting: it says at
     * once that no face is missed, that no face is discarded for hasNullSection or a dead
     * end, and that the outer face - and only the outer face - is refused.
     *
     * It is also what the whole 33 % of §7t.4 was: a third of the block graph's directed
     * edges were in faces refused for hasNullSection, and none is now.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void TheBlocksAreEveryFaceOfTheCoreButTheOutsideOfEachComponent(
        string idString, float size)
    {
        foreach (var (label, store) in _cities(idString, size))
        {
            var cluster = StreetHarness.MakeCluster(idString, size);
            var quarters = StreetHarness.GenerateQuarters(cluster, store, idString);

            var core = BlockGraph.TwoCoreOf(store);
            var accept = BlockGraph.AcceptWithin(core);

            int v = core.Count;
            int e = store.GetStrokes().Count(accept);
            int c = _componentsOf(core, accept);

            Assert.Equal(e - v + c, quarters.GetQuarters().Count);

            /*
             * The control: the graph has faces at all, so "0 == 0" cannot satisfy this.
             */
            if (size >= 500f) Assert.True(quarters.GetQuarters().Count > 0, label);
        }
    }


    /**
     * ⚠️ THE OUTER FACE IS NOT A CITY BLOCK, on the smallest thing that has one.
     *
     * A single square of four streets is one city block, not two. The walk produces two
     * faces - the inside of the square and the outside of the whole component - and only
     * the winding tells them apart, because after the peel both are closed rings of
     * junctions that all have corners. Before WP-O1 the outer face was refused for a
     * reason that had nothing to do with being outside.
     */
    [Fact]
    public void ASquareOfFourStreetsIsOneCityBlockAndNotTwo()
    {
        var store = new StrokeStore(ClusterSize);

        var a = _pointAt(0f, 0f);
        var b = _pointAt(120f, 0f);
        var c = _pointAt(120f, 110f);
        var d = _pointAt(0f, 110f);

        _street(store, a, b);
        _street(store, b, c);
        _street(store, c, d);
        _street(store, d, a);

        var quarters = StreetHarness.GenerateQuarters(
            StreetHarness.MakeCluster("square", ClusterSize), store, "square");

        Assert.Single(quarters.GetQuarters());

        /*
         * ...and it is the INSIDE: its corners lie within the square, not outside it.
         */
        foreach (var delim in quarters.GetQuarters()[0].GetDelims())
        {
            Assert.InRange(delim.StartPoint.X, 0f, 120f);
            Assert.InRange(delim.StartPoint.Y, 0f, 110f);
        }
    }


    /**
     * The same statement about the winding, driven directly, so that "the outer face is
     * refused" is a rule with a sign and not an accident of which face happened to be
     * traced first.
     */
    [Fact]
    public void AnInteriorFaceWindsOneWayAndTheOutsideTheOther()
    {
        var square = new List<Vector2>
        {
            new(0f, 0f), new(0f, 110f), new(120f, 110f), new(120f, 0f)
        };

        Assert.True(BlockGraph.IsInteriorFace(square));

        square.Reverse();
        Assert.False(BlockGraph.IsInteriorFace(square));

        /*
         * A ring with fewer than three corners is no face at all.
         */
        Assert.False(BlockGraph.IsInteriorFace(new List<Vector2> { new(0f, 0f), new(1f, 1f) }));
    }


    /**
     * ⚠️ AND THE SIGN SURVIVES BEING 30 KM FROM THE ORIGIN, which is why this is not
     * SidewalkRing.SignedArea2Of.
     *
     * That expression accumulates absolute coordinates in float, which is right for a
     * pavement ring a few tens of metres across. A face ring may run round a 3800 m city,
     * where the products reach 4e6 and a float step is a quarter of a square metre - and
     * the shipped world puts seventy cities at a median 36 km out. A 100 square metre
     * block is then noise. This one translates to the ring's own first corner and
     * accumulates in double.
     */
    [Fact]
    public void TheWindingOfASmallBlockSurvivesBeingFarFromTheOrigin()
    {
        var origin = new Vector2(1_000_000f, 1_000_000f);
        var block = new List<Vector2>
        {
            origin, origin + new Vector2(0f, 10f),
            origin + new Vector2(10f, 10f), origin + new Vector2(10f, 0f)
        };

        Assert.True(BlockGraph.IsInteriorFace(block));

        block.Reverse();
        Assert.False(BlockGraph.IsInteriorFace(block));
    }


    /**
     * ⚠️ A FACE MAY STILL PASS THROUGH ONE JUNCTION TWICE, WHICH IS WHY THE TERMINATION
     * IS ON THE DIRECTED EDGE AND NOT ON THE JUNCTION.
     *
     * The peel removes the dead-end spur that pinched a face in §7t, but it does not
     * remove a BRIDGE: an edge whose two ends both lie on cycles is kept by a 2-core, and
     * the face beside it runs along it twice. The fixture is a small square inside a big
     * one, joined by a single street; the face between them is an annulus with a slit in
     * it, and it passes through both ends of that street twice.
     *
     * With the old `spNext == spStart` test, a walk that starts at one of those junctions
     * stops half way round and closes its ring with a chord - which is exactly §7t's
     * defect, on a shape the peel does not remove. Over the shipped world 31 stored
     * blocks flag off and 10 flag on have such a ring.
     */
    [Fact]
    public void AFaceThatPassesThroughOneJunctionTwiceIsStillWalkedWhole()
    {
        var store = new StrokeStore(ClusterSize);

        var o0 = _pointAt(-300f, -300f);
        var o1 = _pointAt(300f, -300f);
        var o2 = _pointAt(300f, 300f);
        var o3 = _pointAt(-300f, 300f);

        var i0 = _pointAt(-80f, -80f);
        var i1 = _pointAt(80f, -80f);
        var i2 = _pointAt(80f, 80f);
        var i3 = _pointAt(-80f, 80f);

        _street(store, o0, o1);
        _street(store, o1, o2);
        _street(store, o2, o3);
        _street(store, o3, o0);

        _street(store, i0, i1);
        _street(store, i1, i2);
        _street(store, i2, i3);
        _street(store, i3, i0);

        /* the bridge, from the outer ring's corner to the inner ring's */
        _street(store, o0, i0);

        var core = BlockGraph.TwoCoreOf(store);
        Assert.Equal(8, core.Count);

        var quarters = StreetHarness.GenerateQuarters(
            StreetHarness.MakeCluster("annulus", ClusterSize), store, "annulus");

        /*
         * Two blocks: the inside of the inner square, and the ring between the two - the
         * one that runs along the bridge twice. The outside of the whole thing is refused.
         */
        Assert.Equal(2, quarters.GetQuarters().Count);

        var annulus = quarters.GetQuarters()
            .OrderByDescending(q => q.GetDelims().Count).First();

        /*
         * It is whole: it names every one of the nine streets, and both ends of the
         * bridge twice. A walk truncated at its starting junction cannot do that.
         */
        Assert.Equal(10, annulus.GetDelims().Count);
        Assert.Equal(9, annulus.GetDelims().Select(d => d.Stroke.Sid).Distinct().Count());
        Assert.False(BlockRingClosureTests.RingIsBroken(annulus));

        var ids = annulus.GetDelims().Select(d => d.StreetPoint.Id).ToList();
        Assert.Equal(8, ids.Distinct().Count());
    }


    /*
     * ============================================== what it costs ====================
     */

    /*
     * ⚠️ WP-O2's POSITIVE CONTROL LIVED HERE AND HAS BEEN SPENT.
     *
     * TheSpurIsInsideTheBlockAndTheBuildingIsStillOnIt recorded what shipping WP-O1 on its
     * own cost, so that the estate exclusion had to move it deliberately rather than
     * discover it:
     *
     *     "A 2-cored block contains its spurs by construction, and nothing subtracts them
     *      from the estate yet. So the reported symptom - a street trunk running into a
     *      building - happens on far more blocks than the 146 / 190 §7t.3 counted: it is
     *      the whole population of spurs that now stand inside a block rather than in
     *      ground with no block on it."
     *
     *      spurs                  9840 flag off, 8100 flag on
     *      inside a block         6422             5498
     *      stub under a building  146 -> 4328      190 -> 3696
     *      buildings             27384            24858
     *      over a road            156 -> 3247      191 -> 2986
     *
     * WP-O2 (§7v) drove the two arrows to ZERO, which is the report closing, and it is
     * asserted in SpurEstateTests.NoBuildingStandsOnASpurOrAStructure - together with the
     * spur and building counts above as its controls, unmoved, so that "no building on a
     * road" cannot be satisfied by the population going away. The Theory is deleted rather
     * than left asserting zeros beside it: it walks all seventy cities against every
     * carriageway of each and took 2 m 20 s, and there is no second question it answers.
     */



    /**
     * ⚠️ WHERE THE 33 % WENT. §7t.4 counted 101113 of 304150 directed block edges flag
     * off - 33.2 % - and 64790 of 228348 flag on inside faces discarded for
     * hasNullSection: a third of the street graph produced no block, no estate, no
     * building and no pavement.
     *
     * After the peel nothing is discarded for hasNullSection at all, and the remainder
     * splits into two named parts: the arms the peel took out of the block graph, which
     * are the spur trees themselves and are not block edges any more, and the outer face
     * of each component, which is the outside of the city.
     *
     *      in a stored block   203037 (66.8 %) -> 260283 (85.6 %) flag off
     *                          163558 (71.6 %) -> 198275 (86.8 %) flag on
     *      discarded faces     101113 (33.2 %) ->  15271  (5.0 %) flag off
     *                           64790 (28.4 %) ->  10827  (4.7 %) flag on
     */
    [Theory]
    //                   directed  in stored blocks  on a peeled arm
    [InlineData(false, 304150, 260283, 28596)]
    [InlineData(true, 228348, 198275, 19246)]
    public void MostOfTheBlockGraphIsInABlockNow(
        bool gradeSeparation, int directed, int stored, int peeled)
    {
        int nDirected = 0, nStored = 0, nPeeled = 0;

        foreach (var city in BlockRingClosureTests.World(gradeSeparation))
        {
            var core = BlockGraph.TwoCoreOf(city.Store);

            nDirected += 2 * city.Store.GetStrokes().Count(BlockGraph.IsBlockEdge);
            nPeeled += 2 * city.Store.GetStrokes().Count(
                s => BlockGraph.IsBlockEdge(s)
                     && (!core.Contains(s.A) || !core.Contains(s.B)));

            foreach (var q in city.Quarters.GetQuarters()) nStored += q.GetDelims().Count;
        }

        Assert.Equal(directed, nDirected);
        Assert.Equal(stored, nStored);
        Assert.Equal(peeled, nPeeled);

        /*
         * ...and what is left over is the outer face of each of the seventy components
         * and nothing else, which is the arithmetic the Euler gate above asserts per
         * seed.
         */
        Assert.True(nDirected - nStored - nPeeled > 0);
    }


    /**
     * No stored block corners on a junction the peel removed, and no stored block corners
     * on one with fewer than two block arms - so hasNullSection, the rule that used to
     * discard a third of all faces, now fires on nothing.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoBlockCornersOnAJunctionThePeelRemoved(string idString, float size)
    {
        foreach (var (label, store) in _cities(idString, size))
        {
            var cluster = StreetHarness.MakeCluster(idString, size);
            var quarters = StreetHarness.GenerateQuarters(cluster, store, idString);
            var core = BlockGraph.TwoCoreOf(store);

            foreach (var q in quarters.GetQuarters())
            {
                /*
                 * The trace's own record that neither backstop fired on this face.
                 */
                Assert.DoesNotContain("hasDeadEnd", q.GetDebugString());
                Assert.DoesNotContain("hasNullSection", q.GetDebugString());

                foreach (var d in q.GetDelims())
                {
                    Assert.Contains(d.StreetPoint, core);
                    Assert.True(BlockGraph.ArmCountOf(d.StreetPoint) >= 2,
                        $"{idString}@{size} {label}: junction {d.StreetPoint.Id}");
                }
            }
        }
    }


    /**
     * The block census of the pinned seeds, per seed, because no baseline file records
     * one and the world-wide totals above cannot say which city moved.
     *
     * Old (WP-B5 to WP-O1) in the left column of each pair.
     */
    public static IEnumerable<object[]> PinnedBlocks => new List<object[]>
    {
        //                                  flag off      flag on (flat)
        //                                 was -> is       was -> is
        new object[] { "seed000", 500f, 3, 3, 2, 4 },
        new object[] { "seed011", 500f, 2, 2, 3, 3 },
        new object[] { "Yelukhdidru", 400f, 0, 0, 0, 0 },
        new object[] { "Yelukhdidru", 800f, 10, 11, 7, 8 },
        new object[] { "seed000", 1500f, 82, 94, 61, 73 },
        new object[] { "seed017", 2400f, 221, 250, 165, 189 },
        new object[] { "Yelukhdidru", 3000f, 445, 497, 282, 327 },
        new object[] { "seed008", 500f, 4, 4, 5, 5 },
    };


    [Theory]
    [MemberData(nameof(PinnedBlocks))]
    public void ThePinnedSeedsGetTheseBlocks(
        string idString, float size, int wasOff, int isOff, int wasOn, int isOn)
    {
        var cluster = StreetHarness.MakeCluster(idString, size);

        foreach (var (gradeSeparation, expected) in new[] { (false, isOff), (true, isOn) })
        {
            var store = gradeSeparation
                ? StreetHarness.GenerateHeavyFirst(idString, size)
                : StreetHarness.Generate(idString, size);
            var quarters = StreetHarness.GenerateQuarters(cluster, store, idString);

            Assert.Equal(expected, quarters.GetQuarters().Count);
            Assert.All(quarters.GetQuarters(),
                q => Assert.False(BlockRingClosureTests.RingIsBroken(q)));
        }

        /*
         * The old counts are carried as parameters rather than only in a comment, so that
         * a reader of a failure sees what this used to be. Never fewer blocks than before
         * - the peel only ever recovers faces that were being thrown away.
         */
        Assert.True(isOff >= wasOff);
        Assert.True(isOn >= wasOn);
    }


    /*
     * ============================================== plumbing =========================
     */

    public static IEnumerable<object[]> Seeds => new List<object[]>
    {
        new object[] { "seed000", 500f },
        new object[] { "seed011", 500f },
        new object[] { "Yelukhdidru", 800f },
        new object[] { "seed000", 1500f },
        new object[] { "seed017", 2400f },
        new object[] { "Yelukhdidru", 3000f },
    };


    private static IEnumerable<(string Label, StrokeStore Store)> _cities(
        string idString, float size)
    {
        yield return ("flag off", StreetHarness.Generate(idString, size));
        yield return ("flag on", StreetHarness.GenerateHeavyFirst(idString, size));
    }


    private static int _componentsOf(HashSet<StreetPoint> core, Func<Stroke, bool> accept)
    {
        var adjacency = new Dictionary<StreetPoint, List<StreetPoint>>();
        foreach (var sp in core) adjacency[sp] = new List<StreetPoint>();

        foreach (var sp in core)
        foreach (var s in sp.GetAngleArray())
        {
            if (!accept(s)) continue;

            adjacency[sp].Add(s.A == sp ? s.B : s.A);
        }

        int n = 0;
        var seen = new HashSet<StreetPoint>();
        foreach (var sp in core)
        {
            if (!seen.Add(sp)) continue;

            ++n;
            var stack = new Stack<StreetPoint>();
            stack.Push(sp);
            while (stack.Count > 0)
            {
                foreach (var next in adjacency[stack.Pop()])
                {
                    if (seen.Add(next)) stack.Push(next);
                }
            }
        }

        return n;
    }


    private static StreetPoint _pointAt(float x, float y)
    {
        var sp = new StreetPoint { ClusterId = 0, Level = 0 };
        sp.SetPos(x, y);
        return sp;
    }


    private static Stroke _street(StrokeStore store, StreetPoint a, StreetPoint b)
    {
        var s = new Stroke
        {
            ClusterId = 0, IsPrimary = false, Weight = 1f, Kind = StrokeKind.Street, Level = 0
        };
        s.A = a;
        s.B = b;
        store.AddStroke(s);
        return s;
    }

}
