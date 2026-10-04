using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using engine.streets;
using engine.streets.generation;
using engine.world;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * ⚠️ WP-O3 — A BUILDING IS FOUNDED ON ITS OWN PIECE OF GROUND, OVER THE SHIPPED WORLD.
 *
 * Ledger item (o), repair (b), last of three work packages. WP-O1 put the block trace on
 * the 2-core, WP-O2 subtracted the dead-end spur from the estate and gave every remaining
 * polygon its own building - and left BuildingFooting founding all of them on the BLOCK's
 * lowest corner, which is the bound the class was written around when a block carried one
 * building and one estate. §7t.10.7 measured what that costs the higher piece of a split
 * block on the shipped terrain: a median 7.46 m flag off and 6.21 m flag on, worst 21.2 and
 * 28.1 m. It is EXACTLY ZERO on a flat city, which is why every gate here is on the ground
 * the game ships.
 *
 * ⚠️ AND THE PER-PIECE BOUND THE BRIEF ASKED FOR - the lowest of a piece's own boundary
 * heights - IS NOT A BOUND. See TheObviousPerPieceBoundIsNotABound below, which is the
 * finding that decided the shape of the fix and is asserted rather than described.
 *
 * The world here is the real one: GenerateClustersOperator's own cluster list seeded
 * "mydear", seventy cities, on ShippedTerrain's relaxed heights - i.e. what
 * RelaxedStreetHeight over TerrainStreetHeight answers in the game.
 */
public class BuildingFootingWorldTests
{
    private static readonly object _lo = new();
    private static readonly Dictionary<bool, Measured> _measured = new();


    private sealed class Block
    {
        internal string City;
        internal int Buildings;

        /**
         * How far above the block's lowest corner the floor's own minimum over each
         * footprint is, per building - i.e. exactly what the block-wide bound over-sinks
         * that building by, and exactly what the per-piece bound removes.
         */
        internal List<float> OverSink = new();
    }


    private sealed class Measured
    {
        internal int Quarters, Buildings, Blocks2, CapsBuilt, NoCap;
        internal int VerticesChecked, VerticesFloating;
        internal float WorstFloat;
        internal int NaiveChecked, NaiveFloating;
        internal float NaiveWorstFloat;
        internal int CapVerticesInsideAFootprint, CapVertexIsTheMinimum;
        internal int Shopfronts, ShopfrontsOffTheirOwnStoreyGrid;
        internal float WorstStoreyOffset;
        internal List<Block> Blocks = new();
    }


    /**
     * The seventy shipped cities with their blocks, estates and buildings, on the shipped
     * terrain, read once per flag.
     *
     * ⚠️ ITS OWN ClusterDesc OBJECTS and not BlockRingClosureTests' shared ones. This is
     * the only file that needs a height SOURCE on the cluster rather than a table beside
     * it - Quarter.CornerGroundHeightAt reads ClusterDesc.StreetHeightSource - and xUnit
     * runs collections in parallel, so writing that field on a shared cluster would change
     * what every other file measures. The cluster list is a pure function of its seed
     * (§7v.10), so these are the same seventy cities.
     */
    private static Measured World(bool gradeSeparation)
    {
        lock (_lo)
        {
            if (_measured.TryGetValue(gradeSeparation, out var cached)) return cached;

            var m = new Measured();

            var op = new GenerateClustersOperator("mydear");
            op._generateClusterList(null, out var clusters);

            foreach (var cd in clusters)
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

                cd.StreetHeightSource = ShippedTerrain.StreetHeightsOf(cd, store);
                var quarters = StreetHarness.GenerateQuarters(cd, store, cd.IdString);

                foreach (var q in quarters.GetQuarters())
                {
                    ++m.Quarters;

                    var floor = BlockFloor.Of(q);
                    if (null == floor) ++m.NoCap; else ++m.CapsBuilt;

                    float blockMin = BuildingFooting.MinGroundOf(q);
                    var block = new Block { City = cd.IdString };

                    foreach (var est in q.GetEstates())
                    foreach (var b in est.GetBuildings())
                    {
                        ++m.Buildings;
                        ++block.Buildings;

                        float baseGround = BuildingFooting.MinGroundOf(q, b);
                        block.OverSink.Add(baseGround - blockMin);

                        /*
                         * The bound itself, at every corner of every footprint, against the
                         * floor's OWN surface. BlockFloor is the production expression, so
                         * this would be circular on its own - what makes it a gate is
                         * BuildingFootingTests, which asserts the identity against
                         * DrawnBlockFloor's independent reading of the emitted mesh on the
                         * four baseline cities. Here it is the coverage: seventy cities.
                         */
                        foreach (var p in b.GetPoints())
                        {
                            if (null == floor) continue;
                            if (!floor.TryHeightAt(new Vector2(p.X, p.Z), out float h))
                            {
                                continue;
                            }

                            ++m.VerticesChecked;
                            if (baseGround > h + 1e-3f)
                            {
                                ++m.VerticesFloating;
                                m.WorstFloat = Single.Max(m.WorstFloat, baseGround - h);
                            }

                            /*
                             * ...and the same question of the rule this work package was
                             * briefed to build, which is what says the brief was wrong.
                             */
                            float naive = Single.MaxValue;
                            foreach (var v in b.GetPoints())
                            {
                                naive = Single.Min(
                                    naive,
                                    BuildingFooting.GroundAt(q, new Vector2(v.X, v.Z)));
                            }

                            ++m.NaiveChecked;
                            if (naive > h + 1e-3f)
                            {
                                ++m.NaiveFloating;
                                m.NaiveWorstFloat = Single.Max(m.NaiveWorstFloat, naive - h);
                            }
                        }

                        /*
                         * ⚠️ WHY REAL DATA CANNOT KILL "the cap's own corners inside the
                         * footprint count too" - see the gate below, where two guesses at
                         * the reason are refuted by these counts before the third holds.
                         */
                        if (null != floor)
                        {
                            var plan = b.GetPoints()
                                .Select(p => new Vector2(p.X, p.Z)).ToList();
                            foreach (var v in floor.Vertices)
                            {
                                if (!DrawnBlockFloor.Contains(plan, new Vector2(v.X, v.Z)))
                                {
                                    continue;
                                }

                                ++m.CapVerticesInsideAFootprint;
                                if (v.Y < baseGround + 1e-4f) ++m.CapVertexIsTheMinimum;
                            }
                        }

                        /*
                         * A shop is snapped to a whole storey of ITS OWN building, which is
                         * the whole of "align to stories, ditch the stairs" once a block
                         * carries more than one of them.
                         */
                        foreach (var sf in b.GetShopFronts())
                        {
                            Vector2 plan = BuildingFooting.PlanOf(sf);
                            float rise = BuildingFooting.StoreyGroundAt(q, b, plan)
                                         - baseGround;
                            float off = Single.Abs(
                                rise - MathF.Round(rise / BuildingFooting.StoryHeight)
                                * BuildingFooting.StoryHeight);

                            ++m.Shopfronts;
                            if (off > 1e-3f) ++m.ShopfrontsOffTheirOwnStoreyGrid;
                            m.WorstStoreyOffset = Single.Max(m.WorstStoreyOffset, off);
                        }
                    }

                    if (block.Buildings >= 2) ++m.Blocks2;
                    if (block.Buildings > 0) m.Blocks.Add(block);
                }
            }

            _measured[gradeSeparation] = m;
            return m;
        }
    }


    /**
     * ⚠️ THE GUARANTEE, OVER THE WHOLE SHIPPED WORLD: no corner of any building's footprint
     * stands above the floor under it.
     *
     * This is the property the whole class exists for and the one the original report was
     * about - a house hanging in the air with its underside on show. Asserted at zero over
     * seventy cities and both flag states, not as a rate.
     */
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoFootprintCornerStandsAboveTheFloorUnderIt(bool gradeSeparation)
    {
        var m = World(gradeSeparation);

        Assert.True(m.VerticesChecked > 100000,
            $"only {m.VerticesChecked} footprint corners landed on a block floor");

        Assert.Equal(0, m.VerticesFloating);
        Assert.Equal(0f, m.WorstFloat);
    }


    /**
     * ⚠️ AND THE OBVIOUS PER-PIECE RULE FLOATS BUILDINGS, WHICH IS THE FINDING.
     *
     * "Take the lowest of the piece's own boundary heights, read off the block's boundary
     * ring the way BuildingFooting.GroundAt reads it" is the rule WP-O3 was briefed to
     * build, and it is not a bound at all: the cap's INTERIOR is one tessellation of the
     * ring left inside the pavement rim, and the tessellator is free to run a triangle
     * clean across the block, so the height of a corner the piece never comes near appears
     * under it. Nothing local to the piece can see that coming.
     *
     * Recorded as a count rather than described, so that a later simplification back to the
     * cheap rule fails here rather than in play. The counts are the number of footprint
     * corners it would have left in the air and the worst of them, over the same seventy
     * cities.
     */
    [Theory]
    //                 footprint corners it would leave in the air, and the worst of them
    [InlineData(false, 5402, 1.5f, 1.7f)]
    [InlineData(true, 4842, 5.5f, 5.7f)]
    public void TheObviousPerPieceBoundIsNotABound(
        bool gradeSeparation, int expectedFloating, float worstLo, float worstHi)
    {
        var m = World(gradeSeparation);

        Assert.Equal(expectedFloating, m.NaiveFloating);
        Assert.InRange(m.NaiveWorstFloat, worstLo, worstHi);

        /*
         * The control: it is a small fraction, which is exactly why it could not have been
         * found by looking at a city and is the reason this is a count rather than a
         * paragraph.
         */
        Assert.True(m.NaiveFloating * 10 < m.NaiveChecked,
            $"{m.NaiveFloating} of {m.NaiveChecked} is more than a tenth, so the cheap rule "
            + "is not merely unsound, it is visibly wrong and something else is up");
    }


    /**
     * ⚠️ WHAT THE BLOCK-WIDE BOUND COSTS, AND WHAT IS LEFT OF IT.
     *
     * The over-sink of one building is how far the floor's own minimum over its footprint
     * stands above the block's lowest corner - i.e. exactly the distance the block-wide
     * bound sank it by and exactly the distance the per-piece bound takes back. The class
     * comment's standard, measured when a block carried one building, was 0.19-0.61 m at
     * the median and 3.74 m at the worst.
     *
     * Per split block the figure that matters is the WORST of its buildings, which is
     * §7t.10.7's "extra burial imposed on the higher piece" seen through the pieces WP-O2
     * actually builds rather than through a nearest-corner reconstruction.
     *
     * After the change every one of these is zero by construction, because the base IS the
     * minimum; the point of the gate is the size of what was removed.
     */
    [Theory]
    //             blocks>1  median   worst    split median  >0.5m >2m  >5m
    [InlineData(false, 13, 0.40f, 0.50f, 15.0f, 15.4f, 5.5f, 6.2f, 13, 10, 8)]
    [InlineData(true, 793, 0.20f, 0.30f, 32.9f, 33.3f, 4.6f, 5.4f, 775, 602, 397)]
    public void TheBlockWideBoundOverSankTheHigherPieceOfASplitBlock(
        bool gradeSeparation, int expectedSplitBlocks,
        float medianLo, float medianHi, float worstLo, float worstHi,
        float splitMedianLo, float splitMedianHi,
        int overHalf, int overTwo, int overFive)
    {
        var m = World(gradeSeparation);

        /*
         * The control that says this file is measuring the city WP-O2 builds: §7v.6 counted
         * 13 blocks flag off and 793 flag on carrying more than one building, over its own
         * seventy cities built its own way.
         */
        Assert.Equal(expectedSplitBlocks, m.Blocks2);

        var all = m.Blocks.SelectMany(b => b.OverSink).ToList();
        var split = m.Blocks.Where(b => b.Buildings >= 2)
            .Select(b => b.OverSink.Max()).ToList();

        Assert.Equal(m.Buildings, all.Count);
        Assert.True(all.Count > 20000, $"only {all.Count} buildings");

        /*
         * Never negative: the floor cannot dip below the block's lowest corner, which is
         * the old bound's own argument and survives as a sanity property.
         */
        Assert.True(all.Min() >= -1e-3f,
            $"a building is founded {all.Min():F3} m below its block's lowest corner");

        Assert.InRange(_median(all), medianLo, medianHi);
        Assert.InRange(all.Max(), worstLo, worstHi);

        Assert.InRange(_median(split), splitMedianLo, splitMedianHi);
        Assert.Equal(overHalf, split.Count(x => x > 0.5f));
        Assert.Equal(overTwo, split.Count(x => x > 2f));
        Assert.Equal(overFive, split.Count(x => x > 5f));
    }


    /**
     * ⚠️ EVERY SHOP IS ON A WHOLE STOREY OF ITS OWN BUILDING.
     *
     * The owner's steer was to align shopfronts per storey and leave stairs out, and until
     * WP-O2 that was one statement: a block carried one building, so "a whole number of
     * storeys above the block's lowest corner" and "above the building's own floor" were
     * the same sentence. They are not any more, and the difference is the over-sink above -
     * a median 0.26-0.45 m and up to 33 m, i.e. a shop window at an arbitrary height inside
     * its own building's second storey.
     *
     * ⚠️ THIS IS THE GATE A MUTATION ASKED FOR. Reverting the storey reference to the
     * block's lowest corner passed every other assertion in this file and in
     * BuildingFootingTests, because both references are below the pavement in front of the
     * shop and both keep the shop within one storey OF THE PAVEMENT - which is what the
     * reachability gates ask. What it breaks is the alignment to the building.
     */
    [Theory]
    //                 shopfronts, of which off their own building's storey grid
    [InlineData(false, 572404, 0)]
    [InlineData(true, 527807, 0)]
    public void AShopIsOnAWholeStoreyOfItsOwnBuilding(
        bool gradeSeparation, int expectedShopfronts, int expectedOff)
    {
        var m = World(gradeSeparation);

        Assert.Equal(expectedShopfronts, m.Shopfronts);
        Assert.Equal(expectedOff, m.ShopfrontsOffTheirOwnStoreyGrid);
        Assert.True(m.WorstStoreyOffset < 1e-3f,
            $"a shopfront sits {m.WorstStoreyOffset:F3} m off its own building's storey "
            + "grid");
    }


    /**
     * ⚠️ A CORNER OF A BLOCK'S FLOOR INSIDE A FOOTPRINT IS ON ITS BOUNDARY, WHICH IS WHY
     * THAT TERM OF THE MINIMUM CANNOT BE TESTED FROM REAL DATA.
     *
     * BlockFloor.TryBoundsOver enumerates three kinds of candidate, and the middle one - the
     * cap's own corners inside the polygon - is required for the answer to be the exact
     * minimum of a piecewise linear surface. Deleting it passes every gate over seventy
     * cities, which is what mutation testing found.
     *
     * ⚠️ AND THE OBVIOUS EXPLANATION IS WRONG, WHICH IS WHY THIS COUNTS RATHER THAN
     * ASSERTING ZERO. "A cap has no vertex inside a footprint" sounds right - every cap
     * vertex is on the block's outline or on the pavement's inner edge, and a footprint is
     * that outline inset by exactly the width the inner edge stands at - and it is not true:
     * 164 924 of them are inside flag off and 114 207 flag on. They are inside because they
     * are ON the footprint's boundary and Clipper works in whole decimetres, so which side of
     * it they land on is a rounding decision.
     *
     * ⚠️ NOR IS "it is never the minimum" TRUE - 432 of them ARE the minimum flag off and
     * 414 flag on. What holds is one step weaker again and it is the one that matters: such a
     * vertex is on the footprint's boundary, so a crossing of the two boundaries answers with
     * the SAME height at the same place, and dropping the term changes no answer anywhere.
     * That is the reason no amount of real data can kill the mutation.
     *
     * The term is kept for the enumeration to be exact, and
     * BuildingFootingTests.OnlyTheCapsOwnCornersCanAnswerForAPolygon drives the branch
     * directly with a polygon that has no other candidate in it.
     */
    [Theory]
    //                 cap corners inside a footprint, of which tie the minimum
    [InlineData(false, 164924, 432)]
    [InlineData(true, 114207, 414)]
    public void ACornerOfTheFloorInsideAFootprintIsAlwaysOnItsBoundary(
        bool gradeSeparation, int expectedInside, int expectedDeciding)
    {
        var m = World(gradeSeparation);

        Assert.True(m.Buildings > 20000);
        Assert.Equal(expectedInside, m.CapVerticesInsideAFootprint);
        Assert.Equal(expectedDeciding, m.CapVertexIsTheMinimum);
    }


    /**
     * Every block the world builds has a floor to found a building on.
     *
     * The fallback to the block's corner range exists for a block whose cap cannot be
     * tessellated, and if that were common the guarantee above would be about the fallback
     * rather than about the surface.
     */
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryBlockOfTheShippedWorldHasAFloor(bool gradeSeparation)
    {
        var m = World(gradeSeparation);

        Assert.True(m.Quarters > 30000, $"only {m.Quarters} blocks");
        Assert.Equal(0, m.NoCap);
        Assert.Equal(m.Quarters, m.CapsBuilt);
    }


    private static float _median(List<float> values)
    {
        var sorted = values.OrderBy(x => x).ToList();
        return 0 == sorted.Count ? Single.NaN : sorted[sorted.Count / 2];
    }
}
