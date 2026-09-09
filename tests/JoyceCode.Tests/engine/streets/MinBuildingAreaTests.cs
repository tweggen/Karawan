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
 * ⚠️ A PIECE OF LAND UNDER TEN SQUARE METRES CARRIES NO BUILDING (§7y).
 *
 * §7x measured the other side of a cliff nobody had chosen: `_createBuildings` refused a
 * piece of buildable land at 0 m² and accepted one at 0.005 m², so 4 blocks flag off and 53
 * flag on carried a building under a square metre - the smallest fifty square centimetres
 * and three metres tall - standing on the tiny triangles between three roads. The owner's
 * decision, in their own words:
 *
 *      "I think it's safe to assume that in real world, the smallest buildings (apart from
 *      temporary things like tents) would be a 10m2 building, in the context of slums or
 *      small convenience stores, but not really in city centers."
 *
 * engine.world.MetaGen.MinBuildingArea is that number, beside StoryHeight because they are
 * the same kind of number, and QuarterGenerator.CanCarryABuilding is the one predicate.
 * ⚠️ IT IS THE SAME PREDICATE THE LOOP ALREADY HAD - "0 == polygon.Count" is this rule with
 * its threshold at zero - which is why it is unified rather than added beside it, and that
 * matters for more than tidiness: see AGoodPieceOfLandGetsTheDrawTheMatchboxUsedToTake.
 *
 * ⚠️ THE FINDING THIS ROUND CONTRADICTS ITS OWN BRIEF ON: the floor binds MORE downtown,
 * not less. The owner's sentence says such buildings do not occur in city centres, and the
 * expectation written round it was that a downtown block would be large enough for the
 * floor never to bind. Measured, the refusal fires on 1.01 % of blocks above downtownness
 * 0.7 and 0.39 % below 0.3 flag on, and flag off it fires ONLY downtown - 18 of its 20
 * cases are above 0.7 and none at all below 0.5. It is the same mechanism §7x found: the
 * downtown pavement is 6 m wide and the downtown roads are the heaviest, so the block
 * outline downtown is what gets eaten. A flat floor is right; it is simply doing most of
 * its work exactly where the owner said such buildings do not belong.
 *
 * ⚠️ AND AREA ALONE DOES NOT BOUND A SLIVER. §7y recorded fourteen flag-on buildings under
 * two metres THICK - the thinnest 0.32 m thick with a 95.7 m perimeter over 17.2 m² of
 * floor - every one of them clearing this floor comfortably, and left the width to a second
 * owner decision. That decision was taken on 2026-09-09: `MetaGen.MinBuildingWidth` is the
 * second term of the same predicate, and `MinBuildingWidthTests` is where it lives.
 *
 * ⚠️ SO EVERYTHING THIS FILE COUNTS IS THE AREA TERM SPECIFICALLY, not the whole predicate,
 * and `World` says how the two are separated. The numbers §7y recorded are unchanged where
 * they are about the area floor and moved where they are about the world - see
 * AreaAloneStillDoesNotBoundASliverAndTheSecondTermDoes for the retired half of one gate.
 */
public class MinBuildingAreaTests
{
    private static readonly object _lo = new();
    private static readonly Dictionary<bool, Measured> _measured = new();


    private sealed class Measured
    {
        internal int Blocks, Buildings, ShopFronts, BlocksWithABuilding, BlocksWithMoreThanOne;
        internal int Pieces, PiecesRefused, BlocksAllRefused, BlocksNoLandAtAll, Tagged;
        internal int PiecesRefusedForWidth;
        internal List<double> WidthRefusedAreas = new();
        internal double SmallestBuilding = Double.MaxValue;
        internal int BuildingsUnderTheFloor;
        internal List<double> RefusedAreas = new();
        internal List<double> RefusedDowntownness = new();
        internal List<double> BlockDowntownness = new();
        internal List<(double Area, double Side, double HalfWidth)> Thin = new();
        internal double SmallestSurvivorSide;
        internal Vector3 StartPose;
        internal List<double> MixedBlockBuildingAreas = new();
    }


    /**
     * The seventy shipped cities of GenerateClustersOperator's own list, with their blocks
     * traced, their land asked for through QuarterGenerator.BuildableLandOf and their
     * buildings measured as they were designed.
     *
     * Every piece is asked of QuarterGenerator.CanCarryABuilding - the production predicate
     * itself rather than a copy of the threshold - so "which pieces the floor refuses" and
     * "which pieces got a building" are the same rule read twice.
     */
    private static Measured World(bool gradeSeparation)
    {
        lock (_lo)
        {
            if (_measured.TryGetValue(gradeSeparation, out var cached)) return cached;

            var m = new Measured();

            foreach (var city in BlockRingClosureTests.World(gradeSeparation))
            {
                var core = BlockGraph.TwoCoreOf(city.Store);
                var spurs = BlockGraph.SpurCorridorsOf(city.Store, core);
                var structures = city.Store.GetStrokes()
                    .FindAll(s => StrokeKinds.IsStructure(s.Kind));

                if (city.Cluster.IdString.EndsWith("-0"))
                {
                    m.StartPose = PlayerStart.PoseIn(city.Cluster, city.Quarters).V3World;
                }

                foreach (var q in city.Quarters.GetQuarters())
                {
                    ++m.Blocks;

                    var estate = q.GetEstates()[0];
                    var centre = q.GetCenterPoint();
                    double downtownness = city.Cluster.GetAttributeIntensity(
                        city.Cluster.Pos + new Vector3(centre.X, 0f, centre.Y),
                        ClusterDesc.LocationAttributes.Downtown);
                    m.BlockDowntownness.Add(downtownness);

                    var land = QuarterGenerator.BuildableLandOf(q, estate, structures, spurs);
                    var pieces = (null == land ? new List<List<IntPoint>>() : land)
                        .Where(p => p.Count > 0).ToList();

                    /*
                     * ⚠️ THE AREA TERM, NOT THE WHOLE PREDICATE, since §7z gave
                     * CanCarryABuilding a second term. Everything this file counts is
                     * about the ten square metre floor, so a piece that is over the floor
                     * and refused for being too NARROW is not one of its refusals - it is
                     * MinBuildingWidthTests', and it is counted here only to say how many
                     * there are. The predicate is still read rather than copied: a piece
                     * is refused for area exactly when the predicate refuses it and its
                     * area is under the floor.
                     */
                    int refused = 0, refusedAtAll = 0;
                    foreach (var piece in pieces)
                    {
                        ++m.Pieces;
                        if (QuarterGenerator.CanCarryABuilding(piece)) continue;

                        ++refusedAtAll;
                        double area = Math.Abs(Clipper.Area(piece)) / 100.0;
                        if (area >= MetaGen.MinBuildingArea)
                        {
                            ++m.PiecesRefusedForWidth;
                            m.WidthRefusedAreas.Add(area);
                            continue;
                        }

                        ++refused;
                        ++m.PiecesRefused;
                        m.RefusedAreas.Add(area);
                        m.RefusedDowntownness.Add(downtownness);
                    }

                    if (0 == pieces.Count) ++m.BlocksNoLandAtAll;
                    if (refusedAtAll == pieces.Count) ++m.BlocksAllRefused;
                    if (q.GetDebugString().Contains("estateTooSmall")) ++m.Tagged;

                    var buildings = q.GetEstates().SelectMany(e => e.GetBuildings()).ToList();
                    if (buildings.Count > 0) ++m.BlocksWithABuilding;
                    if (buildings.Count > 1) ++m.BlocksWithMoreThanOne;
                    m.Buildings += buildings.Count;

                    /*
                     * The three blocks of the world that hold a refused piece AND a piece
                     * over the floor - the only blocks where refusing before the design
                     * draw can move anything (§7y.5).
                     */
                    if (refused > 0 && refused < pieces.Count)
                    {
                        m.MixedBlockBuildingAreas.AddRange(
                            buildings.Select(b => _areaOf(b.GetPoints())));
                    }

                    foreach (var b in buildings)
                    {
                        var pts = b.GetPoints();
                        double a = _areaOf(pts), side = _minSideOf(pts);

                        m.ShopFronts += b.GetShopFronts().Count;
                        if (a < MetaGen.MinBuildingArea) ++m.BuildingsUnderTheFloor;
                        if (a < m.SmallestBuilding)
                        {
                            m.SmallestBuilding = a;
                            m.SmallestSurvivorSide = side;
                        }

                        /*
                         * ⚠️ "Under two metres thick" is ONE inset rather than a search:
                         * the footprint offset inward by a metre either survives or it
                         * does not, so every building in the world is asked exactly, with
                         * no prefilter to be wrong about. Only the ones that fail it - a
                         * dozen - then get the binary search that says how thin they are.
                         */
                        if (!_survivesInsetBy(pts, 1.0)) m.Thin.Add((a, side, _halfWidthOf(pts)));
                    }
                }
            }

            _measured[gradeSeparation] = m;
            return m;
        }
    }


    /*
     * ============================================== the rule ==========================
     */

    /**
     * ⚠️ NOT ONE BUILDING IN THE SHIPPED WORLD STANDS ON LESS THAN TEN SQUARE METRES, and
     * the smallest one there is stands on 11.92 m² flag off and 10.05 m² flag on.
     *
     * The zero is the whole of the owner's decision. The smallest survivor is recorded
     * beside it because a zero on its own would be satisfied by a rule that removed every
     * building in the world, and because it says the floor is a floor and not a filter with
     * a margin: a piece a hair over ten square metres still gets its building.
     *
     * The control is that almost nothing was removed to get there - 27 387 and 25 860
     * buildings stand, against 27 403 and 26 054 before (§7y.3).
     */
    [Theory]
    //                 buildings  smallest lo/hi (m²)
    [InlineData(false, 27387, 11.9, 12.0)]
    [InlineData(true, 25846, 10.0, 10.1)]
    public void NoBuildingStandsOnLessThanTenSquareMetres(
        bool gradeSeparation, int buildings, double smallLo, double smallHi)
    {
        var m = World(gradeSeparation);

        Assert.Equal(0, m.BuildingsUnderTheFloor);
        Assert.Equal(buildings, m.Buildings);
        Assert.InRange(m.SmallestBuilding, smallLo, smallHi);

        /*
         * ...and the floor is the one in MetaGen rather than a number this file agrees
         * with by coincidence: nothing stands below it, and flag on the smallest thing
         * standing is within a twentieth of a square metre of it.
         */
        Assert.True(m.SmallestBuilding >= MetaGen.MinBuildingArea,
            $"the smallest building is {m.SmallestBuilding:F4} m²");
    }


    /**
     * ⚠️ WHAT GOES, AND IT IS THE POPULATION §7x COUNTED: 20 pieces of land flag off and
     * 310 flag on, of which 16 and 195 carried a building.
     *
     * The removed buildings are §7x.8's own table read down its "under 10 m²" row - 16
     * blocks flag off and 194 flag on, one of which carried two of them. What is left after
     * the refusal is 27 387 buildings against 27 403 and 25 860 against 26 054, i.e. -16
     * and -194: the flag-on delta is one SHORT of the 195 removed, and that one is
     * AGoodPieceOfLandGetsTheDrawTheMatchboxUsedToTake below.
     *
     * The refused land is small even against the ten square metres it is refused by: a
     * median 4.00 m² flag off and 2.65 m² flag on, the smallest 0.03 and 0.005 m². Nothing
     * near the boundary is being thrown away in bulk.
     *
     * ⚠️ AND THE BLOCK THAT LOSES ITS LAST BUILDING IS THE COMMON CASE, which is the number
     * the knock-on hangs off: 27 374 blocks carry a building flag off against 27 390, and
     * 25 025 against 25 216 - 16 and 191 blocks that had one and now have none. Three
     * flag-on blocks keep theirs because they also hold a piece over the floor.
     */
    [Theory]
    //                 pieces  refused  blocks with a building  refused-area p50 lo/hi
    [InlineData(false, 40914, 20, 27374, 3.0, 5.0)]
    [InlineData(true, 38797, 310, 25012, 2.0, 3.5)]
    public void WhatTheFloorTakesAwayIsTheMatchboxPopulation(
        bool gradeSeparation, int pieces, int refused, int withABuilding,
        double p50Lo, double p50Hi)
    {
        var m = World(gradeSeparation);

        Assert.Equal(pieces, m.Pieces);
        Assert.Equal(refused, m.PiecesRefused);
        Assert.Equal(withABuilding, m.BlocksWithABuilding);

        /*
         * Every refused piece is under the floor by construction; what is worth recording
         * is how FAR under it they are, because a distribution crowded against 10 m² would
         * mean the threshold was cutting into ordinary land.
         */
        Assert.Equal(refused, m.RefusedAreas.Count(a => a < MetaGen.MinBuildingArea));
        Assert.InRange(_median(m.RefusedAreas), p50Lo, p50Hi);
        Assert.True(m.RefusedAreas.Max() < MetaGen.MinBuildingArea,
            $"a refused piece of {m.RefusedAreas.Max():F3} m² is not under the floor");
    }


    /**
     * ⚠️ THE FLOOR BINDS MORE DOWNTOWN, NOT LESS - the one thing this round's brief had the
     * wrong way round, and it is measured rather than argued.
     *
     * The owner's sentence ends "...but not really in city centers", and the reading built
     * on it was that a downtown block would be big enough that the floor rarely binds. The
     * opposite is true. Flag on the refusal fires on 1.01 % of blocks above downtownness
     * 0.7 against 0.39 % below 0.3; flag off it is downtown ONLY - 18 of 20 cases above
     * 0.7, and not one below 0.5 anywhere in the world.
     *
     * The mechanism is §7x's, unchanged: `Quarter.SidewalkWidth` is 6 m only above
     * downtownness 0.7 and the heaviest roads are downtown, so the block outline that is
     * eaten down to a scrap is a downtown outline. The floor is flat everywhere anyway -
     * scaling it with downtownness would be a second rule and a second constant, and at
     * about 1 % of blocks either way there is nothing for one to fix.
     */
    [Theory]
    //                 refused below 0.5   refused above 0.7   worst band rate lo/hi (%)
    [InlineData(false, 0, 18, 0.10, 0.15)]
    [InlineData(true, 64, 132, 0.95, 1.10)]
    public void TheFloorBindsMoreDowntownAndNotLess(
        bool gradeSeparation, int belowHalf, int aboveSeven, double rateLo, double rateHi)
    {
        var m = World(gradeSeparation);

        Assert.Equal(belowHalf, m.RefusedDowntownness.Count(d => d < 0.5));
        Assert.Equal(aboveSeven, m.RefusedDowntownness.Count(d => d >= 0.7));

        double topRate = 100.0 * m.RefusedDowntownness.Count(d => d >= 0.7)
                         / m.BlockDowntownness.Count(d => d >= 0.7);
        double bottomRate = 100.0 * m.RefusedDowntownness.Count(d => d < 0.3)
                            / m.BlockDowntownness.Count(d => d < 0.3);

        Assert.InRange(topRate, rateLo, rateHi);

        /*
         * The direction itself, which is the finding: downtown is where this fires.
         */
        Assert.True(topRate > bottomRate,
            $"downtown rate {topRate:F2} % is not above the outlying {bottomRate:F2} %");
    }


    /**
     * ⚠️ REFUSING BEFORE THE DESIGN DRAW IS NOT FREE, AND ON ONE BLOCK OF THE WORLD A 1450 m²
     * BUILDING APPEARS WHERE A 3.5 m² ONE STOOD.
     *
     * This is the consequence of unifying with the `0 == polygon.Count` gate rather than
     * adding a second one inside `_designBuilding`: the refusal happens BEFORE the 30 %
     * "do not build everywhere" draw, so a refused piece no longer consumes that draw and
     * every later piece of the same block sees the sequence the piece in front of it used
     * to see.
     *
     * It can only reach a block holding both a refused piece and a piece over the floor,
     * and the whole world has THREE of them, all flag on:
     *
     *      mydear-474  (333.4,-205.0)  [1.32, 1.09, 132.60] -> [132.60]
     *      mydear-580  ( -86.0, 174.0) [3.50]               -> [1450.10]
     *      mydear-590  ( 565.3,1222.3) [9795.16, 1.20]      -> [9795.16]
     *
     * The middle one is the finding: that block's 1450 m² piece had lost its own draw and
     * its 3.5 m² neighbour had won one, so the block carried a matchbox and nothing else.
     * With the matchbox refused before it draws, the good piece takes the draw the matchbox
     * used to take, wins it, and the block gets the building it should always have had.
     * That is why the flag-on total falls by 194 rather than by the 195 removed.
     *
     * Measured against the tree at 91244e45 by building both worlds and diffing them block
     * by block, not inferred from the totals.
     */
    [Fact]
    public void AGoodPieceOfLandGetsTheDrawTheMatchboxUsedToTake()
    {
        var off = World(false);
        var on = World(true);

        /*
         * Flag off there is no such block at all, so the whole effect is flag-on: every
         * refused piece there is the only piece its block has.
         */
        Assert.Empty(off.MixedBlockBuildingAreas);

        var areas = on.MixedBlockBuildingAreas.OrderBy(a => a).ToList();

        Assert.Equal(3, areas.Count);
        Assert.InRange(areas[0], 132.0, 133.0);
        Assert.InRange(areas[1], 1450.0, 1451.0);
        Assert.InRange(areas[2], 9795.0, 9796.0);

        /*
         * ...and the 1450 m² one is the building that was NOT there before, which is what
         * the middle value means. A count of three would be satisfied by the two survivors
         * plus anything.
         */
        Assert.Equal(0, areas.Count(a => a < MetaGen.MinBuildingArea));
    }


    /**
     * ⚠️ THE GENERATOR'S OWN RECORD FOLLOWS THE PREDICATE, because two records of one rule
     * is how the two would drift.
     *
     * `estateTooSmall` used to mean "this block has no buildable land at all" - §7x's 9 and
     * 522 traffic islands - and now means "not one piece of this block's land can carry a
     * building", which is the same sentence with the same predicate in it. The old set is
     * still inside the new one and is still exactly the blocks with no land, so the number
     * §7x pinned survives as a subset rather than being overwritten.
     *
     * ⚠️ SUPERSEDED IN ITS FLAG-ON COUNT BY §7z, old text 828: the predicate gained a width
     * term, so the tag gained the eighteen blocks whose only land is under two metres
     * across. That it follows the predicate rather than one of its terms is the property,
     * and it is what makes this number move at all.
     */
    [Theory]
    //                 tagged (was 828 flag on)  of which no land at all (§7x's own count)
    [InlineData(false, 29, 9)]
    [InlineData(true, 846, 522)]
    public void TheGeneratorRecordsTheRefusalWithTheSamePredicateItMakesItWith(
        bool gradeSeparation, int tagged, int noLandAtAll)
    {
        var m = World(gradeSeparation);

        Assert.Equal(tagged, m.Tagged);
        Assert.Equal(tagged, m.BlocksAllRefused);
        Assert.Equal(noLandAtAll, m.BlocksNoLandAtAll);
    }


    /*
     * ============================================== the knock-on =====================
     */

    /**
     * ⚠️ A REMOVED BUILDING TAKES ITS SHOPFRONTS WITH IT, AND IT IS 2 AND 13 OF HALF A
     * MILLION.
     *
     * A shopfront is 5 m of a building's own perimeter (`_addShops`), so a matchbox rarely
     * has room for one at all: the 16 buildings removed flag off carried 2 between them and
     * the 195 removed flag on carried 13. Everything downstream of a shopfront - the shop
     * window, the shop POI, the TALE shop door and the shop the nogame operator puts in it -
     * goes with them, and 13 of 527 807 is what that costs.
     *
     * ⚠️ AND NOTHING DOWNSTREAM ASSUMES A BLOCK WITH AN ESTATE HAS A BUILDING, which is the
     * question worth asking rather than this count. Every consumer was searched (§7y.7) and
     * every one of the six either skips a building-less estate, retries past it, or is
     * looking for one on purpose. What makes that safe is not the search, it is the number
     * asserted here: a building-less block is the ORDINARY case and always was - 13 492 and
     * 11 871 of them before this change, because `_designBuilding` refuses 30 % of blocks
     * on a random draw - so this adds 16 and 191 to a population of thousands and reaches
     * no branch that was not already reached on every start.
     *
     * ⚠️ SUPERSEDED IN ITS FLAG-ON COLUMNS BY §7z, old text 527 794 / 12 584 / 791: its two
     * metre width floor takes 14 more buildings and, because a ribbon is nearly all
     * perimeter, **271** more shopfronts - twenty times what these 195 matchboxes cost. See
     * MinBuildingWidthTests.ARemovedRibbonTakesAnExpensiveNumberOfShopfronts.
     */
    [Theory]
    //                 shopfronts  building-less blocks  blocks with more than one building
    [InlineData(false, 572402, 13517, 13)]
    [InlineData(true, 527523, 12597, 790)]
    public void ARemovedBuildingTakesItsShopfrontsAndNothingElse(
        bool gradeSeparation, int shopFronts, int buildingLess, int several)
    {
        var m = World(gradeSeparation);

        Assert.Equal(shopFronts, m.ShopFronts);
        Assert.Equal(several, m.BlocksWithMoreThanOne);

        /*
         * ⚠️ The population that says this is not a new branch anywhere: a block with no
         * building is thousands of blocks, and the ones this change makes are a per-cent
         * of them.
         */
        Assert.Equal(buildingLess, m.Blocks - m.BlocksWithABuilding);
        Assert.True(m.Blocks - m.BlocksWithABuilding > 100 * m.BlocksAllRefused / 10,
            $"{m.Blocks - m.BlocksWithABuilding} building-less blocks is not "
            + $"much more than the {m.BlocksAllRefused} this rule makes");
    }


    /**
     * ⚠️ AND THE NEW GAME STILL STARTS WHERE IT STARTED, IN BOTH FLAG STATES.
     *
     * `PlayerStart.PoseIn` puts a new game on the FIRST estate of the start city that
     * carries no building, so a rule that takes buildings off blocks can move the player -
     * and §7x already recorded that nothing protects against starting on a traffic island.
     * `cluster-clusters-mydear-0` has no piece of land under the floor in either flag state,
     * so its first building-less block is the one it always was: index 13 flag off and 26
     * flag on, unmoved. The floats are the ones the tree produced at 91244e45.
     */
    [Theory]
    [InlineData(false, 92.0651f, 137.76129f, 209.378f)]
    [InlineData(true, -337.45218f, 137.76129f, -336.24365f)]
    public void TheNewGameStartsWhereItStarted(bool gradeSeparation, float x, float y, float z)
    {
        var m = World(gradeSeparation);

        Assert.Equal(x, m.StartPose.X, 3);
        Assert.Equal(y, m.StartPose.Y, 3);
        Assert.Equal(z, m.StartPose.Z, 3);
    }


    /*
     * ============================================== the shape question ================
     */

    /**
     * ⚠️ AREA STILL DOES NOT BOUND A SLIVER - THE SECOND TERM DOES, AND THIS IS WHERE THE
     * TWO ARE SEPARATED.
     *
     * §7y recorded fourteen flag-on buildings under two metres thick, every one of them
     * clearing this floor comfortably, and left the decision to the owner. §7z made it:
     * `MetaGen.MinBuildingWidth` is the second term of the same predicate, and the count is
     * now zero. What this gate keeps is the half that is about AREA, and it is the half
     * that says the two rules are independent rather than one dressed as the other: the 21
     * pieces the width term refuses have 12.75 to 93.97 m² of floor between them, so no
     * area threshold a city would survive could have reached any of them.
     *
     * ⚠️ SUPERSEDED, and its old text, which asserted the defect:
     *
     *      [InlineData(false, 0, 0.0, 0.0)]
     *      [InlineData(true, 14, 0.30, 0.35)]
     *      Assert.Equal(thin, m.Thin.Count);
     *      Assert.InRange(2.0 * m.Thin.Min(t => t.HalfWidth), thinnestLo, thinnestHi);
     *
     * The successor is MinBuildingWidthTests.NoBuildingIsUnderTwoMetresAcross, which carries
     * the zero and the thinnest survivor there is.
     */
    [Theory]
    //                 pieces the WIDTH term refuses  smallest of them lo/hi (m²)
    [InlineData(false, 0, 0.0, 0.0)]
    [InlineData(true, 21, 12.7, 12.8)]
    public void AreaAloneStillDoesNotBoundASliverAndTheSecondTermDoes(
        bool gradeSeparation, int refusedForWidth, double smallestLo, double smallestHi)
    {
        var m = World(gradeSeparation);

        Assert.Empty(m.Thin);
        Assert.Equal(refusedForWidth, m.PiecesRefusedForWidth);

        if (0 == refusedForWidth) return;

        /*
         * ...and the whole of that population is over the area floor, which is what makes
         * it a second rule rather than a tighter first one.
         */
        Assert.InRange(m.WidthRefusedAreas.Min(), smallestLo, smallestHi);
        Assert.Equal(0, m.WidthRefusedAreas.Count(a => a < MetaGen.MinBuildingArea));
    }


    /*
     * ============================================== the predicate itself ==============
     */

    /**
     * ⚠️ THE BOUNDARY IS EXACT AND IT IS INCLUSIVE, driven against the production predicate
     * with polygons whose Clipper area is exactly the floor and exactly one hundredth of a
     * square metre under it.
     *
     * Clipper works in tenth metres and integers, so a rectangle 4 m by 2.5 m has an area of
     * exactly 1000 in its units and the comparison has no rounding in it anywhere. No
     * amount of real data can pin this: the world's smallest survivor is 10.05 m² and its
     * largest refusal 9.98 m², so ">= floor" and "> floor" agree on every block there is.
     *
     * ⚠️ EVERY RECTANGLE HERE IS AT LEAST 2.1 m ACROSS, deliberately, so that the AREA term
     * is the only one deciding. Since §7z the predicate has a width term too, and the
     * fixtures this test used to use - 10 m by 1 m, and 99.9 m by 0.1 m - are refused by
     * that one whatever their area is. The width term's own boundary is
     * MinBuildingWidthTests.TheWidthFloorIsExclusiveAtTheBoundaryAndTheGridIsADecimetre,
     * and it is EXCLUSIVE where this one is inclusive.
     */
    [Fact]
    public void TheFloorIsInclusiveAndTheBoundaryIsExact()
    {
        Assert.Equal(10f, MetaGen.MinBuildingArea);

        Assert.True(QuarterGenerator.CanCarryABuilding(_rect(40, 25)),
            "exactly ten square metres has to be enough");
        Assert.False(QuarterGenerator.CanCarryABuilding(_rect(40, 24)),
            "nine point six square metres has to be refused");

        /*
         * ...and a hundredth of a square metre under the floor, which is one unit of
         * Clipper's own grid squared and the finest a difference here can be.
         */
        var justUnder = _rect(37, 27);
        Assert.InRange(Math.Abs(Clipper.Area(justUnder)) / 100.0, 9.98, 10.0);
        Assert.True(BlockGraph.SurvivesInsetBy(justUnder, 0.5f * MetaGen.MinBuildingWidth),
            "the fixture has to be wide enough for the width term to accept it");
        Assert.False(QuarterGenerator.CanCarryABuilding(justUnder));
    }


    /**
     * ⚠️ AND THE OLD RULE IS THE NEW RULE AT ZERO, which is why there is one predicate and
     * not two.
     *
     * The loop used to ask `0 == polygon.Count`. Every polygon that test refused - none,
     * one or two points - this one refuses too, by having no area rather than by being a
     * special case in front of it. The degenerate cases are driven directly because the
     * shipped world contains none of them: `BuildableLandOf` returns Clipper contours, and
     * a contour with fewer than three points is not something ClipperOffset emits.
     */
    [Fact]
    public void ThePredicateSubsumesTheRuleItReplaced()
    {
        Assert.False(QuarterGenerator.CanCarryABuilding(new List<IntPoint>()));
        Assert.False(QuarterGenerator.CanCarryABuilding(
            new List<IntPoint> { new(0, 0) }));
        Assert.False(QuarterGenerator.CanCarryABuilding(
            new List<IntPoint> { new(0, 0), new(1000, 1000) }));

        /*
         * A polygon with plenty of points and no area is refused for the same reason,
         * which the old rule accepted and built a house on.
         */
        Assert.False(QuarterGenerator.CanCarryABuilding(
            new List<IntPoint> { new(0, 0), new(5000, 0), new(10000, 0), new(5000, 0) }));

        /*
         * ⚠️ Winding is not a licence to build: a piece wound the other way has the same
         * ground under it. Clipper's Area is signed, and no amount of real data can catch
         * a missing Math.Abs - every piece BuildableLandOf returns is wound one way.
         */
        var cw = _rect(200, 200);
        var ccw = new List<IntPoint>(cw);
        ccw.Reverse();

        Assert.True(Clipper.Area(cw) * Clipper.Area(ccw) < 0.0, "the fixture is not mirrored");
        Assert.True(QuarterGenerator.CanCarryABuilding(cw));
        Assert.True(QuarterGenerator.CanCarryABuilding(ccw));
    }


    /*
     * ============================================== plumbing ==========================
     */

    /** A rectangle in Clipper's own tenth-metre units. */
    private static List<IntPoint> _rect(int w, int h)
        => new() { new(0, 0), new(w, 0), new(w, h), new(0, h) };


    private static double _areaOf(IList<Vector3> v)
    {
        double a = 0.0;
        for (int i = 0; i < v.Count; ++i)
        {
            var p = v[i];
            var q = v[(i + 1) % v.Count];
            a += (double)p.X * q.Z - (double)q.X * p.Z;
        }

        return Math.Abs(a) / 2.0;
    }


    private static double _minSideOf(IList<Vector3> v)
    {
        double m = Double.MaxValue;
        for (int i = 0; i < v.Count; ++i) m = Math.Min(m, (v[(i + 1) % v.Count] - v[i]).Length());
        return v.Count > 0 ? m : 0.0;
    }


    private static double _perimeterOf(IList<Vector3> v)
    {
        double m = 0.0;
        for (int i = 0; i < v.Count; ++i) m += (v[(i + 1) % v.Count] - v[i]).Length();
        return m;
    }


    /**
     * Half the polygon's own narrowest width, in metres, and whether it survives one
     * particular inset.
     *
     * ⚠️ ONE COPY EACH, and since §7z both are production expressions: the width half of
     * `CanCarryABuilding` asks exactly "does this piece survive an inset of
     * MetaGen.MinBuildingWidth / 2", so `BlockGraph.SurvivesInsetBy` is the predicate and
     * MinBuildingWidthTests.HalfWidthOf is the search over it. This file and EmptyBlockTests
     * used to carry a copy each.
     */
    private static double _halfWidthOf(IList<Vector3> outline)
        => MinBuildingWidthTests.HalfWidthOf(outline);


    private static bool _survivesInsetBy(IList<Vector3> outline, double d)
        => BlockGraph.SurvivesInsetBy(MinBuildingWidthTests.AsPath(outline), (float)d);


    private static double _median(IEnumerable<double> values)
    {
        var v = values.OrderBy(x => x).ToList();
        return 0 == v.Count ? Double.NaN : v[v.Count / 2];
    }
}
