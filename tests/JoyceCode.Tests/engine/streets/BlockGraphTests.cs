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
 * WP-B5 — city blocks over a network that is no longer planar.
 *
 * A city block is a face of a PLANAR graph. The moment WP-B3b placed a structure the
 * street network stopped being one: a deck and the road under it cross at a place where
 * they do not meet. §3c settled what to do about it - the deck and its ramps are ABSENT
 * from the block graph, the blocks either side of a lifted corridor merge into one, and
 * no pseudo-vertex is needed because what is left is planar again by itself.
 *
 * Three kinds of test here, and each covers something the other two cannot:
 *
 * - the flag-OFF census, which is the control this whole work package rests on. Nothing
 *   about a city with no structure in it may change, and the fingerprint baselines say
 *   nothing about blocks;
 * - whole generated cities with the flag on, for the three properties that FAILED on the
 *   fixture that settled §3c and for what the merge costs;
 * - fixtures, because two of the rules here are refused by no city the ruleset builds
 *   (§9's "a rule can be invisible to unlimited real data", twice more).
 */
public class BlockGraphTests
{
    public static IEnumerable<object[]> Seeds => new List<object[]>
    {
        new object[] { "seed000", 500f },
        new object[] { "seed011", 500f },
        new object[] { "Yelukhdidru", 400f },
        new object[] { "Yelukhdidru", 800f },
        new object[] { "seed000", 1500f },
        new object[] { "seed017", 2400f },
        new object[] { "Yelukhdidru", 3000f },
    };


    /**
     * ⚠️ THE CONTROL. A city with joyce.EnableGradeSeparation off traces exactly the
     * blocks it always did.
     *
     * Recorded per seed rather than compared against a re-run, because the point is that
     * these numbers are the ones the game shipped with: the block trace stopped asking
     * the section MAP for a corner and started computing it, and the two agree only if
     * "no section point here" means the same thing both ways.
     *
     * street-geometry.json pins block geometry for five of these cities and nothing pins
     * the other two, nor the estates, buildings and shops of any of them.
     */
    public static IEnumerable<object[]> FlagOffCensus => new List<object[]>
    {
        //                          quarters estates buildings shops
        new object[] { "seed000",     500f,     3,   3,   3,    69 },
        new object[] { "seed011",     500f,     2,   2,   2,    40 },
        new object[] { "Yelukhdidru", 400f,     0,   0,   0,     0 },
        new object[] { "Yelukhdidru", 800f,    10,  10,   3,   113 },
        new object[] { "seed000",     1500f,   82,  82,  81,  1336 },
        new object[] { "seed017",     2400f,  221, 221, 134,  3015 },
        new object[] { "Yelukhdidru", 3000f,  445, 445, 148,  2904 },
    };


    /**
     * ⚠️ WHAT THE MERGE COSTS, which §3c promised to quantify and nobody had.
     *
     * The flag-ON city, which is a different city from the flag-off one before a single
     * structure is placed (§13.2: heavy-first ordering changes what the city is made
     * of). Both grounds, because they carry very different numbers of structures - 134
     * over the seven flat cities against 20 on the shipped terrain.
     *
     * Read against the WP-B4 state of the same cities, where blocks were traced straight
     * through ramps and decks:
     *
     *      seed              flat  before -> after      terrain  before -> after
     *      seed000@500          2 ->   2                      3 ->   3
     *      seed011@500          3 ->   3                      3 ->   3
     *      Yelukhdidru@400      0 ->   0                      0 ->   0
     *      Yelukhdidru@800      8 ->   7                     11 ->  11
     *      seed000@1500        54 ->  61                     85 ->  85
     *      seed017@2400       152 -> 165                    232 -> 233
     *      Yelukhdidru@3000   264 -> 282                    378 -> 379
     *
     * ⚠️ AND IT GOES UP, not down, which is the opposite of what "blocks merge" sounds
     * like. Both effects are real and the second is the larger: a lift merges the blocks
     * either side of its corridor, but tracing THROUGH a structure used to produce faces
     * that were then discarded whole for hasNullSection, and those blocks come back.
     */
    public static IEnumerable<object[]> FlagOnCensus => new List<object[]>
    {
        //                         flat: q/e/b/shops        terrain: q/e/b/shops
        //
        // ⚠️ Yelukhdidru@800's terrain shop count moved 389 -> 339 in WP-B6, and it is
        // the only census number in this table that did. One of its estates is pinched
        // enough that the pavement inset splits it in two, with no structure anywhere
        // near it; _createBuildings used to concatenate both pieces into one
        // self-crossing ring and hang shop fronts off the whole perimeter. See
        // BlockGraph.LargestOf. Nothing in the FLAG-OFF census moves - 0 of 763 estates
        // there split at all.
        new object[] { "seed000",     500f,   2,   2,  2,    0,     3,   3,   3,   51 },
        new object[] { "seed011",     500f,   3,   3,  3,   83,     3,   3,   3,   83 },
        new object[] { "Yelukhdidru", 400f,   0,   0,  0,    0,     0,   0,   0,    0 },
        new object[] { "Yelukhdidru", 800f,   7,   7,  6,  251,    11,  11,   7,  339 },
        new object[] { "seed000",     1500f, 61,  61, 57,  914,    85,  85,  81, 1525 },
        new object[] { "seed017",     2400f,165, 165, 80, 1050,   233, 233, 118, 2076 },
        new object[] { "Yelukhdidru", 3000f,282, 282, 81, 1273,   379, 379, 111, 2023 },
    };


    // ------------------------------------------------------------------ the block graph

    /**
     * ⚠️ THE ONE PREDICATE, and it names its three kinds rather than saying "not a
     * Street".
     *
     * A ConnectorBridge is an ordinary ground road that exists in every shipped flat
     * city (§0.7); a rule phrased against non-Street would take it out of the block
     * graph and change the default city.
     */
    [Fact]
    public void AStructureIsNotABlockEdgeAndAConnectorBridgeIs()
    {
        Assert.False(BlockGraph.IsBlockEdge(new Stroke { Kind = StrokeKind.Ramp }));
        Assert.False(BlockGraph.IsBlockEdge(new Stroke { Kind = StrokeKind.Bridge }));
        Assert.False(BlockGraph.IsBlockEdge(new Stroke { Kind = StrokeKind.Tunnel }));

        Assert.True(BlockGraph.IsBlockEdge(new Stroke { Kind = StrokeKind.Street }));
        Assert.True(BlockGraph.IsBlockEdge(new Stroke { Kind = StrokeKind.ConnectorBridge }));

        /*
         * The delegate the block trace actually hands to GetNextAngle has to be that
         * predicate and not a lookalike of it.
         */
        foreach (StrokeKind kind in Enum.GetValues<StrokeKind>())
        {
            var s = new Stroke { Kind = kind };
            Assert.Equal(BlockGraph.IsBlockEdge(s), BlockGraph.Accept(s));
        }
    }


    /**
     * ⚠️ THE POSITIVE CONTROL FOR THE FILTER, on identity rather than on a count.
     *
     * At a ramp's foot the unfiltered walk leaves along the ramp - which is exactly how
     * the §3c fixture's five clean quarters became two - and the filtered one leaves
     * along the ordinary street on the far side of it. The two answers are different
     * strokes, so "the filter is wired" cannot be satisfied by a walk that would have
     * taken the same arm anyway.
     */
    [Fact]
    public void TheBlockWalkRefusesToLeaveAJunctionAlongARamp()
    {
        var f = _footFixture();

        /*
         * Arriving at the foot along the west street. Its angle at the foot, taken the
         * way QuarterGenerator takes it.
         */
        float followAngle = global::engine.geom.Angles.Snorm(f.West.Angle);

        var unfiltered = f.Foot.GetNextAngle(f.West, followAngle, true, null);
        var filtered = f.Foot.GetNextAngle(f.West, followAngle, true, BlockGraph.Accept);

        Assert.Same(f.Ramp, unfiltered);
        Assert.Same(f.East, filtered);
    }


    /**
     * ⚠️ AND THE CORNER THE BLOCK TAKES THERE IS NOT IN THE SECTION MAP AT ALL.
     *
     * The section array is the junction CAP, and a ramp leaving a foot has a carriageway
     * that is part of it - so the two ordinary arms the block turns between are not
     * adjacent there and GetSectionPointByStroke misses. Had the trace kept asking it,
     * every block at every foot would have been discarded silently as hasNullSection,
     * which is a whole-city failure that looks exactly like "there were fewer blocks".
     */
    [Fact]
    public void ARampBetweenTwoArmsHidesTheirCornerFromTheSectionMap()
    {
        var f = _footFixture();

        Assert.Null(f.Foot.GetSectionPointByStroke(f.East, f.West));

        Vector2 corner = f.Foot.SectionPointBetween(f.West, f.East);
        Assert.True(Single.IsFinite(corner.X) && Single.IsFinite(corner.Y));

        /*
         * Equality with the mitre of the two arms the block turns between, from
         * SectionMitre itself. A containment or finiteness test cannot tell that from a
         * guess (§7p).
         */
        Vector2 expected = f.Foot.Pos + SectionMitre.OffsetOf(
            -f.West.Unit, f.East.Unit,
            f.West.StreetWidth() / 2f, f.East.StreetWidth() / 2f,
            SectionMitre.MitreLimit, out _);
        Assert.Equal(expected, corner);

        /*
         * The control: with the ramp gone the two ARE adjacent, and then the map has the
         * very same corner. So the miss above is the ramp's doing and nothing else.
         */
        f.Foot.RemoveStartingStroke(f.Ramp);
        Assert.Equal(corner, f.Foot.GetSectionPointByStroke(f.East, f.West));
    }


    /**
     * SectionPointBetween is where the section array comes from, so the two cannot come
     * to disagree. Asserted as exact equality over whole generated cities, on every
     * adjacent pair of every junction.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void SectionPointBetweenIsExactlyWhatTheSectionArrayHolds(string idString, float size)
    {
        var store = StreetHarness.Generate(idString, size);
        int nPairs = 0;

        foreach (var sp in store.GetStreetPoints())
        {
            var arms = sp.GetAngleArray();
            if (arms.Count < 2) continue;

            for (int i = 0; i < arms.Count; ++i)
            {
                Stroke prev = arms[i];
                Stroke curr = arms[(i + 1) % arms.Count];

                var mapped = sp.GetSectionPointByStroke(curr, prev);
                Assert.NotNull(mapped);
                Assert.Equal(mapped.Value, sp.SectionPointBetween(prev, curr));
                ++nPairs;
            }
        }

        if (size > 400f)
        {
            Assert.True(nPairs > 20, $"{idString}@{size} exercised only {nPairs} pairs");
        }
    }


    // -------------------------------------------------- the three properties, over cities

    private sealed class Census
    {
        internal QuarterStore Quarters;
        internal StrokeStore Store;
        internal int NQuarters, NEstates, NBuildings, NShops;
    }


    private static Census _censusOf(string idString, float size, StrokeStore store)
    {
        var cluster = StreetHarness.MakeCluster(idString, size);
        var quarters = StreetHarness.GenerateQuarters(cluster, store, idString);

        var c = new Census { Quarters = quarters, Store = store };
        foreach (var q in quarters.GetQuarters())
        {
            ++c.NQuarters;
            foreach (var e in q.GetEstates())
            {
                ++c.NEstates;
                foreach (var b in e.GetBuildings())
                {
                    ++c.NBuildings;
                    c.NShops += b.GetShopFronts().Count;
                }
            }
        }

        return c;
    }


    private static StrokeStore _onTerrain(string idString, float size)
    {
        var cluster = StreetHarness.MakeCluster(idString, size);
        var (store, _) = StreetHarness.GenerateHeavyFirstReporting(
            idString, size,
            sp => ShippedTerrain.HeightAt(
                cluster.Pos.X + sp.Pos.X, cluster.Pos.Z + sp.Pos.Y));
        return store;
    }


    private static IEnumerable<(string Label, StrokeStore Store)> _flagOnCities(
        string idString, float size)
    {
        yield return ("flat", StreetHarness.GenerateHeavyFirst(idString, size));
        yield return ("terrain", _onTerrain(idString, size));
    }


    [Theory]
    [MemberData(nameof(FlagOffCensus))]
    public void TheFlagOffBlockCensusIsUnchanged(
        string idString, float size, int quarters, int estates, int buildings, int shops)
    {
        var c = _censusOf(idString, size, StreetHarness.Generate(idString, size));

        Assert.Equal(quarters, c.NQuarters);
        Assert.Equal(estates, c.NEstates);
        Assert.Equal(buildings, c.NBuildings);
        Assert.Equal(shops, c.NShops);

        /*
         * And it contains no structure at all, which is what makes it the control.
         */
        Assert.Empty(c.Store.GetStrokes().Where(s => StrokeKinds.IsStructure(s.Kind)));
    }


    [Theory]
    [MemberData(nameof(FlagOnCensus))]
    public void TheFlagOnBlockCensusIsRecorded(
        string idString, float size,
        int flatQuarters, int flatEstates, int flatBuildings, int flatShops,
        int terrainQuarters, int terrainEstates, int terrainBuildings, int terrainShops)
    {
        var flat = _censusOf(idString, size, StreetHarness.GenerateHeavyFirst(idString, size));
        Assert.Equal(flatQuarters, flat.NQuarters);
        Assert.Equal(flatEstates, flat.NEstates);
        Assert.Equal(flatBuildings, flat.NBuildings);
        Assert.Equal(flatShops, flat.NShops);

        var terrain = _censusOf(idString, size, _onTerrain(idString, size));
        Assert.Equal(terrainQuarters, terrain.NQuarters);
        Assert.Equal(terrainEstates, terrain.NEstates);
        Assert.Equal(terrainBuildings, terrain.NBuildings);
        Assert.Equal(terrainShops, terrain.NShops);
    }


    /**
     * PROPERTY ONE. No block runs along a structure, and no block corner stands on a
     * junction that is not on the ground.
     *
     * This is the half of §3c's fixture failure that was visible: six of one face's
     * delimiters were on Ramp or Bridge strokes and four of those junctions were at
     * Level = 1.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoBlockRunsAlongAStructure(string idString, float size)
    {
        foreach (var (label, store) in _flagOnCities(idString, size))
        {
            var c = _censusOf(idString, size, store);

            foreach (var q in c.Quarters.GetQuarters())
            {
                foreach (var d in q.GetDelims())
                {
                    Assert.True(BlockGraph.IsBlockEdge(d.Stroke),
                        $"{idString}@{size} {label}: a block runs along a {d.Stroke.Kind}");
                    Assert.Equal(0, d.StreetPoint.Level);
                }
            }
        }
    }


    /**
     * PROPERTY TWO. No block swallows a junction that is on a cycle of the block graph.
     *
     * ⚠️ NOT "no block contains any junction", which is not true and never was. Three
     * kinds of junction legitimately stand inside a block:
     *
     * - a DEAD-END SPUR, whose own face was discarded whole for hasNullSection. Measured
     *   on the flag-off cities: four such junctions in three blocks of Yelukhdidru@3000
     *   and two in two blocks of seed017@2400, all of them on one- or two-stroke stubs.
     *   Pre-existing, and nothing here touches it;
     * - a DECK END, which carries no block edge at all and is inside the merged block by
     *   construction - that is what a viaduct standing in a block means;
     * - the crossing junction of a lifted corridor keeps its remaining arms and stays a
     *   corner, so it is NOT one of these.
     *
     * What must never happen is a junction on a CYCLE being swallowed, because that is a
     * road the face jumped over instead of turning at - which is precisely §3c's
     * sixteen-corner face traversing the ground road on both sides. The block graph's
     * 2-core is exactly the set of junctions on a cycle: peel degree-one junctions until
     * none is left.
     *
     * Zero on all seven seeds, flag off and flag on, flat and on the shipped terrain.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoBlockSwallowsAJunctionThatIsOnACycle(string idString, float size)
    {
        var cities = new List<(string, StrokeStore)>
        {
            ("flag off", StreetHarness.Generate(idString, size))
        };
        cities.AddRange(_flagOnCities(idString, size));

        foreach (var (label, store) in cities)
        {
            var c = _censusOf(idString, size, store);
            var core = TwoCoreOf(store);

            foreach (var q in c.Quarters.GetQuarters())
            {
                var ring = q.GetDelims().Select(d => d.StartPoint).ToList();
                var corners = new HashSet<int>(q.GetDelims().Select(d => d.StreetPoint.Id));

                foreach (var sp in store.GetStreetPoints())
                {
                    if (corners.Contains(sp.Id) || !core.Contains(sp.Id)) continue;

                    Assert.False(_containsInPlan(ring, sp.Pos),
                        $"{idString}@{size} {label}: a block of {ring.Count} corners "
                        + $"swallows junction {sp.Id} at {sp.Pos}");
                }
            }

            /*
             * Not vacuous: the city has a block graph with cycles in it at all.
             */
            if (size >= 500f)
            {
                Assert.True(core.Count > 5, $"{idString}@{size} {label}: 2-core is {core.Count}");
            }
        }
    }


    /**
     * PROPERTY THREE. No block's outline crosses itself.
     *
     * The §3c face did: it ran along the ground road under the deck on BOTH sides within
     * one face, which is a ring that comes back through itself. Zero on every seed with
     * either flag, flat or on terrain - and it was ten blocks of Yelukhdidru@3000, eight
     * of seed017@2400 and one of seed000@1500 before this work package.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoBlockOutlineCrossesItself(string idString, float size)
    {
        var cities = new List<(string, StrokeStore)>
        {
            ("flag off", StreetHarness.Generate(idString, size))
        };
        cities.AddRange(_flagOnCities(idString, size));

        foreach (var (label, store) in cities)
        {
            var c = _censusOf(idString, size, store);

            foreach (var q in c.Quarters.GetQuarters())
            {
                var ring = q.GetDelims().Select(d => d.StartPoint).ToList();
                Assert.False(_selfIntersects(ring),
                    $"{idString}@{size} {label}: a block of {ring.Count} corners crosses itself");
            }
        }
    }


    /**
     * ⚠️ AND ALL THREE ARE ONLY WORTH ANYTHING IF THE CITIES THEY RUN OVER ACTUALLY HAVE
     * STRUCTURES IN THEM.
     *
     * Five of the seven seeds carry one on the flat ground and four on the shipped
     * terrain; the other two are the ones §11 records as refusing everything. Asserted
     * here rather than trusted, because a placement rule that quietly stopped firing
     * would leave the three properties above passing on a city with nothing to test.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void TheFlagOnCitiesTheseRunOverCarryStructures(string idString, float size)
    {
        int withStructures = 0;
        int feetOnBlocks = 0;

        foreach (var (_, store) in _flagOnCities(idString, size))
        {
            if (store.GetStrokes().Any(s => StrokeKinds.IsStructure(s.Kind)))
            {
                ++withStructures;
            }

            /*
             * A FOOT: a junction carrying both a structure and ordinary streets, i.e.
             * exactly the junction at which the filtered and unfiltered walks differ.
             */
            foreach (var sp in store.GetStreetPoints())
            {
                var arms = sp.GetAngleArray();
                if (arms.Any(s => StrokeKinds.IsStructure(s.Kind))
                    && BlockGraph.ArmCountOf(sp) >= 2)
                {
                    ++feetOnBlocks;
                }
            }
        }

        bool expected = size >= 500f;
        Assert.Equal(expected, withStructures > 0);
        Assert.Equal(expected, feetOnBlocks > 0);
    }


    // ------------------------------------------- the structure footprint on the estate

    /**
     * ⚠️ THE SECOND GAP §3c NAMED: the structure is INSIDE the merged block.
     *
     * A ramp is a ground-level road lying in the block's interior in plan, the estate is
     * the block outline inset by the pavement width, and _createBuildings would put a
     * building on it. Measured over the seven seeds BEFORE the exclusion existed, with
     * the merge already in place: 39 of 229 buildings in the flat cities stood on a
     * structure's carriageway - 1, 0, 0, 0, 12, 12, 14 per seed - the worst by
     * 5841 square metres, and 6 of 323 on the shipped terrain, worst 1885.
     *
     * After: none, anywhere, by any area at all.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoBuildingStandsOnAStructure(string idString, float size)
    {
        foreach (var (label, store) in _flagOnCities(idString, size))
        {
            var c = _censusOf(idString, size, store);
            var structures = store.GetStrokes().FindAll(s => StrokeKinds.IsStructure(s.Kind));

            foreach (var q in c.Quarters.GetQuarters())
            foreach (var e in q.GetEstates())
            foreach (var b in e.GetBuildings())
            {
                foreach (var s in structures)
                {
                    Assert.Equal(0.0, _overlapArea(b.GetPoints(), BlockGraph.FootprintOf(s, 0f)));
                }
            }

            /*
             * Not vacuous: some block of this city has both a building and a structure
             * standing in it, so the exclusion had something to do.
             */
            if (size >= 1500f)
            {
                Assert.Contains(c.Quarters.GetQuarters(), q =>
                    q.GetEstates().Any(e => e.GetBuildings().Count > 0)
                    && structures.Any(s => _reaches(q, s)));
            }
        }
    }


    /**
     * A block with nothing to exclude gets back the very list it handed in - not an
     * equal one. That is what keeps the flag-off city on the code it always ran, and the
     * assertion is on identity so that a rebuild which happens to compare equal cannot
     * satisfy it.
     */
    [Fact]
    public void ExcludingNothingReturnsTheSameEstate()
    {
        var estate = _square(0f, 0f, 100f);

        Assert.Same(estate, BlockGraph.ExcludeStructures(
            estate, Array.Empty<Stroke>(), 3f));
    }


    /**
     * ⚠️ AND A ConnectorBridge IS NOT EXCLUDED. It is an ordinary ground road and blocks
     * are built along it in every shipped flat city; a rule phrased as "not a Street"
     * would cut a hole out of a block for one.
     */
    [Fact]
    public void AConnectorBridgeIsNotExcludedFromAnEstate()
    {
        var estate = _square(0f, 0f, 100f);
        var store = new StrokeStore(1000f);

        var ordinary = _street(store, _pointAt(-50f, 0f), _pointAt(50f, 0f), 1f,
            StrokeKind.ConnectorBridge);
        var ramp = _street(store, _pointAt(-50f, 10f), _pointAt(50f, 10f), 1f,
            StrokeKind.Ramp);

        Assert.Same(estate, BlockGraph.ExcludeStructures(estate, new[] { ordinary }, 3f));
        Assert.NotSame(estate, BlockGraph.ExcludeStructures(estate, new[] { ramp }, 3f));
    }


    /**
     * The footprint is the carriageway plus the margin, on all four sides. Asserted on
     * the corners rather than on an area, because an area cannot tell a rectangle that is
     * too long from one that is too wide.
     */
    [Fact]
    public void AFootprintIsTheCarriagewayPlusTheMargin()
    {
        var store = new StrokeStore(1000f);
        var s = _street(store, _pointAt(-50f, 0f), _pointAt(50f, 0f), 1f, StrokeKind.Ramp);

        float half = s.StreetWidth() / 2f;
        var poly = BlockGraph.FootprintOf(s, 3f);

        Assert.Equal(4, poly.Count);

        long minX = poly.Min(p => p.X), maxX = poly.Max(p => p.X);
        long minY = poly.Min(p => p.Y), maxY = poly.Max(p => p.Y);

        Assert.Equal((long)((-50f - 3f) * 10f), minX);
        Assert.Equal((long)((50f + 3f) * 10f), maxX);
        Assert.Equal((long)(-(half + 3f) * 10f), minY);
        Assert.Equal((long)((half + 3f) * 10f), maxY);
    }


    /**
     * ⚠️ A structure can cut a block's buildable land in TWO, and the caller cannot cope
     * with that: QuarterGenerator._createBuildings concatenates every polygon of the
     * solution into one ring - its own TXWTODO says so and it has done it since it was
     * written. So the largest piece is chosen here, where the split happens.
     *
     * Without the choice the two pieces would be strung into a single self-crossing
     * outline and a building would be designed on it.
     */
    [Fact]
    public void OnlyTheLargestPieceSurvivesAStructureThatCutsAnEstateInTwo()
    {
        var estate = _square(0f, 0f, 100f);
        var store = new StrokeStore(1000f);

        /*
         * Straight across the middle, and off centre so the two halves differ.
         */
        var ramp = _street(store, _pointAt(-200f, 20f), _pointAt(200f, 20f), 1f,
            StrokeKind.Ramp);

        var cut = BlockGraph.ExcludeStructures(estate, new[] { ramp }, 3f);

        Assert.Single(cut);

        /*
         * The bigger half is the southern one: the ramp sits at y = +20 in a block that
         * runs -100 .. +100.
         */
        Assert.True(cut[0].Min(p => p.Y) < -900);
        Assert.True(cut[0].Max(p => p.Y) < 200);
    }


    // ------------------------------------------------------- two structures may not cross

    /**
     * ⚠️ §3c's OTHER gap: two decks over the same area are both at Level = 1.
     *
     * The plan offered two answers - refuse the corridor, or send one deck to level 2.
     * Level 2 is not an alternative on this ruleset and the arithmetic says so: the climb
     * doubles, so a ramp is 2 * DeckHeight / MaxRampGrade = 160 m, and with the deck's
     * overhang the corridor needs 179.7 m of arm at each end. The longest stroke in ANY
     * of the seven pinned cities with the flag on is 179.2 m, in a city that places no
     * structure at all - so ArmTooShort would refuse every level-2 corridor there is, and
     * refusing outright is the same outcome named honestly.
     */
    [Fact]
    public void ALevelTwoDeckWouldNeedMoreArmThanAnyPinnedCityHas()
    {
        var policy = new GradePolicy();

        float levelTwoRamp =
            2f * StreetLevels.DeckHeight / policy.MaxRampGrade;
        Assert.Equal(160f, levelTwoRamp, 3);

        /*
         * The overhang a corridor must leave past the crossing, which is the same
         * RampClearance the generator defaults to - the widest carriageway this ruleset
         * builds.
         */
        float needed = levelTwoRamp + Stroke.WidthForWeight(1.3f);

        foreach (var seed in Seeds)
        {
            string idString = (string)seed[0];
            float size = (float)seed[1];

            var store = StreetHarness.GenerateHeavyFirst(idString, size);
            float longest = store.GetStrokes().Count == 0
                ? 0f
                : store.GetStrokes().Max(s => s.Length);

            Assert.True(longest < needed,
                $"{idString}@{size}: longest stroke {longest:F1} m reaches the "
                + $"{needed:F1} m a level-two ramp needs");
        }
    }


    /**
     * The rule itself: a corridor whose chain would cross an accepted one is refused.
     */
    [Fact]
    public void TwoStructuresWhoseDecksCrossAreNotBothBuilt()
    {
        var f = _twoCorridors(crossing: true);
        var report = _place(f);

        Assert.Equal(1, report.Placed);
        Assert.Equal(1, _refusals(report, StructureRefusal.CrossesAnotherStructure));
    }


    /**
     * The control, and it is the same fixture with one corridor slid sideways: the decks
     * no longer cross, nothing is refused for it, and BOTH are built. Without this the
     * rule above passes on a fixture that refuses the second corridor for some other
     * reason entirely.
     */
    [Fact]
    public void TwoStructuresThatDoNotCrossAreBothBuilt()
    {
        var f = _twoCorridors(crossing: false);
        var report = _place(f);

        Assert.Equal(2, report.Placed);
        Assert.Equal(0, _refusals(report, StructureRefusal.CrossesAnotherStructure));
    }


    /**
     * ⚠️ A DECK CROSSING ANOTHER STRUCTURE'S RAMP is the same defect and no fixture built
     * out of two whole corridors can reach it: making two corridors cross at all crosses
     * them deck to deck, because a deck is the middle of its own chain. A mutation that
     * compared only the two decks survived every test in this file until this one existed.
     *
     * So the pairwise rule is driven directly. The ramp climbs through the deck's level
     * somewhere along its run, so a deck laid across it has no answer to "which of us is
     * higher here".
     */
    [Fact]
    public void ADeckMayNotCrossAnotherStructuresRamp()
    {
        /* one structure's ramp, climbing away northwards from its foot */
        var footOfIt = _pointAt(0f, -100f);
        var deckStart = _pointAt(0f, 0f, 1);
        var deckEnd = _pointAt(0f, 200f, 1);

        var ramp = _unstoredStroke(footOfIt, deckStart, StrokeKind.Ramp);
        var deckOfIt = _unstoredStroke(deckStart, deckEnd, StrokeKind.Bridge);

        /* another structure's deck, laid straight across the middle of that ramp */
        var acrossWest = _pointAt(-100f, -50f, 1);
        var acrossEast = _pointAt(100f, -50f, 1);

        var across = _unstoredStroke(acrossWest, acrossEast, StrokeKind.Bridge);
        var itsRamp = _unstoredStroke(_pointAt(-200f, -50f), acrossWest, StrokeKind.Ramp);

        Assert.False(StructurePlacer.ChainsAreClear(
            new[] { itsRamp, across }, new[] { ramp, deckOfIt }));

        /*
         * ⚠️ AND THE MIRROR IMAGE, because one of these two on its own leaves a mutation
         * alive: above it is my DECK crossing their RAMP, so a rule that looked only at
         * my chain's deck still refuses. Here it is my RAMP crossing their DECK, and a
         * rule that looked only at my chain's deck lets it through. §7q's
         * symmetric-survivor lesson, and the second half was written because the first
         * half alone was walked through.
         */
        var mineWest = _pointAt(-100f, 100f);
        var mineEast = _pointAt(100f, 100f, 1);
        var myRamp = _unstoredStroke(mineWest, mineEast, StrokeKind.Ramp);
        var myDeck = _unstoredStroke(mineEast, _pointAt(300f, 100f, 1), StrokeKind.Bridge);

        Assert.False(StructurePlacer.ChainsAreClear(
            new[] { myRamp, myDeck }, new[] { ramp, deckOfIt }));

        /*
         * The control, and it is the same two chains slid clear of each other: nothing
         * crosses, and neither deck is near the other's ramp.
         */
        var clearWest = _pointAt(-100f, -400f, 1);
        var clearAcross = _unstoredStroke(clearWest, _pointAt(100f, -400f, 1), StrokeKind.Bridge);
        var clearRamp = _unstoredStroke(_pointAt(-200f, -400f), clearWest, StrokeKind.Ramp);

        Assert.True(StructurePlacer.ChainsAreClear(
            new[] { clearRamp, clearAcross }, new[] { ramp, deckOfIt }));

        /*
         * ⚠️ Two chains that MEET at a junction are clear of each other, and that is not
         * a detail: two ramps leaving one foot is a legitimate shape, and Stroke.Intersects
         * reports a shared endpoint as a crossing. The placer never produces the case -
         * OverlapsAStructure refuses a candidate that shares a junction with an accepted
         * one long before this - so it is asserted here or nowhere.
         */
        var sibling = _unstoredStroke(footOfIt, _pointAt(-200f, -100f, 1), StrokeKind.Ramp);

        Assert.True(StructurePlacer.ChainsAreClear(new[] { sibling }, new[] { ramp }));
    }


    /**
     * ⚠️ THE BLOCK TRACE NEVER TOUCHES A STRUCTURE AT ALL.
     *
     * Stronger than "no delimiter is on one", and it is what makes refusing to START a
     * trace on a structure load bearing: without that half the trace walks out along a
     * ramp, finds no block arm at the deck end, turns round, arrives back at the junction
     * it started from and breaks - marking the ramp traversed in both directions and
     * discarding the face. Harmless as it happens, because arriving back at the start
     * ends the walk before any ordinary stroke is consumed, which is precisely why the
     * mutation that removes the start filter passes every other test in this file.
     *
     * `Stroke.TraversedAB`/`TraversedBA` are the trace's own record of where it went, so
     * this asks the production code where it has been rather than inspecting what it
     * produced.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void TheBlockTraceNeverTouchesAStructure(string idString, float size)
    {
        foreach (var (label, store) in _flagOnCities(idString, size))
        {
            _censusOf(idString, size, store);

            foreach (var s in store.GetStrokes())
            {
                if (!StrokeKinds.IsStructure(s.Kind)) continue;

                Assert.False(s.TraversedAB, $"{idString}@{size} {label}: {s.Kind} {s.Sid}");
                Assert.False(s.TraversedBA, $"{idString}@{size} {label}: {s.Kind} {s.Sid}");
            }

            /*
             * The control: an ordinary street IS traversed, so "nothing was traversed"
             * cannot satisfy this.
             */
            if (size >= 500f)
            {
                Assert.Contains(store.GetStrokes(),
                    s => BlockGraph.IsBlockEdge(s) && (s.TraversedAB || s.TraversedBA));
            }
        }
    }


    /**
     * ⚠️ AND IT REFUSES NOTHING IN ANY CITY THE RULESET BUILDS - 0 of the 667 crossings
     * over the seven seeds, flat and on the shipped terrain, and 0 of the 402 structure
     * strokes the flat cities carry crosses another.
     *
     * §9's "a rule can be invisible to unlimited real data" for the third time in this
     * phase. Recorded here so that the day a ruleset starts producing crossing decks the
     * cost is visible, rather than the rule looking like it was never needed.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoPinnedCityHasTwoStructuresCrossingEachOther(string idString, float size)
    {
        foreach (var (label, store) in _flagOnCities(idString, size))
        {
            var structures = store.GetStrokes().FindAll(s => StrokeKinds.IsStructure(s.Kind));

            for (int i = 0; i < structures.Count; ++i)
            for (int j = i + 1; j < structures.Count; ++j)
            {
                Stroke a = structures[i], b = structures[j];
                if (a.A == b.A || a.A == b.B || a.B == b.A || a.B == b.B) continue;

                Assert.Null(a.Intersects(b));
            }
        }
    }


    // -------------------------------------------------------------------------- fixtures

    private const float ClusterSize = 5000f;


    private static StreetPoint _pointAt(float x, float y, sbyte level = 0)
    {
        var sp = new StreetPoint { ClusterId = 0, Level = level };
        sp.SetPos(x, y);
        return sp;
    }


    private static Stroke _street(
        StrokeStore store, StreetPoint a, StreetPoint b, float weight,
        StrokeKind kind = StrokeKind.Street)
    {
        var s = new Stroke
        {
            ClusterId = 0, IsPrimary = false, Weight = weight, Kind = kind, Level = 0
        };
        s.A = a;
        s.B = b;
        store.AddStroke(s);
        return s;
    }


    /**
     * A stroke that never joins a store. ChainsAreClear reads only geometry and kind, and
     * a chain's members legitimately share junctions - which StrokeStore.AddPoint refuses
     * to accept twice at one position.
     */
    private static Stroke _unstoredStroke(StreetPoint a, StreetPoint b, StrokeKind kind)
    {
        var s = new Stroke
        {
            ClusterId = 0, IsPrimary = false, Weight = 1f, Kind = kind, Level = 0
        };
        s.A = a;
        s.B = b;
        return s;
    }


    private sealed class FootFixture
    {
        internal StreetPoint Foot;
        internal Stroke West, East, Ramp;
    }


    /**
     * A ramp's foot: an ordinary street arriving from the west, another leaving to the
     * east, and the ramp climbing away southwards - which puts it strictly between the
     * two streets in the walk the block trace takes.
     *
     * The east street is bent slightly north on purpose: two exactly opposed arms are
     * the one shape SectionMitre has no answer for, and a fixture that walked into it
     * would be testing the mitre's degenerate branch rather than this.
     */
    private static FootFixture _footFixture()
    {
        var store = new StrokeStore(ClusterSize);

        var foot = _pointAt(0f, 0f);
        var west = _pointAt(-100f, 0f);
        var east = _pointAt(100f, 40f);
        var deck = _pointAt(0f, -100f, 1);

        return new FootFixture
        {
            Foot = foot,
            West = _street(store, west, foot, 1f),
            East = _street(store, foot, east, 1f),
            Ramp = _street(store, foot, deck, 1f, StrokeKind.Ramp)
        };
    }


    private sealed class TwoCorridors
    {
        internal StrokeStore Store;
        internal Dictionary<int, float> Ground = new();

        internal float GroundAt(StreetPoint sp) => 0f;
    }


    /**
     * Two independent crossings whose corridors are long enough to lift, arranged so
     * that their DECKS either cross each other or do not.
     *
     * Corridor A runs east-west through MA and its deck reaches from x = -320 to
     * x = +120 at y = 0. Corridor B runs north-south through MB and its deck runs from
     * y = -120 to y = +120 at MB's own x. Sliding MB from x = 100 to x = 400 is the only
     * difference between the two cases: at 100 the decks cross, at 400 corridor B is
     * past the east end of deck A entirely.
     *
     * Each crossing carries its own ring, because a grade separated crossing has no slip
     * roads and a bare cross would be refused for WouldDisconnect before this rule was
     * ever reached; one light stroke joins the two rings so the fixture is one component.
     */
    private static TwoCorridors _twoCorridors(bool crossing)
    {
        float bx = crossing ? 100f : 400f;

        var store = new StrokeStore(ClusterSize);
        var f = new TwoCorridors { Store = store };

        /* corridor A: west-east through MA, feet 300 m either side */
        var ma = _pointAt(-100f, 0f);
        var wa = _pointAt(-400f, 0f);
        var ea = _pointAt(200f, 0f);
        var an = _pointAt(-100f, 150f);
        var asouth = _pointAt(-100f, -150f);

        _street(store, wa, ma, 1f);
        _street(store, ma, ea, 1f);
        _street(store, ma, an, 1f);
        _street(store, ma, asouth, 1f);

        _street(store, wa, an, RingWeight);
        _street(store, an, ea, RingWeight);
        _street(store, wa, asouth, RingWeight);
        _street(store, asouth, ea, RingWeight);

        /* corridor B: south-north through MB, feet 200 m either side */
        var mb = _pointAt(bx, 0f);
        var bn = _pointAt(bx, 200f);
        var bs = _pointAt(bx, -200f);

        /*
         * Its two cross arms are deliberately NOT opposed, so that the north-south pair
         * is unambiguously the straight one.
         */
        var bw = _pointAt(bx - 150f, 60f);
        var be = _pointAt(bx + 150f, 40f);

        _street(store, bs, mb, 1f);
        _street(store, mb, bn, 1f);
        _street(store, mb, bw, 1f);
        _street(store, mb, be, 1f);

        _street(store, bs, bw, RingWeight);
        _street(store, bw, bn, RingWeight);
        _street(store, bs, be, RingWeight);
        _street(store, be, bn, RingWeight);

        /* one component */
        _street(store, an, bn, RingWeight);

        return f;
    }


    /**
     * Light enough that the relaxation never corrects a ring stroke; two arms each, so
     * they add no crossing of their own. Same value and same reason as
     * StructurePlacementTests.
     */
    private const float RingWeight = 0.2f;


    /**
     * Ramp clearance is switched OFF here on purpose. It is judged AFTER the rule under
     * test, so it cannot mask a result - but a fixture built to make two decks cross
     * necessarily brings other roads close to the ramps, and the point of this fixture is
     * one rule and not the ruleset.
     */
    private static StructurePlacementReport _place(TwoCorridors f)
        => StructurePlacer.Place(
            f.Store, 0, new GradePolicy(), f.GroundAt,
            rampClearance: 0f, minSpanLength: 10f, maxSpanLength: 0f,
            maxJunctionSpacing: 1000f);


    private static int _refusals(StructurePlacementReport report, StructureRefusal reason)
        => report.Refused.TryGetValue(reason, out int n) ? n : 0;


    // ------------------------------------------------------------------------- geometry

    private static List<List<IntPoint>> _square(float cx, float cy, float half)
    {
        var poly = new List<IntPoint>
        {
            new IntPoint((int)((cx - half) * 10f), (int)((cy - half) * 10f)),
            new IntPoint((int)((cx + half) * 10f), (int)((cy - half) * 10f)),
            new IntPoint((int)((cx + half) * 10f), (int)((cy + half) * 10f)),
            new IntPoint((int)((cx - half) * 10f), (int)((cy + half) * 10f))
        };

        return new List<List<IntPoint>> { poly };
    }


    private static double _overlapArea(List<Vector3> building, List<IntPoint> footprint)
    {
        var poly = building
            .Select(v => new IntPoint((int)(v.X * 10f), (int)(v.Z * 10f)))
            .ToList();

        var clipper = new Clipper();
        clipper.AddPath(poly, PolyType.ptSubject, true);
        clipper.AddPath(footprint, PolyType.ptClip, true);

        var solution = new List<List<IntPoint>>();
        clipper.Execute(ClipType.ctIntersection, solution);

        double area = 0.0;
        foreach (var p in solution) area += Math.Abs(Clipper.Area(p)) / 100.0;
        return area;
    }


    private static bool _reaches(Quarter q, Stroke s)
    {
        var aabb = q.AABB;
        float minX = Single.Min(s.A.Pos.X, s.B.Pos.X);
        float maxX = Single.Max(s.A.Pos.X, s.B.Pos.X);
        float minY = Single.Min(s.A.Pos.Y, s.B.Pos.Y);
        float maxY = Single.Max(s.A.Pos.Y, s.B.Pos.Y);

        return !(maxX < aabb.AA.X || minX > aabb.BB.X || maxY < aabb.AA.Z || minY > aabb.BB.Z);
    }


    /**
     * The junctions of the block graph that lie on a cycle: peel every junction with
     * fewer than two block-graph arms until none is left.
     *
     * Shared with SpurBlocks, which reconstructs the blocks §7t.5(b) would give the city
     * out of exactly this set - internal rather than copied, so that "the 2-core" means
     * one thing in both files.
     */
    internal static HashSet<int> TwoCoreOf(StrokeStore store)
    {
        var degree = new Dictionary<int, int>();
        var adjacency = new Dictionary<int, List<int>>();

        foreach (var sp in store.GetStreetPoints())
        {
            degree[sp.Id] = 0;
            adjacency[sp.Id] = new List<int>();
        }

        foreach (var s in store.GetStrokes())
        {
            if (!BlockGraph.IsBlockEdge(s)) continue;
            if (!degree.ContainsKey(s.A.Id) || !degree.ContainsKey(s.B.Id)) continue;

            ++degree[s.A.Id];
            ++degree[s.B.Id];
            adjacency[s.A.Id].Add(s.B.Id);
            adjacency[s.B.Id].Add(s.A.Id);
        }

        var live = new HashSet<int>(degree.Keys);
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (int id in live.ToList())
            {
                if (degree[id] >= 2) continue;

                live.Remove(id);
                foreach (int neighbour in adjacency[id])
                {
                    if (live.Contains(neighbour)) --degree[neighbour];
                }

                degree[id] = 0;
                changed = true;
            }
        }

        return live;
    }


    private static bool _containsInPlan(List<Vector2> ring, Vector2 p)
    {
        bool inside = false;
        int n = ring.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
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


    private static bool _selfIntersects(List<Vector2> ring)
    {
        int n = ring.Count;
        if (n < 4) return false;

        for (int i = 0; i < n; ++i)
        {
            for (int j = i + 2; j < n; ++j)
            {
                if (i == 0 && j == n - 1) continue;
                if (_segmentsCross(ring[i], ring[(i + 1) % n], ring[j], ring[(j + 1) % n]))
                {
                    return true;
                }
            }
        }

        return false;
    }


    /*
     * ================================================== a split inset ================
     */

    /**
     * WP-B6 triage item 3 — when the pavement inset splits a block on its own.
     *
     * QuarterGenerator._createBuildings concatenates every polygon of the inset into one
     * ring, which is a self-crossing outline the moment there are two of them. WP-B5 made
     * ExcludeStructures return at most one polygon, which covers a split caused by
     * subtracting a structure; a block pinched to less than two pavement widths across its
     * middle splits without any structure being involved, and that is what LargestOf is.
     *
     * Driven on two rings so that "the largest" is a choice and not the only answer.
     */
    [Fact]
    public void ASplitInsetIsResolvedToItsLargestPiece()
    {
        var small = new List<IntPoint>
        {
            new(0, 0), new(100, 0), new(100, 100), new(0, 100)
        };
        var large = new List<IntPoint>
        {
            new(1000, 0), new(1400, 0), new(1400, 400), new(1000, 400)
        };

        var chosen = BlockGraph.LargestOf(new List<List<IntPoint>> { small, large });

        Assert.Single(chosen);
        Assert.Same(large, chosen[0]);

        var otherOrder = BlockGraph.LargestOf(new List<List<IntPoint>> { large, small });

        Assert.Single(otherOrder);
        Assert.Same(large, otherOrder[0]);
    }


    /**
     * With nothing to choose between, the very list handed in comes back.
     *
     * Not "a list holding the same polygon": the same object, so that a block whose inset
     * did not split runs exactly the code it ran before this existed. Measured, that is
     * every estate of every flag-off city - 0 of 763 over the seven pinned seeds split.
     */
    [Fact]
    public void AnUnsplitInsetIsTheVeryListItWas()
    {
        var one = new List<List<IntPoint>>
        {
            new() { new IntPoint(0, 0), new IntPoint(100, 0), new IntPoint(100, 100) }
        };

        Assert.Same(one, BlockGraph.LargestOf(one));

        var none = new List<List<IntPoint>>();

        Assert.Same(none, BlockGraph.LargestOf(none));
    }


    private static bool _segmentsCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        static float Cross(Vector2 p, Vector2 q) => p.X * q.Y - p.Y * q.X;

        Vector2 r = b - a;
        Vector2 s = d - c;

        float denominator = Cross(r, s);
        if (Single.Abs(denominator) < 1e-9f) return false;

        float t = Cross(c - a, s) / denominator;
        float u = Cross(c - a, r) / denominator;

        return t > 1e-4f && t < 1f - 1e-4f && u > 1e-4f && u < 1f - 1e-4f;
    }
}
