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
 * ⚠️ THE BLOCKS WITH NO BUILDABLE LAND AT ALL - 9 flag off and 522 flag on - AND THE ANSWER
 * IS THAT THEY ARE CORRECT (§7x).
 *
 * WP-O2 (§7v) recorded the count as a *found and not fixed* item, on the strength of one
 * cell of one table and nothing else: "522 flag-on blocks have no buildable land at all
 * after their structures are subtracted, against 9 flag off. Nothing looks at what a block
 * with no estate should be." This file is what those blocks turned out to be.
 *
 * ⚠️ THE HEADLINE IS THAT THE OBVIOUS READING IS WRONG IN BOTH OF ITS HALVES.
 *
 * "After their structures are subtracted" is not what happens: **every one of the 531 is
 * emptied by the PAVEMENT INSET ALONE**, and the structure subtraction and the spur
 * subtraction between them empty not one block in either world. And ledger item (o) did not
 * make them: the tree at 6c228797^ - before WP-O1 - produced exactly 9 and exactly 522
 * blocks tagged `estateTooSmall`, with byte-identical outlines, and not one of them carried
 * a building there either.
 *
 * ⚠️ WHAT THEY ARE: a block with no buildable land is a block that is ENTIRELY PAVEMENT.
 * The equivalence is exact over all 78 500 blocks of both worlds, in both directions: the
 * land is empty exactly when the block's own half-width is less than its Quarter.
 * SidewalkWidth, so every square metre of it is inside the pavement strip. They are small
 * triangles between three roads - median 68 m² flag on against a block median of 7692 -
 * whose narrowest width is a median 7.0 m where the pavement asks for 12. A traffic island
 * is what that is, and a traffic island is what the generator makes of it.
 *
 * ⚠️ AND THE 9 -> 522 IS THE HEAVY-FIRST CITY'S ROADS, NOT ITS STRUCTURES. The street
 * graph's own face is never small: the smallest is 471 m² flag off and 486 m² flag on, and
 * on all 531 that face is wide enough to carry the pavement. What eats it is the
 * carriageway between the centre line and the kerb, and §13's finding is why - a heavy-first
 * city has no alleys in it, so its streets are a median 18.8 m wide against 9.85. The
 * control that says so without appealing to that: in the TEN cities that get no structure
 * at all with the flag on, the rate still goes from 0 of 441 blocks to 4 of 355.
 *
 * ⚠️ THE ONE THING THAT WAS NOT CORRECT IS ON THE OTHER SIDE OF THE SAME CLIFF, and it was
 * recorded here rather than asserted at zero: 53 blocks flag on and 4 flag off carried a
 * building standing on less than one square metre, the smallest on 0.005 m², all of them one
 * storey on the same kind of tiny triangle - because the rule refused a block with 0 m² of
 * land and accepted one with 50 cm². ✅ FIXED 2026-09-08 (§7y): the owner set a floor of ten
 * square metres, `engine.world.MetaGen.MinBuildingArea`, and MinBuildingAreaTests is where
 * that lives. AMatchboxStandsOnTheOtherSideOfTheSameCliff below keeps the old numbers beside
 * the new ones rather than being deleted.
 */
public class EmptyBlockTests
{
    private static readonly object _lo = new();
    private static readonly Dictionary<bool, Measured> _measured = new();


    private sealed class Block
    {
        internal string City;
        internal float SidewalkWidth;
        internal int Corners;
        internal double Area, CentreArea;
        internal float HalfWidth, CentreHalfWidth, MeanStreetWidth;
        internal int Pieces, InsetOnly, InsetAndStructures, InsetAndSpurs;
        internal bool TooSmall, PavementFits;
        internal int Buildings, StructuresReaching, SpursReaching, StructuresInCity;
        internal double RecoverArea, RecoverSide;
        internal double MinBuildingArea = -1.0, MinBuildingSide, MinBuildingHeight;
    }


    private sealed class Measured
    {
        internal List<Block> Blocks = new();
        internal int StartCityBlocks, StartCityEmpty;
    }


    /**
     * The seventy shipped cities with their blocks traced, and for each block the four
     * readings of QuarterGenerator.BuildableLandOf that say WHICH step took its land: the
     * inset on its own, the inset plus the structures, the inset plus the spurs, and what
     * the generator actually did.
     *
     * The three counterfactuals go through the production expression rather than a copy of
     * it, with an empty stroke list standing in for the step being left out - which is
     * exactly the state a city with no structure in it is already in, so the same code runs.
     */
    private static Measured World(bool gradeSeparation)
    {
        lock (_lo)
        {
            if (_measured.TryGetValue(gradeSeparation, out var cached)) return cached;

            var m = new Measured();
            var none = new List<Stroke>();

            foreach (var city in BlockRingClosureTests.World(gradeSeparation))
            {
                var core = BlockGraph.TwoCoreOf(city.Store);
                var spurs = BlockGraph.SpurCorridorsOf(city.Store, core);
                var structures = city.Store.GetStrokes()
                    .FindAll(s => StrokeKinds.IsStructure(s.Kind));

                bool isStartCity = city.Cluster.IdString.EndsWith("-0");

                foreach (var q in city.Quarters.GetQuarters())
                {
                    var estate = q.GetEstates()[0];
                    var outline = estate.GetPoints();
                    var ring = q.GetDelims().Select(d => d.StartPoint).ToList();
                    var own = q.GetDelims().Select(d => d.Stroke).ToList();

                    var b = new Block
                    {
                        City = city.Cluster.IdString,
                        SidewalkWidth = q.SidewalkWidth,
                        Corners = ring.Count,
                        Area = _area2Of(ring) / 2.0,
                        CentreArea = _area2Of(
                            q.GetDelims().Select(d => d.StreetPoint.Pos).ToList()) / 2.0,
                        HalfWidth = _halfWidthOf(outline),
                        CentreHalfWidth = _halfWidthOf(
                            q.GetDelims()
                                .Select(d => new Vector3(d.StreetPoint.Pos.X, 0f,
                                    d.StreetPoint.Pos.Y)).ToList()),
                        MeanStreetWidth = own.Average(s => s.StreetWidth()),
                        Pieces = _piecesOf(
                            QuarterGenerator.BuildableLandOf(q, estate, structures, spurs)),
                        InsetOnly = _piecesOf(
                            QuarterGenerator.BuildableLandOf(q, estate, none, none)),
                        InsetAndStructures = _piecesOf(
                            QuarterGenerator.BuildableLandOf(q, estate, structures, none)),
                        InsetAndSpurs = _piecesOf(
                            QuarterGenerator.BuildableLandOf(q, estate, none, spurs)),

                        /*
                         * The generator's own record that it found nothing to build on -
                         * a debug tag it has written since the file was written, which
                         * nothing has ever read.
                         */
                        TooSmall = q.GetDebugString().Contains("estateTooSmall"),

                        /*
                         * ...and whether the block FLOOR thinks there is room for a
                         * pavement of this width, which is a different expression asking
                         * the same question (§7c).
                         */
                        PavementFits = null != SidewalkRing.InsetOf(outline, q.SidewalkWidth),

                        Buildings = q.GetEstates().Sum(e => e.GetBuildings().Count),
                        StructuresReaching = QuarterGenerator.Reaching(q, structures).Count(),
                        SpursReaching = QuarterGenerator.Reaching(q, spurs).Count(),
                        StructuresInCity = structures.Count
                    };

                    /*
                     * The most any relaxation of the pavement rule could possibly recover
                     * here: the widest inset this outline still survives, backed off by a
                     * tenth. An upper bound on every repair that keeps the block's shape.
                     */
                    if (0 == b.Pieces)
                    {
                        var rec = _shapeOf(_insetBy(outline, 0.9f * b.HalfWidth));
                        b.RecoverArea = rec.Area;
                        b.RecoverSide = rec.MinSide;
                    }

                    foreach (var e in q.GetEstates())
                    foreach (var building in e.GetBuildings())
                    {
                        var poly = building.GetPoints()
                            .Select(v => new IntPoint((int)(v.X * 10f), (int)(v.Z * 10f)))
                            .ToList();
                        double a = Math.Abs(Clipper.Area(poly)) / 100.0;
                        if (b.MinBuildingArea >= 0.0 && a >= b.MinBuildingArea) continue;

                        b.MinBuildingArea = a;
                        b.MinBuildingHeight = building.GetHeight();

                        /*
                         * The shortest side taken off the building's own Vector3 corners
                         * rather than off the tenth-metre copy above, because this is the
                         * float _designBuilding compared against 2.0f - and one building of
                         * the world sits so exactly on that boundary that dividing before
                         * subtracting rather than after puts it on the other side of it.
                         */
                        var v = building.GetPoints();
                        b.MinBuildingSide = Double.MaxValue;
                        for (int i = 0; i < v.Count; ++i)
                        {
                            b.MinBuildingSide = Math.Min(
                                b.MinBuildingSide, (v[(i + 1) % v.Count] - v[i]).Length());
                        }
                    }

                    m.Blocks.Add(b);

                    if (!isStartCity) continue;

                    ++m.StartCityBlocks;
                    if (0 == b.Pieces) ++m.StartCityEmpty;
                }
            }

            _measured[gradeSeparation] = m;
            return m;
        }
    }


    /*
     * ============================================== which step took the land ==========
     */

    /**
     * ⚠️ EVERY BLOCK WITH NO BUILDABLE LAND IS EMPTIED BY THE PAVEMENT INSET ALONE, and
     * neither subtraction empties one anywhere in either world.
     *
     * This is the first thing that had to be measured and it refutes the sentence the item
     * was recorded under - "no buildable land at all AFTER THEIR STRUCTURES ARE SUBTRACTED".
     * The structures have nothing to do with it. Asked four ways over the same block,
     * through the production expression each time:
     *
     *      inset alone                     empty on all 9 / 522
     *      inset + structure carriageways  empties no block the inset left standing
     *      inset + spur corridors          empties no block the inset left standing
     *      both, i.e. what the game does   empty on exactly the same 9 / 522
     *
     * The last line is the one that closes it: there is no block anywhere whose land the
     * inset leaves and the subtractions take away. So "which step" has a single answer over
     * the whole world, and it is the step that has been there since the file was written.
     */
    [Theory]
    //                  blocks  empty
    [InlineData(false, 40891, 9)]
    [InlineData(true, 37609, 522)]
    public void ThePavementInsetTakesTheLandAndNeitherSubtractionEverDoes(
        bool gradeSeparation, int blocks, int empty)
    {
        var all = World(gradeSeparation).Blocks;

        Assert.Equal(blocks, all.Count);
        Assert.Equal(empty, all.Count(b => 0 == b.Pieces));

        /*
         * Every empty block is already empty after the inset, with nothing subtracted.
         */
        Assert.Equal(empty, all.Count(b => 0 == b.Pieces && 0 == b.InsetOnly));

        /*
         * ...and neither subtraction, alone or together, ever empties a block the inset
         * left standing. Three statements rather than one, because a single "production
         * empties none extra" would be satisfied if the two subtractions cancelled.
         */
        Assert.Equal(0, all.Count(b => b.InsetOnly > 0 && 0 == b.InsetAndStructures));
        Assert.Equal(0, all.Count(b => b.InsetOnly > 0 && 0 == b.InsetAndSpurs));
        Assert.Equal(0, all.Count(b => b.InsetOnly > 0 && 0 == b.Pieces));

        /*
         * Not vacuous: something IS subtracted on thousands of blocks - the counts move,
         * they just never move to zero.
         */
        Assert.True(all.Count(b => b.SpursReaching > 0) > 1000,
            $"only {all.Count(b => b.SpursReaching > 0)} blocks have a spur reaching them");
    }


    /**
     * ⚠️ AND WHAT SUCH A BLOCK IS: ONE THAT IS ENTIRELY PAVEMENT.
     *
     * The equivalence is exact over all 40 891 and 37 609 blocks, in BOTH directions: the
     * buildable land is empty exactly when the block's own half-width - the widest uniform
     * inset its outline survives, binary searched to half a millimetre - is less than the
     * Quarter.SidewalkWidth the estate is inset by. That is the definition of "every point
     * of this block is inside the pavement strip".
     *
     * So there is nothing to diagnose. A block whose narrowest width is 7 m, with a 6 m
     * pavement asked for on both sides of it, has no ground left that is not pavement, and
     * the generator's answer - a paved island with no building - is the right one.
     *
     * The size distribution is recorded beside the equivalence because it is what makes the
     * conclusion readable: these are triangles of a median 68 m² flag on against a block
     * median of 7692 m², and the largest of them is a 1136 m² ribbon 11 m wide and 100 m
     * long between two parallel arterials, which is a boulevard median.
     */
    [Theory]
    //                 empty  3-corner  4-corner  area p50 lo/hi  area max lo/hi
    [InlineData(false, 9, 9, 0, 100f, 200f, 200f, 250f)]
    [InlineData(true, 522, 517, 5, 50f, 100f, 1100f, 1200f)]
    public void ABlockWithNoBuildableLandIsOneThatIsAllPavement(
        bool gradeSeparation, int empty, int triangles, int quads,
        float areaLo, float areaHi, float maxLo, float maxHi)
    {
        var all = World(gradeSeparation).Blocks;

        /*
         * ⚠️ THE EQUIVALENCE, both ways round. A one-way containment would be satisfied by
         * a rule that empties blocks for some other reason as well.
         */
        Assert.Equal(0, all.Count(b => b.HalfWidth < b.SidewalkWidth && b.Pieces > 0));
        Assert.Equal(0, all.Count(b => b.HalfWidth >= b.SidewalkWidth && 0 == b.Pieces));
        Assert.Equal(empty, all.Count(b => b.HalfWidth < b.SidewalkWidth));

        var e = all.Where(b => 0 == b.Pieces).ToList();

        Assert.Equal(triangles, e.Count(b => 3 == b.Corners));
        Assert.Equal(quads, e.Count(b => 4 == b.Corners));
        Assert.Equal(empty, e.Count(b => b.Corners <= 4));

        Assert.InRange(_median(e.Select(b => b.Area)), areaLo, areaHi);
        Assert.InRange(e.Max(b => b.Area), maxLo, maxHi);
    }


    /**
     * ⚠️ THE STREET GRAPH'S OWN FACE IS NEVER SMALL - the block is eaten by the CARRIAGEWAY.
     *
     * A city block's outline is not the face of the street graph: it runs through the
     * junctions' section points, i.e. the face inset by half the street width on every edge.
     * Measured on the same rings both ways: the face through the junctions themselves is a
     * minimum of 471 m² flag off and 486 m² flag on - never tiny - and it is wide enough for
     * the pavement on every single one of the 531 blocks that end up with nothing.
     *
     * The two half-widths differ by the half street width to within a few centimetres, which
     * is the whole of the arithmetic:
     *
     *      outline half-width  =  face half-width  -  street width / 2
     *
     * and a block has no land when that is under Quarter.SidewalkWidth. Nothing about these
     * blocks is degenerate in the graph; they are ordinary small triangles with a lot of
     * road on them.
     */
    [Theory]
    //                 smallest face lo/hi   half-width gap lo/hi (m)
    [InlineData(false, 400f, 500f, 4.8f, 5.1f)]
    [InlineData(true, 450f, 520f, 9.2f, 9.7f)]
    public void TheGraphsOwnFaceIsNeverTooSmall(
        bool gradeSeparation, float faceLo, float faceHi, float gapLo, float gapHi)
    {
        var e = World(gradeSeparation).Blocks.Where(b => 0 == b.Pieces).ToList();

        Assert.InRange(e.Min(b => b.CentreArea), faceLo, faceHi);

        /*
         * ...and every one of those faces would carry the pavement. It is the carriageway
         * between the centre line and the kerb that leaves no room, not the face.
         */
        Assert.Equal(e.Count, e.Count(b => b.CentreHalfWidth >= b.SidewalkWidth));

        Assert.InRange(
            _median(e.Select(b => (double)(b.CentreHalfWidth - b.HalfWidth))), gapLo, gapHi);
        Assert.InRange(_median(e.Select(b => (double)b.MeanStreetWidth / 2.0)), gapLo, gapHi);
    }


    /**
     * ⚠️ AND SO 9 -> 522 IS THE HEAVY-FIRST CITY'S ROADS AND NOT ITS STRUCTURES, which is
     * the reading the item was recorded under and the one that had to be tested.
     *
     * Three separate things say so.
     *
     * (1) Not one of the 531 is emptied by a structure subtraction - the gate above.
     * (2) 515 of the 522 have no structure whose footprint even reaches them.
     * (3) ⚠️ THE CONTROL: ten of the seventy cities get no structure at all with the flag
     *     on. Those same ten cities go from 0 empty blocks of 441 flag off to 4 of 355 flag
     *     on - a rate of 0.00 % to 1.13 %, against 1.39 % in the sixty cities that do get
     *     structures. Whatever is doing this does not need a structure to do it.
     *
     * What is left is §13's finding, which was recorded about the stroke network and never
     * followed through to what stands on it: a heavy-first city has no alleys in it, so
     * every street in it is an arterial. The median street of the flag-on world is 18.8 m
     * wide against 9.85 m flag off, and the block outline is the graph face inset by half of
     * that on every edge.
     */
    [Theory]
    //                 cities without a structure, their blocks, their empties, width p50,
    //                 empty blocks with no structure reaching them
    [InlineData(false, 10, 441, 0, 9.5f, 10.5f, 9)]
    [InlineData(true, 10, 355, 4, 18.0f, 19.5f, 515)]
    public void TheHeavyFirstCitysRoadsDoThisAndItsStructuresDoNot(
        bool gradeSeparation, int cities, int blocks, int empty, float widthLo,
        float widthHi, int untouched)
    {
        var all = World(gradeSeparation).Blocks;

        /*
         * Which cities those are is read off the FLAG-ON world in both runs, because "a city
         * that gets no structure" is a property of the flag-on run and the flag-off run is
         * its control on the same ground.
         */
        var clean = new HashSet<string>(
            World(true).Blocks.Where(b => 0 == b.StructuresInCity).Select(b => b.City));

        Assert.Equal(cities, clean.Count);

        var sel = all.Where(b => clean.Contains(b.City)).ToList();

        Assert.Equal(blocks, sel.Count);
        Assert.Equal(empty, sel.Count(b => 0 == b.Pieces));

        /*
         * ...and the whole world's median street, which is what actually changed.
         */
        Assert.InRange(_median(all.Select(b => (double)b.MeanStreetWidth)), widthLo, widthHi);

        /*
         * ...and almost none of the empty blocks has a structure whose footprint even
         * reaches it - seven of the 522, none of which it empties.
         */
        var e = all.Where(b => 0 == b.Pieces).ToList();
        Assert.Equal(untouched, e.Count(b => 0 == b.StructuresReaching));
    }


    /*
     * ============================================== is it a defect ====================
     */

    /**
     * ⚠️ NOTHING COULD STAND THERE EVEN IF THE PAVEMENT GAVE WAY ENTIRELY.
     *
     * The most generous repair that keeps the block's shape is to inset by whatever the
     * block can take rather than by the pavement width. Measured at nine tenths of that - the
     * widest inset the outline survives - the land that comes back is a median 0.75 m² flag
     * on and 1.56 m² flag off, at most 104 m², and its shortest side is under the 2 m at
     * which _designBuilding caps a building at one storey on 516 of the 522 and 7 of the 9.
     *
     * So the whole recoverable stock is a one-storey shed on a square metre. The refusal is
     * not throwing anything away, and no threshold anywhere would recover anything worth
     * having. That is what makes this correct behaviour rather than a defect.
     */
    [Theory]
    //                 recover p50 lo/hi (m²)  one-storey  under 20 m²
    [InlineData(false, 0.5, 3.0, 7, 9)]
    [InlineData(true, 0.3, 1.5, 516, 519)]
    public void NothingCouldStandThereEvenIfThePavementGaveWay(
        bool gradeSeparation, double lo, double hi, int oneStorey, int tiny)
    {
        var e = World(gradeSeparation).Blocks.Where(b => 0 == b.Pieces).ToList();

        Assert.InRange(_median(e.Select(b => b.RecoverArea)), lo, hi);
        Assert.Equal(oneStorey, e.Count(b => b.RecoverSide <= 2.0));
        Assert.Equal(tiny, e.Count(b => b.RecoverArea < 20.0));
    }


    /**
     * ⚠️ AND THE BLOCK FLOOR SAYS THE SAME THING THROUGH A DIFFERENT EXPRESSION.
     *
     * generation.SidewalkRing.InsetOf is what gives a block floor its level pavement rim
     * (§7c). It refuses every one of the 531, so such a block is drawn as one plain fan at
     * pavement height across its whole area - which is the picture the geometry above says
     * it should be, arrived at by a rule written for another purpose entirely.
     *
     * ⚠️ IT HAS TWO GUARDS AND EITHER IS SUFFICIENT ON THIS CLASS, which mutation found and
     * reading would not have. Deleting the corner-ramp length test - "a block whose edges are
     * too short to carry both of their ramps has no room for a pavement of this width at all"
     * - passes this gate, and so does deleting _isUsable, which catches the same blocks on its
     * self-intersection scan and whose own comment says as much: "the interior ring crosses
     * itself ... this is what happens once a block is narrower than twice its pavement".
     * Deleting BOTH is what fails. Neither is therefore load bearing on its own here, and the
     * gate is about the answer rather than about which line produces it.
     *
     * The control is that the refusal is not universal: over 90 % of the blocks that DO have
     * land get their rim, so "InsetOf returns null" is not simply what happens.
     */
    [Theory]
    [InlineData(false, 40891)]
    [InlineData(true, 37609)]
    public void TheBlockFloorRefusesItsPavementRimOnEveryOneOfThem(
        bool gradeSeparation, int blocks)
    {
        var all = World(gradeSeparation).Blocks;

        Assert.Equal(blocks, all.Count);
        Assert.Equal(0, all.Count(b => 0 == b.Pieces && b.PavementFits));

        /*
         * ...and the great majority of blocks with land do get their rim, so the agreement
         * above is a statement about these blocks rather than about the rim.
         */
        Assert.True(all.Count(b => b.Pieces > 0 && b.PavementFits) > all.Count * 0.9,
            $"only {all.Count(b => b.Pieces > 0 && b.PavementFits)} of {all.Count} get a rim");
    }


    /**
     * ⚠️ THE GENERATOR ALREADY RECORDS EVERY ONE OF THEM, AND NOTHING HAS EVER READ IT.
     *
     * _createBuildings tags a block `estateTooSmall` when not one piece of its buildable
     * land can carry a building, and it has done since the file was written. Every one of
     * the blocks this file is about is in that set - 0 blocks with no land at all that the
     * generator failed to tag, over 78 500 blocks - so "which blocks have no buildable land"
     * was answerable from the debug map all along.
     *
     * The other tag, `estateWithoutPoints`, is the null return for a block whose outline has
     * no points at all, and it fires on nothing.
     *
     * ⚠️ AND THE COUNTS ARE THE ONES THE TREE BEFORE LEDGER ITEM (o) PRODUCED. Measured by
     * restoring 6c228797^ into a worktree rather than reasoned about: that tree tagged
     * exactly 9 and exactly 522 blocks `estateTooSmall`, every one of them with a
     * byte-identical outline to one of these, and not one of them carried a building there
     * either. WP-O1/O2/O3 neither created these blocks nor took a building off one.
     *
     * ⚠️ SUPERSEDED BY §7y AND THE OLD TEXT IS KEPT, because what this asserted stopped
     * being true when the owner's ten square metre floor landed. It used to read:
     *
     *      [InlineData(false, 9)] [InlineData(true, 522)]
     *      Assert.Equal(tagged, all.Count(b => b.TooSmall));
     *      Assert.Equal(0, all.Count(b => b.TooSmall != (0 == b.Pieces)));
     *
     * i.e. the tag was exactly "this block has no buildable land", with 0 disagreements in
     * BOTH directions. `estateTooSmall` now follows QuarterGenerator.CanCarryABuilding, so
     * it also covers the 20 and 306 blocks whose only land is under the floor - 29 and 828
     * altogether, which MinBuildingAreaTests pins. What survives here unchanged is the half
     * that is about THIS file's population: every block with no land at all is tagged. The
     * other direction moved to
     * MinBuildingAreaTests.TheGeneratorRecordsTheRefusalWithTheSamePredicateItMakesItWith.
     */
    [Theory]
    //                 estateTooSmall (also the pre-(o) count)  the whole tag today
    [InlineData(false, 9, 29)]
    [InlineData(true, 522, 828)]
    public void TheGeneratorAlreadyTagsThemAndTheCountIsOlderThanLedgerItemO(
        bool gradeSeparation, int noLand, int tagged)
    {
        var all = World(gradeSeparation).Blocks;

        Assert.Equal(noLand, all.Count(b => 0 == b.Pieces));
        Assert.Equal(tagged, all.Count(b => b.TooSmall));

        /*
         * Every block with no land at all is tagged; the rest of the tag is the floor.
         */
        Assert.Equal(0, all.Count(b => 0 == b.Pieces && !b.TooSmall));
    }


    /**
     * ⚠️ AND THE PLAYER DOES NOT START ON ONE.
     *
     * engine.world.PlayerStart.PoseIn puts a new game on the first estate of the start city
     * that carries no building, and a block with no buildable land is guaranteed to carry
     * none - so it is a candidate by construction, and its centre is a point on a traffic
     * island between three arterials. `cluster-clusters-mydear-0`, the city at the world
     * origin, has none of them in either flag state, so the branch cannot be reached there
     * today. It is not protected against, which is why the number is pinned.
     */
    [Theory]
    //                 start city blocks
    [InlineData(false, 42)]
    [InlineData(true, 28)]
    public void TheStartCityHasNoneOfThem(bool gradeSeparation, int blocks)
    {
        var m = World(gradeSeparation);

        Assert.Equal(blocks, m.StartCityBlocks);
        Assert.Equal(0, m.StartCityEmpty);
    }


    /**
     * ⚠️ THE MATCHBOX ON THE OTHER SIDE OF THE SAME CLIFF IS GONE, AND THIS IS WHERE IT WAS
     * RECORDED.
     *
     * The rule used to refuse a block with 0 m² of buildable land and accept one with
     * 0.005 m². This gate counted what that cost - 4 blocks flag off and 53 flag on carrying
     * a building under ONE SQUARE METRE, 8 / 126 under four, the smallest fifty square
     * centimetres and three metres tall - and left it deliberately unasserted at zero,
     * because whether a block needs a minimum area to be built on was the owner's decision
     * and not this file's.
     *
     * ⚠️ THE DECISION WAS MADE ON 2026-09-08 AND THIS GATE IS SUPERSEDED BY IT (§7y). Its
     * old text, which is now false in both flag states:
     *
     *      [InlineData(false, 4, 8, 0.01, 0.05, 1445)]
     *      [InlineData(true, 53, 126, 0.001, 0.01, 814)]
     *      Assert.Equal(underOne, withA.Count(b => b.MinBuildingArea < 1.0));
     *      Assert.Equal(underFour, withA.Count(b => b.MinBuildingArea < 4.0));
     *      Assert.InRange(withA.Min(b => b.MinBuildingArea), smallLo, smallHi);
     *
     * engine.world.MetaGen.MinBuildingArea is ten square metres and the counts are zero;
     * MinBuildingAreaTests.NoBuildingStandsOnLessThanTenSquareMetres is the successor, and
     * it carries the smallest building there now is - 11.92 m² flag off and 10.05 m² flag
     * on - so that a zero cannot be met by a world with no buildings in it.
     *
     * ⚠️ WHAT DOES NOT MOVE IS THE SECOND HALF, and it is kept here because it is the reason
     * this class needed a rule of its own: `_designBuilding`'s `minHouseSide <= 2.0f` catches
     * a matchbox for HEIGHT and never for existence. It still holds every building with a
     * side under 2 m to a single storey - and the population is unchanged in the flag-off
     * world and barely moved flag on, because a short SIDE is a mitred corner far more often
     * than it is a small building.
     */
    [Theory]
    //                 no building below the floor  one-storey blocks (was 1445 / 814)
    [InlineData(false, 11.9, 12.0, 1439)]
    [InlineData(true, 10.0, 10.1, 725)]
    public void AMatchboxStandsOnTheOtherSideOfTheSameCliff(
        bool gradeSeparation, double smallLo, double smallHi, int narrow)
    {
        var withA = World(gradeSeparation).Blocks.Where(b => b.Buildings > 0).ToList();

        Assert.Equal(0, withA.Count(b => b.MinBuildingArea < MetaGen.MinBuildingArea));
        Assert.InRange(withA.Min(b => b.MinBuildingArea), smallLo, smallHi);

        /*
         * ...and the only rule that ever said anything about them at all: a building whose
         * shortest side is 2 m or less is exactly one storey, everywhere in the world. The
         * implication rather than a height bound, because _designBuilding's next branch caps
         * at TWO storeys and a bound of 6 m would be satisfied by that one too.
         */
        Assert.Equal(narrow, withA.Count(b => b.MinBuildingSide <= 2.0));
        Assert.Equal(0,
            withA.Count(b => b.MinBuildingSide <= 2.0
                             && Math.Abs(b.MinBuildingHeight - MetaGen.StoryHeight) > 1e-3));
    }


    /*
     * ============================================== plumbing ==========================
     */

    private static double _area2Of(IList<Vector2> ring)
    {
        double a = 0.0;
        for (int i = 0; i < ring.Count; ++i)
        {
            var p = ring[i];
            var q = ring[(i + 1) % ring.Count];
            a += (double)p.X * q.Y - (double)q.X * p.Y;
        }

        return Math.Abs(a);
    }


    private static int _piecesOf(List<List<IntPoint>> land)
        => null == land ? 0 : land.Count(p => p.Count > 0);


    /**
     * The widest uniform inset this outline still survives, in metres - half the polygon's
     * own narrowest width. Binary searched to half a millimetre through the very
     * ClipperOffset the estate's inset goes through, so "wide enough for the pavement" and
     * "the pavement leaves something" are the same question asked of the same code.
     */
    private static float _halfWidthOf(IList<Vector3> outline)
    {
        if (outline.Count < 3) return 0f;

        float lo = 0f, hi = 512f;
        for (int i = 0; i < 20; ++i)
        {
            float mid = 0.5f * (lo + hi);
            if (_insetBy(outline, mid).Any(p => p.Count > 0)) lo = mid; else hi = mid;
        }

        return lo;
    }


    private static List<List<IntPoint>> _insetBy(IList<Vector3> outline, float d)
    {
        var poly = outline
            .Select(p => new IntPoint((int)(p.X * 10f), (int)(p.Z * 10f))).ToList();

        var offset = new ClipperOffset();
        offset.AddPath(poly, JoinType.jtMiter, EndType.etClosedPolygon);
        var solution = new List<List<IntPoint>>();
        offset.Execute(ref solution, -d * 10f);

        return solution;
    }


    /**
     * Total area in m² and the shortest side of the largest piece, which is the
     * minHouseSide _designBuilding would measure round it.
     */
    private static (double Area, double MinSide) _shapeOf(List<List<IntPoint>> land)
    {
        double area = 0.0, best = -1.0;
        List<IntPoint> biggest = null;

        foreach (var p in land)
        {
            if (0 == p.Count) continue;

            double a = Math.Abs(Clipper.Area(p)) / 100.0;
            area += a;
            if (a > best) { best = a; biggest = p; }
        }

        return null == biggest ? (0.0, 0.0) : (area, SpurEstateTests.MinSideOf(biggest));
    }


    private static double _median(IEnumerable<double> values)
    {
        var v = values.OrderBy(x => x).ToList();
        return 0 == v.Count ? 0.0 : v[v.Count / 2];
    }
}
