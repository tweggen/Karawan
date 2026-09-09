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
 * ⚠️ A PIECE OF LAND UNDER TWO METRES ACROSS CARRIES NO BUILDING (§7z).
 *
 * §7y shipped the owner's ten square metre floor and reported, without acting on it, that
 * area does not answer the other half of "what is a building": the thinnest building in the
 * shipped world was 0.32 m thick with a 95.7 m perimeter round 17.2 m² of floor, seventy per
 * cent clear of the area floor, and one of the fourteen ribbons had 83 m² of it. No area
 * threshold a city would survive reaches that shape. The owner asked for the second rule on
 * 2026-09-09 and left the number to be derived.
 *
 * ⚠️ THE NUMBER IS DERIVED AND THE DERIVATION HOLDS, which was checked before it was
 * adopted (see TheSliverPopulationEndsWhereTheFloorIs). `_designBuilding` has held a
 * building whose shortest side is 2.0 m or less to a single storey since long before this
 * work: the tree already says that below two metres a thing is barely a building, and this
 * is the same magnitude in the same domain deciding existence rather than height. Measured
 * over the seventy shipped cities, that is also where the sliver population ends.
 *
 * ⚠️ AND IT IS A FLOOR RATHER THAN THE NATURAL BREAK, which is said out loud because the
 * two are half a metre apart and the difference is the honest cost of this round. The
 * distribution's own cliff is at about 2.55 m; between 2.0 and 2.5 four more ribbons stand,
 * the worst 140 m² of floor 2.17 m thick round a 206 m perimeter, and it survives. What is
 * immediately above them is the run of perfectly good compact triangles sitting exactly on
 * MetaGen.MinBuildingArea - an equilateral triangle of 10 m² is 2.77 m thick - so a floor
 * much over 2.5 would start refusing the smallest building the owner's own area rule
 * permits. Two metres is the derived number and is safely below that.
 *
 * ⚠️ THE INSTRUMENT IS §7x's HALF-WIDTH AND NOT `minHouseSide`, which §7y measured the
 * reason for: a polygon's shortest SIDE is short at a mitred corner as often as on a sliver,
 * so a minHouseSide rule would take 1442 flag-off and 921 flag-on ordinary fat buildings
 * against this rule's 0 and 14. `BlockGraph.SurvivesInsetBy` is the one expression - "does
 * this outline survive being inset by this much all round" - and §7x's half-width is that
 * predicate binary searched. ⚠️ It is the ESTATE'S OWN MITRED inset rather than an exact disc
 * erosion, which the two agree on for a convex polygon and not at a reflex corner: see
 * TheInsetIsTheEstatesOwnMitreAndTheJoinTypeDecides, which exists because swapping the join
 * for a round one passes over all 79 711 real pieces.
 *
 * ⚠️ THE FLAT CITY DOES NOT MOVE BY A SINGLE BUILDING. Not one piece of land in any of the
 * seventy flag-off cities is under two metres across, so this rule is invisible to unlimited
 * real data on that ground and is driven by fixture there (§9's lesson again). Grade
 * separation is the shipped default, and the flag-on world loses 14 buildings of 25 860.
 */
public class MinBuildingWidthTests
{
    private static readonly object _lo = new();
    private static readonly Dictionary<bool, Measured> _measured = new();


    internal sealed class Measured
    {
        internal int Blocks, Buildings, ShopFronts, BlocksWithABuilding, BlocksWithMoreThanOne;
        internal int Pieces, RefusedForArea, RefusedForWidth, Tagged;
        internal double ThinnestBuilding = Double.MaxValue;
        internal int BuildingsUnderTheWidthFloor;
        internal int BuildingsWithASideUnderTheWidthFloor;
        internal List<(double Thickness, double Area, double Perimeter)> RefusedWide = new();
        internal List<double> SurvivingThickness = new();
        internal List<double> MixedBlockBuildingAreas = new();
        internal List<double> MixedBlockLandAreas = new();
        internal int MixedBlocks, MixedBlocksWithNoBuilding;
        internal int PredicateDisagreements;
    }


    /**
     * The seventy shipped cities, with every piece of buildable land asked of the production
     * predicate and every building measured across rather than along.
     *
     * ⚠️ THE BUILDING IS MEASURED BY THE BINARY SEARCH AND NOT BY THE PREDICATE, so that
     * "no building is under the floor" is a measurement of the artefact rather than a
     * restatement of the rule that made it. The two are then compared on every piece of the
     * world, which is what says the search and the predicate are the same question
     * (PredicateDisagreements).
     *
     * Only the buildings that fail one inset of half the floor get the search - three
     * hundred of fifty thousand - because a fat building's thickness is not interesting and
     * twenty-two ClipperOffsets each is not free.
     */
    private static Measured World(bool gradeSeparation)
    {
        lock (_lo)
        {
            if (_measured.TryGetValue(gradeSeparation, out var cached)) return cached;

            var m = new Measured();
            float radius = 0.5f * MetaGen.MinBuildingWidth;

            foreach (var city in BlockRingClosureTests.World(gradeSeparation))
            {
                var core = BlockGraph.TwoCoreOf(city.Store);
                var spurs = BlockGraph.SpurCorridorsOf(city.Store, core);
                var structures = city.Store.GetStrokes()
                    .FindAll(s => StrokeKinds.IsStructure(s.Kind));

                foreach (var q in city.Quarters.GetQuarters())
                {
                    ++m.Blocks;

                    var estate = q.GetEstates()[0];
                    var land = QuarterGenerator.BuildableLandOf(q, estate, structures, spurs);
                    var pieces = (null == land ? new List<List<IntPoint>>() : land)
                        .Where(p => p.Count > 0).ToList();

                    int refusedHere = 0;
                    foreach (var piece in pieces)
                    {
                        ++m.Pieces;

                        bool overTheArea = Math.Abs(Clipper.Area(piece))
                                           >= 100.0 * MetaGen.MinBuildingArea;
                        bool wideEnough = BlockGraph.SurvivesInsetBy(piece, radius);

                        /*
                         * The production predicate is the conjunction of the two terms and
                         * nothing else - asserted here on every piece of the world rather
                         * than read off the source.
                         */
                        if (QuarterGenerator.CanCarryABuilding(piece)
                            != (overTheArea && wideEnough))
                        {
                            ++m.PredicateDisagreements;
                        }

                        if (overTheArea && wideEnough) continue;

                        ++refusedHere;
                        if (!overTheArea)
                        {
                            ++m.RefusedForArea;
                            continue;
                        }

                        /*
                         * Over the area floor and refused anyway: this rule's own
                         * population, and the whole of it.
                         */
                        ++m.RefusedForWidth;
                        var v = _asOutline(piece);
                        m.RefusedWide.Add((2.0 * HalfWidthOf(piece), _areaOf(v), _perimeterOf(v)));
                    }

                    if (q.GetDebugString().Contains("estateTooSmall")) ++m.Tagged;

                    var buildings = q.GetEstates().SelectMany(e => e.GetBuildings()).ToList();
                    if (buildings.Count > 0) ++m.BlocksWithABuilding;
                    if (buildings.Count > 1) ++m.BlocksWithMoreThanOne;
                    m.Buildings += buildings.Count;

                    /*
                     * A block holding both a refused piece and an accepted one is where the
                     * design draw shifts - §7y.5's finding, and §7z.5's.
                     */
                    if (refusedHere > 0 && refusedHere < pieces.Count)
                    {
                        ++m.MixedBlocks;
                        if (0 == buildings.Count) ++m.MixedBlocksWithNoBuilding;
                        m.MixedBlockBuildingAreas.AddRange(
                            buildings.Select(b => _areaOf(b.GetPoints())));
                        m.MixedBlockLandAreas.AddRange(pieces
                            .Where(p => QuarterGenerator.CanCarryABuilding(p))
                            .Select(p => Math.Abs(Clipper.Area(p)) / 100.0));
                    }

                    foreach (var b in buildings)
                    {
                        var pts = b.GetPoints();
                        m.ShopFronts += b.GetShopFronts().Count;
                        if (_minSideOf(pts) <= MetaGen.MinBuildingWidth)
                        {
                            ++m.BuildingsWithASideUnderTheWidthFloor;
                        }

                        /*
                         * One inset decides whether this building is interesting at all;
                         * only the few that fail it are searched.
                         */
                        if (BlockGraph.SurvivesInsetBy(AsPath(pts), 1.5f)) continue;

                        double thickness = 2.0 * HalfWidthOf(AsPath(pts));
                        m.SurvivingThickness.Add(thickness);
                        if (thickness < MetaGen.MinBuildingWidth) ++m.BuildingsUnderTheWidthFloor;
                        if (thickness < m.ThinnestBuilding) m.ThinnestBuilding = thickness;
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
     * ⚠️ NOT ONE BUILDING IN THE SHIPPED WORLD IS UNDER TWO METRES ACROSS, and the thinnest
     * one there is stands 2.90 m thick flag off and 2.17 m flag on.
     *
     * The zero is the decision. The thinnest survivor is recorded beside it for §7y's own
     * reason - a zero on its own would be satisfied by a rule that removed every building in
     * the world - and for a second one this time: 2.17 m is not 2.00 m, so the number says
     * out loud that four ribbons stand between the floor and the natural break and this rule
     * did not pretend to remove them.
     *
     * The thickness is measured by binary search over BlockGraph.SurvivesInsetBy rather than
     * asked of the predicate, so it is a statement about the buildings that exist.
     */
    [Theory]
    //                 buildings  thinnest lo/hi (m)
    [InlineData(false, 27387, 2.89, 2.91)]
    [InlineData(true, 25846, 2.16, 2.18)]
    public void NoBuildingIsUnderTwoMetresAcross(
        bool gradeSeparation, int buildings, double thinLo, double thinHi)
    {
        var m = World(gradeSeparation);

        Assert.Equal(0, m.BuildingsUnderTheWidthFloor);
        Assert.Equal(buildings, m.Buildings);
        Assert.InRange(m.ThinnestBuilding, thinLo, thinHi);

        /*
         * ...and the floor is the one in MetaGen rather than a number this file agrees with
         * by coincidence.
         */
        Assert.True(m.ThinnestBuilding >= MetaGen.MinBuildingWidth,
            $"the thinnest building is {m.ThinnestBuilding:F4} m across");
    }


    /**
     * ⚠️ WHAT GOES: 21 pieces of land flag on and NOT ONE flag off, and every one of them is
     * a ribbon that the area floor cannot see.
     *
     * §7y.6 predicted the order of this - it counted 14 buildings under two metres thick -
     * and the piece count is 21 because seven of those pieces had lost their own 30 % design
     * draw and carried nothing. That is the number to check a rule like this against: a
     * width floor meant to catch fourteen ribbons that removed hundreds of buildings would
     * not be the rule that was asked for.
     *
     * The shapes are the argument for the instrument. The refused pieces run 0.32 m to
     * 1.99 m thick over 12.75 m² to 93.97 m² of floor, with perimeters of 27.8 m to 172 m -
     * so the largest of them has NINE TIMES the owner's area floor and would survive any
     * area threshold a city would survive.
     */
    [Theory]
    //                 pieces  refused for width  thickest refused lo/hi   largest area lo/hi
    [InlineData(false, 40914, 0, 0.0, 0.0, 0.0, 0.0)]
    [InlineData(true, 38797, 21, 1.98, 2.00, 93.0, 94.5)]
    public void WhatTheWidthFloorTakesAwayIsARibbonPopulation(
        bool gradeSeparation, int pieces, int refused,
        double thickestLo, double thickestHi, double largestLo, double largestHi)
    {
        var m = World(gradeSeparation);

        Assert.Equal(pieces, m.Pieces);
        Assert.Equal(refused, m.RefusedForWidth);
        Assert.Equal(0, m.PredicateDisagreements);

        if (0 == refused) return;

        /*
         * Every refused piece is under the floor by construction; what says something is
         * how far under, and that the largest of them clears the AREA floor many times.
         */
        Assert.Equal(refused, m.RefusedWide.Count(r => r.Thickness < MetaGen.MinBuildingWidth));
        Assert.InRange(m.RefusedWide.Max(r => r.Thickness), thickestLo, thickestHi);
        Assert.InRange(m.RefusedWide.Max(r => r.Area), largestLo, largestHi);
        Assert.Equal(0, m.RefusedWide.Count(r => r.Area < MetaGen.MinBuildingArea));
    }


    /**
     * ⚠️ THE FLAT CITY DOES NOT MOVE AT ALL, which makes this a rule unlimited real data
     * cannot see on that ground.
     *
     * Not one piece of buildable land in any of the seventy flag-off cities is under two
     * metres across, so every count of the flag-off world is byte-for-byte what §7y left:
     * 27 387 buildings, 572 402 shopfronts, 29 blocks tagged estateTooSmall, 13 blocks with
     * more than one building. That was verified block by block against the tree at 88fde8ac,
     * not inferred - all 40 891 blocks carry an identical list of building areas.
     *
     * §9's lesson for the fifth time in this work stream: a rule can be invisible to
     * unlimited real data. What drives it here is the fixtures below, and the invisibility
     * itself is what this asserts.
     */
    [Fact]
    public void TheFlatCityDoesNotMove()
    {
        var m = World(false);

        Assert.Equal(0, m.RefusedForWidth);
        Assert.Empty(m.RefusedWide);

        Assert.Equal(27387, m.Buildings);
        Assert.Equal(572402, m.ShopFronts);
        Assert.Equal(27374, m.BlocksWithABuilding);
        Assert.Equal(13, m.BlocksWithMoreThanOne);
        Assert.Equal(29, m.Tagged);
        Assert.Equal(0, m.MixedBlocks);
    }


    /**
     * ⚠️ AND THE SLIVER POPULATION REALLY DOES END AT THE FLOOR, which is the derivation and
     * is the one thing that had to be measured before the number was adopted.
     *
     * Cumulative count of surviving flag-on buildings below a thickness, after the rule:
     *
     *      < 2.00 m    0        the rule
     *      < 2.25 m    2
     *      < 2.50 m    4        four ribbons stand between the floor and the break
     *      < 2.75 m   23        <- the cliff: compact 10 m² triangles start here
     *      < 3.00 m   53
     *
     * The density multiplies by ten between 2.5 and 2.75 m, and what arrives there is not
     * more ribbons: it is the run of three-cornered buildings sitting exactly on
     * MetaGen.MinBuildingArea, 10.05 to 11.9 m² with 15 m perimeters. An equilateral
     * triangle of 10 m² is 2.77 m thick, so ANY floor much above 2.5 refuses the smallest
     * building the owner's area rule permits - which is why the number is 2 and why moving
     * it upward is not a free tightening.
     */
    [Fact]
    public void TheSliverPopulationEndsWhereTheFloorIs()
    {
        var t = World(true).SurvivingThickness;

        Assert.Equal(0, t.Count(x => x < 2.00));
        Assert.Equal(2, t.Count(x => x < 2.25));
        Assert.Equal(4, t.Count(x => x < 2.50));
        Assert.Equal(23, t.Count(x => x < 2.75));
        Assert.Equal(53, t.Count(x => x < 3.00));

        /*
         * The cliff, as a statement rather than as five numbers: the quarter metre above
         * 2.5 holds several times what the whole half metre below it does.
         */
        int belowTheBreak = t.Count(x => x < 2.50);
        int theQuarterAbove = t.Count(x => x < 2.75) - belowTheBreak;

        Assert.True(theQuarterAbove > 4 * belowTheBreak,
            $"{theQuarterAbove} buildings in [2.50, 2.75) is not a cliff "
            + $"against {belowTheBreak} in [2.00, 2.50)");
    }


    /*
     * ============================================== the two rules =====================
     */

    /**
     * ⚠️ THE TWO TERMS ARE INDEPENDENT AND NEITHER SUBSUMES THE OTHER, driven by fixture in
     * both directions because the shipped world only ever exercises one of them at a time.
     *
     * The disc a two metre width asks for has an area of π ≈ 3.14 m², well under the ten the
     * area floor asks for, so the two orders of implication both fail:
     *
     *      a 1 m x 20 m ribbon has 20 m² and passes the AREA floor and fails the width one;
     *      a 2.2 m square has 4.84 m² and passes the WIDTH floor and fails the area one.
     *
     * Without both cases a single term could be deleted and the survivor would answer for
     * it on some of the data (§7q's symmetric-survivor lesson in its plainest form).
     */
    [Fact]
    public void TheAreaFloorAndTheWidthFloorAreIndependent()
    {
        Assert.Equal(10f, MetaGen.MinBuildingArea);
        Assert.Equal(2f, MetaGen.MinBuildingWidth);

        /*
         * The reason they can be: the largest disc the width rule insists on holds
         * π·(w/2)² square metres, and that is a third of the area floor.
         */
        double disc = Math.PI * 0.25 * MetaGen.MinBuildingWidth * MetaGen.MinBuildingWidth;
        Assert.InRange(disc, 3.0, 3.2);
        Assert.True(disc < MetaGen.MinBuildingArea);

        var ribbon = _rect(200, 10);
        Assert.InRange(Math.Abs(Clipper.Area(ribbon)) / 100.0, 19.9, 20.1);
        Assert.True(Math.Abs(Clipper.Area(ribbon)) >= 100.0 * MetaGen.MinBuildingArea,
            "a 20 m² ribbon has to clear the area floor");
        Assert.False(BlockGraph.SurvivesInsetBy(ribbon, 0.5f * MetaGen.MinBuildingWidth));
        Assert.False(QuarterGenerator.CanCarryABuilding(ribbon));

        var stub = _rect(22, 22);
        Assert.InRange(Math.Abs(Clipper.Area(stub)) / 100.0, 4.8, 4.9);
        Assert.True(BlockGraph.SurvivesInsetBy(stub, 0.5f * MetaGen.MinBuildingWidth),
            "a 2.2 m square has to clear the width floor");
        Assert.False(QuarterGenerator.CanCarryABuilding(stub));

        /*
         * ...and something that clears both, so that neither fixture above is passing for
         * the trivial reason that the predicate refuses everything.
         */
        Assert.True(QuarterGenerator.CanCarryABuilding(_rect(40, 40)));
    }


    /**
     * ⚠️ THE TWO HALVES OF ONE PREDICATE HAVE OPPOSITE BOUNDARY BEHAVIOUR, and it is
     * recorded rather than made uniform.
     *
     * The area floor is INCLUSIVE and exact: a piece of exactly 10.00 m² is built on
     * (§7y's TheFloorIsInclusiveAndTheBoundaryIsExact). The width floor is EXCLUSIVE, and
     * not because of a comparison operator - there is none - but because a strip exactly two
     * metres across inset by one metre encloses nothing, and Clipper returns no contour for
     * a result of no area. A strip 2.1 m across survives.
     *
     * That leaves one decimetre of slack at the boundary, which is Clipper's own grid and
     * the finest anything in this pipeline resolves. No amount of real data can pin either
     * side of it: the thinnest surviving piece in the world is 2.17 m across and the
     * thickest refused one 1.99 m.
     */
    [Fact]
    public void TheWidthFloorIsExclusiveAtTheBoundaryAndTheGridIsADecimetre()
    {
        float radius = 0.5f * MetaGen.MinBuildingWidth;

        Assert.False(BlockGraph.SurvivesInsetBy(_rect(2000, 19), radius));
        Assert.False(BlockGraph.SurvivesInsetBy(_rect(2000, 20), radius));
        Assert.True(BlockGraph.SurvivesInsetBy(_rect(2000, 21), radius));

        /*
         * A polygon that encloses nothing at all is refused by the same expression rather
         * than by a special case in front of it, exactly as the area term refuses it.
         */
        Assert.False(BlockGraph.SurvivesInsetBy(new List<IntPoint>(), radius));
        Assert.False(BlockGraph.SurvivesInsetBy(
            new List<IntPoint> { new(0, 0), new(1000, 1000) }, radius));
    }


    /**
     * ⚠️ THE INSET IS THE ESTATE'S OWN MITRE, AND THE JOIN TYPE DECIDES - which is not what
     * the rule was briefed as and is a finding of the mutation round rather than of reading
     * the function.
     *
     * "Does a disc of radius r fit inside this piece" is a ROUND erosion. A mitred inset is
     * the same thing exactly for a convex polygon and NOT at a reflex corner, where a mitre
     * cuts a point in where a round join would arc. The mitre is the right rule here because
     * it is what `BuildableLandOf` insets the estate with, so that "wide enough for the
     * pavement" and "wide enough for a building" are the same code asked twice - but nothing
     * said so until this fixture, and swapping `jtMiter` for `jtRound` passes the whole suite
     * over all 79 711 pieces of both worlds. No city block distinguishes them.
     *
     * The fixture that does is a DART: a triangle with a notch cut into its base, 10.50 m² of
     * floor, so it clears the area floor and the width term is the only one deciding. Its
     * reflex corner mitres to a point that reaches up into it and meets the inset coming down
     * from the tip; a round join arcs instead and something survives.
     */
    [Fact]
    public void TheInsetIsTheEstatesOwnMitreAndTheJoinTypeDecides()
    {
        var dart = new List<IntPoint> { new(0, 0), new(30, 25), new(60, 0), new(30, 60) };

        Assert.InRange(Math.Abs(Clipper.Area(dart)) / 100.0, 10.4, 10.6);
        Assert.True(Math.Abs(Clipper.Area(dart)) >= 100.0 * MetaGen.MinBuildingArea,
            "the dart has to clear the area floor, or this measures the wrong term");

        float radius = 0.5f * MetaGen.MinBuildingWidth;

        Assert.False(BlockGraph.SurvivesInsetBy(dart, radius));
        Assert.True(_survivesRoundInsetBy(dart, radius),
            "a round join has to keep it, or the fixture does not separate the two");
        Assert.False(QuarterGenerator.CanCarryABuilding(dart));
    }


    /**
     * ⚠️ WINDING IS NOT A LICENCE TO BUILD, AND THE TWO TERMS NEED DIFFERENT CARE FOR IT -
     * which was checked rather than assumed, in both directions.
     *
     * `Clipper.Area` is signed, so the area term carries a `Math.Abs` and §7y drove a
     * mirrored pair through it. `ClipperOffset` fixes the orientation of what it is handed
     * before it offsets, so the width term needs nothing: a mirrored polygon SHRINKS rather
     * than growing, and adding a normalisation would be a provably equivalent line.
     *
     * This is therefore a RECORD rather than a gate - no mutation of our own code can make
     * it fail - and it is worth the six lines, because "a negative delta grows a clockwise
     * path" is exactly the kind of thing that is true of some offsetters and would put a
     * building on every sliver in the world without failing anything else.
     */
    [Fact]
    public void AMirroredPieceIsTheSamePieceOfGround()
    {
        float radius = 0.5f * MetaGen.MinBuildingWidth;

        var wide = _rect(2000, 40);
        var wideMirrored = new List<IntPoint>(wide);
        wideMirrored.Reverse();

        var thin = _rect(2000, 10);
        var thinMirrored = new List<IntPoint>(thin);
        thinMirrored.Reverse();

        Assert.True(Clipper.Area(wide) * Clipper.Area(wideMirrored) < 0.0,
            "the fixture is not mirrored");

        Assert.True(BlockGraph.SurvivesInsetBy(wide, radius));
        Assert.True(BlockGraph.SurvivesInsetBy(wideMirrored, radius));
        Assert.False(BlockGraph.SurvivesInsetBy(thin, radius));
        Assert.False(BlockGraph.SurvivesInsetBy(thinMirrored, radius));
    }


    /*
     * ============================================== the instrument ====================
     */

    /**
     * ⚠️ `minHouseSide` WOULD HAVE BEEN THE WRONG INSTRUMENT BY TWO ORDERS OF MAGNITUDE,
     * which is §7y.6's measurement turned into the gate that keeps anyone from reaching for
     * it.
     *
     * `_designBuilding` already has a shortest-side number and holds a building with one
     * under 2 m to a single storey, so the obvious economy is to refuse on the same
     * quantity. Measured over the world AFTER this rule: 1442 flag-off and 921 flag-on
     * buildings still have a side of 2 m or less, and they are ordinary fat buildings - a
     * mitred corner is short. Refusing on that would have deleted them.
     *
     * The width rule removed 0 and 14. The ratio between the two populations is the whole
     * argument for asking the question as a half-width. (§7y's own 1439 / 725 counted the
     * BLOCKS whose smallest building has a short side; these are the buildings themselves,
     * which is the population a minHouseSide refusal would have reached.)
     */
    [Theory]
    //                 buildings with a short SIDE   removed by the width rule
    [InlineData(false, 1442, 0)]
    [InlineData(true, 921, 14)]
    public void TheShortestSideIsNotTheWidth(
        bool gradeSeparation, int shortSided, int removed)
    {
        var m = World(gradeSeparation);

        Assert.Equal(shortSided, m.BuildingsWithASideUnderTheWidthFloor);

        /*
         * ...and every one of those still stands, which is what says the two quantities
         * are different rather than merely differently named.
         */
        Assert.Equal(0, m.BuildingsUnderTheWidthFloor);

        if (0 == removed)
        {
            Assert.Equal(0, m.RefusedForWidth);
            return;
        }

        Assert.True(shortSided > 40 * removed,
            $"{shortSided} short-sided buildings against {removed} removed is not the "
            + "gap the instrument was chosen for");
    }


    /**
     * ⚠️ THE HALF-WIDTH AND THE PREDICATE ARE ONE EXPRESSION, asserted on the world rather
     * than on a fixture.
     *
     * §7x defined a block's half-width as "the widest uniform inset its outline survives"
     * and measured it with a binary search; this rule asks whether one particular inset
     * survives. They have to be the same question or the number in MetaGen means something
     * different from the number this file reports. Over every piece of buildable land in
     * both worlds, the search agrees with the predicate at the threshold.
     */
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheSearchAndThePredicateAgreeOnEveryPieceOfTheWorld(bool gradeSeparation)
    {
        float radius = 0.5f * MetaGen.MinBuildingWidth;
        int checked_ = 0, disagreements = 0;

        foreach (var city in BlockRingClosureTests.World(gradeSeparation))
        {
            var core = BlockGraph.TwoCoreOf(city.Store);
            var spurs = BlockGraph.SpurCorridorsOf(city.Store, core);
            var structures = city.Store.GetStrokes()
                .FindAll(s => StrokeKinds.IsStructure(s.Kind));

            foreach (var q in city.Quarters.GetQuarters())
            {
                var land = QuarterGenerator.BuildableLandOf(
                    q, q.GetEstates()[0], structures, spurs);
                if (null == land) continue;

                foreach (var piece in land)
                {
                    if (0 == piece.Count) continue;

                    ++checked_;
                    if (BlockGraph.SurvivesInsetBy(piece, radius)
                        != (HalfWidthOf(piece) >= radius))
                    {
                        ++disagreements;
                    }
                }
            }
        }

        Assert.Equal(0, disagreements);
        Assert.True(checked_ > 38000, $"only {checked_} pieces were checked");
    }


    /*
     * ============================================== the knock-on =====================
     */

    /**
     * ⚠️ THE DRAW SHIFT GIVES A BLOCK A BUILDING AND, THIS TIME, TAKES A 28 411 m² ONE AWAY -
     * §7y.5's finding with its sign reversed, which is the thing this round found that
     * nobody asked about.
     *
     * The refusal happens before `_designBuilding`'s 30 % "do not build everywhere" draw, so
     * a refused piece stops consuming that draw and every later piece of its block sees the
     * sequence the piece in front of it used to see. §7y saw that give a block a 1450 m²
     * building where a 3.5 m² one had stood. It can equally take one away, and it does:
     *
     *      mydear-190 (-212.7,-340.1)  [36.59]     -> [154.94]      gained
     *      mydear-505 (-115.4,-106.9)  [28411.36]  -> []            LOST
     *      mydear-1432 (-299.5,-815.8) [46.84, 47573.45] -> [47573.45]
     *
     * plus §7y's own three, which do not move. So the flag-on total falls by exactly 14 -
     * the fourteen ribbons - and that clean number is a coincidence of one gain cancelling
     * one loss, not a subtraction. Measured by building the world at 88fde8ac and at this
     * tree and diffing them block by block, all 37 609 blocks, not inferred from the totals.
     *
     * The 28 411 m² loss is not a defect and is not repairable without making the refusal a
     * second gate inside `_designBuilding`, which is the shape §7y.8's mutation 7 fails on:
     * the draw sequence of a block is a function of which pieces reach the draw, and any
     * rule that removes a piece moves it.
     */
    [Fact]
    public void TheDrawShiftGivesOneBlockABuildingAndTakesOneAway()
    {
        var off = World(false);
        var on = World(true);

        /*
         * Flag off there is no such block at all - every refused piece there is the only
         * piece its block has - so the whole effect is flag-on.
         */
        Assert.Equal(0, off.MixedBlocks);
        Assert.Empty(off.MixedBlockBuildingAreas);

        Assert.Equal(6, on.MixedBlocks);

        var areas = on.MixedBlockBuildingAreas.OrderBy(a => a).ToList();
        Assert.Equal(5, areas.Count);
        Assert.InRange(areas[0], 132.0, 133.0);       // §7y's mydear-474, unmoved
        Assert.InRange(areas[1], 154.0, 156.0);       // mydear-190, GAINED here
        Assert.InRange(areas[2], 1450.0, 1451.0);     // §7y's mydear-580, unmoved
        Assert.InRange(areas[3], 9795.0, 9796.0);     // §7y's mydear-590, unmoved
        Assert.InRange(areas[4], 47573.0, 47574.0);   // mydear-1432, kept its big piece

        /*
         * ⚠️ AND ONE OF THE SIX CARRIES NOTHING, which is the loss: that block's remaining
         * piece of buildable land is 28 411 m² and it had a building on it at 88fde8ac.
         * A count of six mixed blocks would be satisfied without it; the land area is what
         * says the block that lost its draw is a large one rather than another scrap.
         */
        Assert.Equal(1, on.MixedBlocksWithNoBuilding);
        Assert.Equal(1, on.MixedBlockLandAreas.Count(a => a > 20000.0 && a < 30000.0));
        Assert.InRange(on.MixedBlockLandAreas.Max(), 47573.0, 47574.0);
    }


    /**
     * ⚠️ A REMOVED BUILDING TAKES ITS SHOPFRONTS WITH IT, AND A RIBBON HAS A LOT OF THEM:
     * 271 of 527 794, against the 13 the ten square metre floor cost.
     *
     * `_addShops` walks a building's own perimeter at 5 m a shopfront, so it sizes the shop
     * count by PERIMETER while everything else about a building is sized by area - which is
     * §7y.10's open item and is exactly why a sliver is expensive here. The fourteen ribbons
     * removed carry perimeters of 27.8 m to 172 m between them, i.e. up to nineteen 5 m
     * shopfronts on a wall 0.32 m thick that a player could not walk into. Removing them
     * removes about a fifth of a per-mille of the world's shopfronts.
     *
     * Everything downstream of a shopfront goes with it - the shop window, the shop POI, the
     * TALE shop door, the nogame shop. `SpatialModel.ExtractFrom` gives a building with no
     * shop ONE home/warehouse/office location and a building with shops one location per
     * shop-tagged shopfront, so both counts here take their TALE locations with them.
     *
     * 271 is not 14 buildings' worth of ordinary shopfronts; that is the point of recording
     * it rather than the building count alone.
     */
    [Theory]
    //                 shopfronts  blocks with a building  blocks with more than one
    [InlineData(false, 572402, 27374, 13)]
    [InlineData(true, 527523, 25012, 790)]
    public void ARemovedRibbonTakesAnExpensiveNumberOfShopfronts(
        bool gradeSeparation, int shopFronts, int withABuilding, int several)
    {
        var m = World(gradeSeparation);

        Assert.Equal(shopFronts, m.ShopFronts);
        Assert.Equal(withABuilding, m.BlocksWithABuilding);
        Assert.Equal(several, m.BlocksWithMoreThanOne);
    }


    /**
     * ⚠️ AND THE GENERATOR'S OWN RECORD STILL FOLLOWS THE PREDICATE, now that the predicate
     * has two terms.
     *
     * `estateTooSmall` means "not one piece of this block's land can carry a building" and
     * is written from the same predicate the refusal is made with, so it picks up the blocks
     * whose only land is too thin as well as the ones whose only land is too small: 828 to
     * 846 flag on, and 29 flag off, unmoved. §7x's own 9 and 522 - the blocks with no land
     * at all - are still inside it.
     */
    [Theory]
    //                 tagged
    [InlineData(false, 29)]
    [InlineData(true, 846)]
    public void TheGeneratorRecordsAWidthRefusalTheSameWay(bool gradeSeparation, int tagged)
    {
        Assert.Equal(tagged, World(gradeSeparation).Tagged);
    }


    /*
     * ============================================== plumbing ==========================
     */

    /**
     * Half the polygon's own narrowest width, in metres: the widest uniform inset it still
     * survives, binary searched to a tenth of a millimetre over the production predicate.
     *
     * ⚠️ ONE COPY, and it is the one §7x defined. EmptyBlockTests and MinBuildingAreaTests
     * both call it; two of them measuring a block a hair differently is how "the block's
     * half-width" and "MetaGen.MinBuildingWidth" would come to mean different things.
     */
    internal static double HalfWidthOf(List<IntPoint> poly)
    {
        if (poly.Count < 3) return 0.0;

        double lo = 0.0, hi = 512.0;
        for (int i = 0; i < 23; ++i)
        {
            double mid = 0.5 * (lo + hi);
            if (BlockGraph.SurvivesInsetBy(poly, (float)mid)) lo = mid; else hi = mid;
        }

        return lo;
    }


    internal static double HalfWidthOf(IList<Vector3> outline) => HalfWidthOf(AsPath(outline));


    /** An outline in metres as the Clipper path the estate's own inset is made of. */
    internal static List<IntPoint> AsPath(IList<Vector3> outline)
        => outline.Select(p => new IntPoint((int)(p.X * 10f), (int)(p.Z * 10f))).ToList();


    private static List<Vector3> _asOutline(IList<IntPoint> poly)
        => poly.Select(p => new Vector3(p.X / 10f, 0f, p.Y / 10f)).ToList();


    /** A rectangle in Clipper's own tenth-metre units. */
    private static List<IntPoint> _rect(int w, int h)
        => new() { new(0, 0), new(w, 0), new(w, h), new(0, h) };


    /**
     * The same inset with a ROUND join instead of the estate's mitre - a deliberate second
     * expression, which exists only to show that the two differ and is used by exactly one
     * fixture.
     */
    private static bool _survivesRoundInsetBy(List<IntPoint> poly, float metres)
    {
        var offset = new ClipperOffset();
        offset.AddPath(poly, JoinType.jtRound, EndType.etClosedPolygon);
        var solution = new List<List<IntPoint>>();
        offset.Execute(ref solution, -metres * 10f);

        return solution.Any(p => p.Count > 0);
    }


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


    private static double _perimeterOf(IList<Vector3> v)
    {
        double m = 0.0;
        for (int i = 0; i < v.Count; ++i) m += (v[(i + 1) % v.Count] - v[i]).Length();
        return m;
    }


    private static double _minSideOf(IList<Vector3> v)
    {
        double m = Double.MaxValue;
        for (int i = 0; i < v.Count; ++i) m = Math.Min(m, (v[(i + 1) % v.Count] - v[i]).Length());
        return v.Count > 0 ? m : 0.0;
    }
}
