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
 * ⚠️ WP-O2 — THE DEAD-END SPUR COMES OUT OF THE ESTATE, AND EVERY PIECE THAT IS LEFT GETS
 * ITS OWN BUILDING.
 *
 * Ledger item (o), repair (b), second of three work packages. WP-O1 peeled the block graph
 * to its 2-core so that a face pinched by a dead-end spur closes round it instead of being
 * cut short by a chord - and left the spur INSIDE the block with nothing subtracting it.
 * That is why it made the reported symptom twenty times worse on purpose: buildings over a
 * `Street` carriageway went 156 -> 3247 flag off and 191 -> 2986 flag on, in 69 and 68 of
 * the seventy shipped cities, and spurs with their stub under a building 146 -> 4328 and
 * 190 -> 3696.
 *
 * ⚠️ THE GATE THIS FILE EXISTS FOR IS THOSE COUNTS AT ZERO. Everything else here is either
 * a control saying the zero is not vacuous, or a record of what the exclusion moved.
 *
 * ⚠️ THE RULE HAS NO THRESHOLD IN IT, which is §7t.10's answer measured over the world:
 * inset the block by its own Quarter.SidewalkWidth, subtract each carriageway standing
 * inside it widened by that same width, and count the polygons. A shallow spur leaves one
 * polygon with a notch; a deep one leaves two, because the neck was narrower than two
 * pavement widths and the inset the estate was always going to get closed it. Over the
 * blocks that hold a spur the estate comes back in one piece 4757 times in 4780 flag off,
 * which is §7t.10's independent reconstruction to within two blocks.
 *
 * ⚠️ AND THE ORDER OF THE TWO STEPS IS THE ONE THING THAT COULD HAVE BEEN GOT WRONG WITHOUT
 * ANY COUNT MOVING TO ZERO. See TheSubtractionHappensAfterTheInsetAndNotBefore.
 */
public class SpurEstateTests
{
    private const float ClusterSize = 5000f;

    private static readonly object _lo = new();
    private static readonly Dictionary<bool, Measured> _measured = new();


    private sealed class Block
    {
        internal string City;
        internal float SidewalkWidth;
        internal int Pieces;
        internal int PiecesWithoutTheSpur;
        internal bool HoldsASpur;
        internal int Holes;
        internal int Buildings;
        internal double SecondArea, SecondSide;
    }


    private sealed class Measured
    {
        internal int Quarters, Estates, Buildings;
        internal int OverASpur, OverAStructure, OverABoundingStreet, OverAnotherStreet;
        internal int SpurUnderABuilding;
        internal double WorstBoundingSliver;
        internal int Spurs, SpursInsideABlock;
        internal int SelfCrossingOutlines, OutlinesThatAreNotAPieceOfLand;
        internal int StartCityQuarters, StartCityBuildings, StartCityOverAStreet;
        internal List<Block> Blocks = new();
    }


    /**
     * The whole shipped world with its blocks, estates and buildings, read once. Every
     * assertion below is a different reading of the same pass, because the world takes
     * most of a minute to build and there are six of them.
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

                var spurSids = new HashSet<int>(spurs.Select(s => s.Sid));

                var roads = city.Store.GetStrokes()
                    .Where(s => s.Kind == StrokeKind.Street)
                    .Select(s => (S: s, P: BlockGraph.FootprintOf(s, 0f))).ToList();
                var structureFootprints = structures
                    .Select(s => BlockGraph.FootprintOf(s, 0f)).ToList();

                var peeled = city.Store.GetStreetPoints()
                    .Where(sp => !core.Contains(sp) && BlockGraph.ArmCountOf(sp) >= 1)
                    .ToList();

                var blocks = city.Quarters.GetQuarters()
                    .Select(q => (
                        Ring: q.GetDelims().Select(d => d.StartPoint).ToList(), Q: q))
                    .ToList();

                bool isStartCity = city.Cluster.IdString.EndsWith("-0");
                int startOver = 0;

                foreach (var (ring, q) in blocks)
                {
                    ++m.Quarters;
                    m.Estates += q.GetEstates().Count;

                    var estate = q.GetEstates()[0];
                    var land = QuarterGenerator.BuildableLandOf(q, estate, structures, spurs);
                    var withoutTheSpur = QuarterGenerator.BuildableLandOf(
                        q, estate, structures, new List<Stroke>());

                    var block = new Block
                    {
                        City = city.Cluster.IdString,
                        SidewalkWidth = q.SidewalkWidth,
                        Pieces = null == land ? 0 : land.Count(p => p.Count > 0),
                        PiecesWithoutTheSpur = null == withoutTheSpur
                            ? 0 : withoutTheSpur.Count(p => p.Count > 0),
                        HoldsASpur = peeled.Any(
                            sp => BlockRingClosureTests.InsidePlan(ring, sp.Pos))
                    };

                    /*
                     * A hole is what "one building per polygon" would put a house exactly
                     * on top of, so it is counted from the tree Clipper builds rather than
                     * inferred from the flat path list ExcludeCarriageways hands back.
                     */
                    var tree = BlockGraph.DifferenceOf(
                        withoutTheSpur ?? new List<List<IntPoint>>(),
                        QuarterGenerator.Reaching(q, spurs), q.SidewalkWidth);
                    if (null != tree) block.Holes = tree.Childs.Sum(c => c.Childs.Count);

                    if (null != land && block.Pieces >= 2)
                    {
                        var ordered = land.Where(p => p.Count > 0)
                            .OrderByDescending(p => Math.Abs(Clipper.Area(p))).ToList();
                        block.SecondArea = Math.Abs(Clipper.Area(ordered[1])) / 100.0;
                        block.SecondSide = MinSideOf(ordered[1]);
                    }

                    var own = new HashSet<int>(q.GetDelims().Select(d => d.Stroke.Sid));

                    foreach (var e in q.GetEstates())
                    foreach (var b in e.GetBuildings())
                    {
                        ++m.Buildings;
                        ++block.Buildings;

                        var points = b.GetPoints();
                        if (SelfCrossing(points)) ++m.SelfCrossingOutlines;
                        if (!_isAPieceOf(points, land)) ++m.OutlinesThatAreNotAPieceOfLand;

                        var poly = points
                            .Select(v => new IntPoint((int)(v.X * 10f), (int)(v.Z * 10f)))
                            .ToList();
                        var box = _boxOf(poly);

                        foreach (var (s, p) in roads)
                        {
                            if (!_hits(box, _boxOf(p))) continue;

                            double a = Overlap(poly, p);
                            if (a <= 0.5) continue;

                            if (spurSids.Contains(s.Sid)) ++m.OverASpur;
                            else if (own.Contains(s.Sid))
                            {
                                ++m.OverABoundingStreet;
                                m.WorstBoundingSliver = Math.Max(m.WorstBoundingSliver, a);
                            }
                            else
                            {
                                /*
                                 * Neither a spur nor a street this block runs along - a
                                 * third road crossing the block, which nothing here would
                                 * remove. There is no such case today and the arithmetic
                                 * would hide one if it were folded into either count above.
                                 */
                                ++m.OverAnotherStreet;
                            }

                            if (isStartCity) ++startOver;
                        }

                        foreach (var p in structureFootprints)
                        {
                            if (!_hits(box, _boxOf(p))) continue;
                            if (Overlap(poly, p) > 0.5) { ++m.OverAStructure; break; }
                        }
                    }

                    m.Blocks.Add(block);
                }

                foreach (var sp in city.Store.GetStreetPoints())
                {
                    if (1 != BlockGraph.ArmCountOf(sp)) continue;

                    ++m.Spurs;

                    var stub = sp.GetAngleArray().First(BlockGraph.IsBlockEdge);
                    var foot = BlockGraph.FootprintOf(stub, 0f);
                    var footBox = _boxOf(foot);
                    bool isInside = false, isUnder = false;

                    foreach (var (ring, q) in blocks)
                    {
                        if (!BlockRingClosureTests.InsidePlan(ring, sp.Pos)) continue;

                        isInside = true;
                        foreach (var e in q.GetEstates())
                        foreach (var b in e.GetBuildings())
                        {
                            var poly = b.GetPoints()
                                .Select(v => new IntPoint((int)(v.X * 10f), (int)(v.Z * 10f)))
                                .ToList();
                            if (!_hits(_boxOf(poly), footBox)) continue;
                            if (Overlap(poly, foot) > 0.5) isUnder = true;
                        }
                    }

                    if (isInside) ++m.SpursInsideABlock;
                    if (isUnder) ++m.SpurUnderABuilding;
                }

                if (isStartCity)
                {
                    m.StartCityQuarters = blocks.Count;
                    m.StartCityBuildings = blocks.Sum(
                        t => t.Q.GetEstates().Sum(e => e.GetBuildings().Count));
                    m.StartCityOverAStreet = startOver;
                }
            }

            _measured[gradeSeparation] = m;
            return m;
        }
    }


    /*
     * ============================================== the report closes =================
     */

    /**
     * ⚠️ THE REPORT, CLOSED: not one building in the shipped world stands on a dead-end
     * spur, and none stands on a ramp, a bridge or a bore.
     *
     * Over the seventy cities of GenerateClustersOperator's own cluster list, a building's
     * outline against every carriageway of its own city at its BARE width - no margin, so
     * this is "is the house on the road" and not "is the house close to the road". The two
     * populations are named separately because they are inside their block for two
     * different reasons: a spur because WP-O1 peeled it out of the block graph, a structure
     * because WP-B5 took it out (§3c) and the blocks either side merged.
     *
     * ⚠️ WHAT IS LEFT IS NOT THIS CLASS, and it is worth naming rather than rounding away.
     * 13 buildings flag off and 0 flag on overlap a street their own block RUNS ALONG, by
     * 0.9 to 13.4 m², all on blocks with a 2 m pavement. That is §7t's own control seen
     * again: before WP-O1 it counted 10 such slivers flag off and 1 flag on, described as
     * "1-13 m² corner slivers" on blocks whose ring closed - the same size, the same shape,
     * and the same rate against 12 % more blocks. It is the estate's inset meeting the
     * carriageway rectangle near a junction, it predates all of this, and no spur and no
     * structure is involved in any of them.
     *
     * The controls matter as much as the zeros: a city with no buildings, or one where the
     * spurs had stopped standing inside blocks, would satisfy "zero on a road" for reasons
     * that have nothing to do with the exclusion. Both are WP-O1's counts, unmoved.
     */
    [Theory]
    //                   spurs  inside a block  buildings  bounding slivers
    //
    // ⚠️ The building column is superseded by §7y and its old text is 27403 / 26054: the
    // owner's ten square metre floor removes 16 and 195 matchboxes and one flag-on block
    // gains a 1450 m² building it used to lose to one (§7y.5), so the totals are 27387 and
    // 25860. The sliver count does not move - none of the 13 is a matchbox.
    [InlineData(false, 9840, 6422, 27387, 13)]
    [InlineData(true, 8100, 5498, 25860, 0)]
    public void NoBuildingStandsOnASpurOrAStructure(
        bool gradeSeparation, int spurs, int inside, int buildings, int slivers)
    {
        var m = World(gradeSeparation);

        Assert.Equal(0, m.OverASpur);
        Assert.Equal(0, m.OverAStructure);
        Assert.Equal(0, m.SpurUnderABuilding);

        /*
         * ...and no building is over a road that is neither: a third street crossing the
         * block would be removed by nothing here, and folding it into either count above
         * would hide it.
         */
        Assert.Equal(0, m.OverAnotherStreet);

        Assert.Equal(spurs, m.Spurs);
        Assert.Equal(inside, m.SpursInsideABlock);
        Assert.Equal(buildings, m.Buildings);

        Assert.Equal(slivers, m.OverABoundingStreet);
        Assert.True(m.WorstBoundingSliver < 15.0,
            $"worst overlap with a block's own bounding street {m.WorstBoundingSliver:F1} m²");
    }


    /**
     * ⚠️ THE START CITY, BY NAME, because it is the city the owner plays and because WP-O1
     * put the reported picture there for the first time.
     *
     * §7t.7 established that `cluster-clusters-mydear-0` - 1000 m, named Yelukhdidru, where
     * the player stands - had no broken ring, no building over a street and no spur inside
     * any block in either flag state, so the class was established while the instance was
     * not. WP-O1 gave it 5 buildings over a `Street` flag off (worst 1231 m²) and 4 flag on
     * (worst 1630 m²). WP-O2 takes them away again.
     */
    [Theory]
    //                  quarters  buildings
    [InlineData(false, 42, 41)]
    [InlineData(true, 28, 28)]
    public void TheStartCityHasNoBuildingOverAStreet(
        bool gradeSeparation, int quarters, int buildings)
    {
        var m = World(gradeSeparation);

        Assert.Equal(0, m.StartCityOverAStreet);
        Assert.Equal(quarters, m.StartCityQuarters);
        Assert.Equal(buildings, m.StartCityBuildings);
    }


    /*
     * ============================================== notch or split ====================
     */

    /**
     * ⚠️ MOSTLY-NOTCH, AS §7t.10 PREDICTED IT WOULD BE - and the flag-off half lands on that
     * prediction from a different direction.
     *
     * §7t.10 reconstructed the 2-cored block off to one side, with its own face walk and its
     * own copy of the accept rule, and found the estate came back in one piece on 4757 of
     * 4778 blocks flag off. This asks the production expression, on the blocks the game
     * stores, and gets 4757 of 4780 - two blocks and two splits apart. That is the control
     * saying the measurement and the shipped rule are the same rule.
     *
     * ⚠️ THE FLAG-ON HALF DOES NOT LAND ON IT, and the reason is stated rather than
     * absorbed: §7t.10 subtracted only the SPUR, while a flag-on block may also have a
     * ramp, a deck or a bore inside it and the production expression subtracts those too.
     * 348 of these blocks are already in more than one piece before a spur is subtracted at
     * all, against §7t.10's 43 for the inset alone - so the extra splits are structures,
     * not a disagreement about spurs.
     *
     * The whole-world distribution is recorded beside it, because the second estate is a
     * thing the game now builds and nothing else counts them.
     */
    [Theory]
    //                 all blocks: 0    1      2     3    4     spur blocks: total  1     2
    [InlineData(false, 9, 40850, 32, 0, 0, 4780, 4757, 23, 1)]
    [InlineData(true, 522, 35514, 1448, 113, 12, 4444, 3848, 524, 348)]
    public void TheEstateComesBackInOnePieceAlmostEverywhere(
        bool gradeSeparation,
        int none, int one, int two, int three, int four,
        int spurBlocks, int spurOne, int spurTwo, int splitBeforeTheSpur)
    {
        var blocks = World(gradeSeparation).Blocks;

        Assert.Equal(none, blocks.Count(b => 0 == b.Pieces));
        Assert.Equal(one, blocks.Count(b => 1 == b.Pieces));
        Assert.Equal(two, blocks.Count(b => 2 == b.Pieces));
        Assert.Equal(three, blocks.Count(b => 3 == b.Pieces));
        Assert.Equal(four, blocks.Count(b => 4 == b.Pieces));

        var withASpur = blocks.Where(b => b.HoldsASpur).ToList();

        Assert.Equal(spurBlocks, withASpur.Count);
        Assert.Equal(spurOne, withASpur.Count(b => 1 == b.Pieces));
        Assert.Equal(spurTwo, withASpur.Count(b => 2 == b.Pieces));
        Assert.Equal(splitBeforeTheSpur, withASpur.Count(b => b.PiecesWithoutTheSpur > 1));
    }


    /**
     * ⚠️ AND NO PIECE OF BUILDABLE LAND HAS A HOLE IN IT, which is the other thing removing
     * BlockGraph.LargestOf could have got silently wrong.
     *
     * Clipper's flat path output puts a hole in the same list as the piece it is a hole in,
     * distinguished only by its winding, so "one building per polygon" over that list would
     * design a building whose outline is exactly the hole - a house standing precisely on
     * the road that made it. ExcludeCarriageways takes the top-level contours of a PolyTree
     * instead, and this says the distinction never has to be made: a carriageway that stands
     * in a block reaches that block's boundary, because it hangs off a junction ON the ring.
     */
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoPieceOfBuildableLandHasAHoleInIt(bool gradeSeparation)
    {
        var blocks = World(gradeSeparation).Blocks;

        Assert.Equal(0, blocks.Sum(b => b.Holes));

        /*
         * Not vacuous: something was subtracted from something on thousands of blocks.
         */
        Assert.True(blocks.Count(b => b.HoldsASpur) > 1000);
    }


    /**
     * ⚠️ AND WHAT THE ZERO ABOVE IS PROTECTING, ON A BLOCK THAT DOES HAVE A HOLE - because
     * no city has one and no amount of real data can therefore kill the mistake.
     *
     * Mutation testing said so out loud: making ExcludeCarriageways return Clipper's flat
     * path list instead of the top-level contours of its PolyTree passed every gate in this
     * repository, because the two are the same list exactly when the hole count is zero -
     * which is what NoPieceOfBuildableLandHasAHoleInIt asserts. §7o's lesson: data you do
     * not have cannot catch anything.
     *
     * So the branch is reached deliberately. A road that stops short of the block on BOTH
     * sides leaves an island of unbuildable land in the middle of the estate; the flat list
     * comes back with two paths, the outer piece and the hole, and "one building per
     * polygon" over it would design a house whose outline is exactly the hole - standing
     * precisely on the road that made it.
     */
    [Fact]
    public void AHoleIsNotAPieceOfBuildableLand()
    {
        var estate = new List<List<IntPoint>>
        {
            new()
            {
                new IntPoint(0, 0), new IntPoint(0, 4000),
                new IntPoint(4000, 4000), new IntPoint(4000, 0)
            }
        };

        var store = new StrokeStore(ClusterSize);
        var island = _strokeBetween(
            store, _pointAt(150f, 200f), _pointAt(250f, 200f), StrokeKind.Street);

        var tree = BlockGraph.DifferenceOf(estate, new[] { island }, 2f);

        Assert.Single(tree.Childs);
        Assert.Single(tree.Childs[0].Childs);

        /*
         * One piece of buildable land, with the road cut out of the middle of it - not two.
         */
        var land = BlockGraph.ExcludeCarriageways(estate, new[] { island }, 2f);

        Assert.Single(land);
        Assert.Equal(tree.Childs[0].Contour, land[0]);

        /*
         * ...and the piece that comes back is the OUTER one: it reaches the block's own
         * corners, where the hole is a rectangle in the middle. An area test could not tell
         * the two apart on a block small enough.
         */
        Assert.Equal(0, land[0].Min(p => p.X));
        Assert.Equal(4000, land[0].Max(p => p.X));
    }


    /**
     * ⚠️ WHEN IT DOES SPLIT, THE SECOND ESTATE IS WORTH HAVING - §7t.10's Q4, re-asked of
     * the blocks the game now stores.
     *
     * The smaller of two pieces is a median 2280 m² flag off and 5104 m² flag on -
     * comparable with a whole ordinary block - and its shortest side is a median 17.0 and
     * 20.0 m, well clear of the 2 m at which _createBuildings caps a building at one storey.
     * Nothing ever comes back with no points at all, so that function's `mn == 0` return is
     * still never reached.
     *
     * §7t.10 measured 2412 m² and 19.3 m on the flag-off side from its own reconstruction;
     * the flag-on figures are larger here because most flag-on splits are structures rather
     * than spurs (see above), and a deck cuts a block into two large halves where a spur
     * takes a slice off one end.
     */
    [Theory]
    //                second estates  one storey  under 100 m²  empty   area p50     side p50
    [InlineData(false, 32, 4, 2, 0, 1800f, 3000f, 14f, 20f)]
    [InlineData(true, 1573, 295, 27, 0, 4000f, 6000f, 17f, 23f)]
    public void TheSecondEstateIsWorthBuildingOn(
        bool gradeSeparation, int seconds, int oneStorey, int tiny, int empty,
        float areaLo, float areaHi, float sideLo, float sideHi)
    {
        var split = World(gradeSeparation).Blocks.Where(b => b.Pieces >= 2).ToList();

        Assert.Equal(seconds, split.Count);
        Assert.Equal(oneStorey, split.Count(b => b.SecondSide <= 2.0));
        Assert.Equal(tiny, split.Count(b => b.SecondArea < 100.0));
        Assert.Equal(empty, split.Count(b => b.SecondArea <= 0.0));

        Assert.InRange(_median(split.Select(b => b.SecondArea)), areaLo, areaHi);
        Assert.InRange(_median(split.Select(b => b.SecondSide)), sideLo, sideHi);
    }


    /*
     * ============================================== every piece is kept ===============
     */

    /**
     * ⚠️ EVERY BUILDING'S OUTLINE IS ONE PIECE OF ITS BLOCK'S BUILDABLE LAND, EXACTLY.
     *
     * This is the assertion that removes both of the things WP-O2 deleted. Until it,
     * _createBuildings concatenated every polygon of the inset into a single ring - its own
     * TXWTODO said so and it had done it since the file was written - which is a
     * self-crossing outline the moment there are two of them, with one building designed
     * across the pair and minHouseSide measured across the gap between them. And
     * BlockGraph.LargestOf existed to keep that from happening, by throwing every piece but
     * the biggest away.
     *
     * Asked as an identity - the building's points are one of BuildableLandOf's polygons,
     * reversed, corner for corner - rather than as a count or an area, because a count
     * cannot tell a building designed on one piece from a building designed on the union of
     * two, and an area cannot tell it from a building on the largest.
     *
     * ⚠️ AND ONE THING THAT IS NOT ASSERTED AT ZERO: 1 building of 27387 flag off and 2 of
     * 25860 flag on have an outline that touches itself. Those are pinches at Clipper's own
     * decimetre - two parts of one contour meeting within 0.1 m where the inset all but
     * split the block - and Clipper returns them as one self-touching path rather than two
     * polygons (Clipper.SimplifyPolygon splits the flag-off one in two). Two of the three
     * come back identically with no spur subtracted at all, so it is the pavement inset's
     * own pinch, §14.4's class at the resolution limit, and not this work package's.
     *
     * ⚠️ SUPERSEDED IN ITS FIRST COLUMN BY §7y, old text recorded: it read
     *
     *      [InlineData(false, 13, 1)] [InlineData(true, 793, 2)]
     *
     * and the owner's ten square metre floor takes two flag-on blocks from two buildings to
     * one, so "blocks with more than one building" is 791. The self-touching count does not
     * move: neither of those outlines is a matchbox.
     */
    [Theory]
    //                blocks with >1 building  self-touching outlines
    [InlineData(false, 13, 1)]
    [InlineData(true, 791, 2)]
    public void EveryBuildingIsOnePieceOfItsBlocksBuildableLand(
        bool gradeSeparation, int multi, int selfTouching)
    {
        var m = World(gradeSeparation);

        Assert.Equal(0, m.OutlinesThatAreNotAPieceOfLand);

        /*
         * Not vacuous: blocks that carry more than one building exist, so "each building is
         * a piece" is a statement about several pieces and not about one.
         */
        Assert.Equal(multi, m.Blocks.Count(b => b.Buildings > 1));
        Assert.True(m.Blocks.Any(b => b.Buildings > 0 && b.Pieces > b.Buildings));

        Assert.Equal(selfTouching, m.SelfCrossingOutlines);
    }


    /**
     * The building's outline IS one of these polygons, walked backwards.
     *
     * _createBuildings reverses the points before handing them to the Building, and has
     * since it was written - the winding is what ExtrudePoly builds the walls from - so the
     * reversal is part of the identity rather than something to normalise away.
     */
    private static bool _isAPieceOf(List<Vector3> points, List<List<IntPoint>> land)
    {
        if (null == land) return false;

        foreach (var polygon in land)
        {
            if (polygon.Count != points.Count) continue;

            bool same = true;
            for (int i = 0; i < points.Count && same; ++i)
            {
                var p = polygon[polygon.Count - 1 - i];
                same = points[i].X == p.X / 10f
                       && points[i].Y == 0f
                       && points[i].Z == p.Y / 10f;
            }

            if (same) return true;
        }

        return false;
    }


    /*
     * ============================================== the order of the two steps ========
     */

    /**
     * ⚠️ THE ONE THING THAT COULD HAVE BEEN GOT WRONG WITHOUT ANY COUNT MOVING TO ZERO: the
     * subtraction goes AFTER the inset, where BlockGraph.ExcludeStructures already is, and
     * not before it.
     *
     * Both halves of the rule are the block's own Quarter.SidewalkWidth - the estate is
     * inset by it and the corridor is widened by it - so the order decides how much land is
     * left round the tip of a spur. Insetting first leaves (tip to boundary) − 2 ×
     * SidewalkWidth; subtracting first insets the NOTCH as well, taking a third pavement
     * width out of the same land bridge. Both orders remove the spur, so both drive the
     * report to zero; they disagree about how often the block comes back in two pieces, and
     * over the seventy shipped cities they disagree by eight times (21 / 150 against
     * 163 / 646, §7t.10.2). Nothing in the tree stated which order was intended.
     *
     * Driven as the THRESHOLD rather than as one case, because a single spur depth can only
     * say that the two answers differ somewhere. The shallowest spur that still leaves one
     * piece is measured under each order, and the two differ by exactly one pavement width -
     * which is the mechanism rather than a symptom of it, and which also pins both uses of
     * SidewalkWidth: a corridor widened by nothing would put the production threshold at one
     * pavement width instead of two.
     */
    [Fact]
    public void TheSubtractionHappensAfterTheInsetAndNotBefore()
    {
        var cluster = StreetHarness.MakeCluster("order", ClusterSize);

        /*
         * A rectangular block wide enough that the only place a spur can pinch its estate
         * is round its own tip.
         */
        const float w = 400f, h = 400f;
        var quarter = _blockOf(cluster, new List<Vector2>
        {
            new(0f, 0f), new(0f, h), new(w, h), new(w, 0f)
        });

        float sidewalk = quarter.SidewalkWidth;
        Assert.True(sidewalk > 0f);

        var estate = new Estate { ClusterDesc = cluster };
        estate.AddPoints(quarter.GetDelims()
            .Select(d => new Vector3(d.StartPoint.X, 0f, d.StartPoint.Y)).ToList());

        var outline = estate.GetPoints()
            .Select(v => new IntPoint((int)(v.X * 10f), (int)(v.Z * 10f))).ToList();

        /*
         * The shallowest spur - measured from the far side of the block - that still leaves
         * the estate in one piece, under each order. Stepped by a decimetre, which is
         * Clipper's own resolution here.
         */
        float production = -1f, reversed = -1f;
        for (float depth = 0.5f; depth <= 5f * sidewalk; depth += 0.1f)
        {
            var spur = _spurInto(h, depth, w / 2f);

            if (production < 0f
                && 1 == QuarterGenerator.BuildableLandOf(
                    quarter, estate, new List<Stroke>(), new List<Stroke> { spur }).Count)
            {
                production = depth;
            }

            if (reversed < 0f && 1 == _subtractThenInset(outline, spur, sidewalk).Count)
            {
                reversed = depth;
            }
        }

        Assert.InRange(production, 2f * sidewalk - 0.15f, 2f * sidewalk + 0.15f);
        Assert.InRange(reversed, 3f * sidewalk - 0.15f, 3f * sidewalk + 0.15f);

        /*
         * Said as the difference too, so a reader of a failure sees what the order is worth
         * rather than two numbers that happen not to match.
         */
        Assert.InRange(reversed - production, sidewalk - 0.2f, sidewalk + 0.2f);
    }


    /**
     * The other order: the spur taken out of the block's OUTLINE, and what is left inset.
     * The control for the test above and nothing else - no production code does this.
     */
    private static List<List<IntPoint>> _subtractThenInset(
        List<IntPoint> outline, Stroke spur, float sidewalk)
    {
        var clipper = new Clipper();
        clipper.AddPath(outline, PolyType.ptSubject, true);
        clipper.AddPath(BlockGraph.FootprintOf(spur, sidewalk), PolyType.ptClip, true);

        var tree = new PolyTree();
        clipper.Execute(ClipType.ctDifference, tree,
            PolyFillType.pftNonZero, PolyFillType.pftNonZero);

        var result = new List<List<IntPoint>>();
        foreach (var child in tree.Childs)
        {
            var offset = new ClipperOffset();
            offset.AddPath(child.Contour, JoinType.jtMiter, EndType.etClosedPolygon);

            var solution = new List<List<IntPoint>>();
            offset.Execute(ref solution, -sidewalk * 10f);
            result.AddRange(solution);
        }

        return result;
    }


    /*
     * ============================================== which roads are subtracted ========
     */

    /**
     * ⚠️ THE SPUR SET IS THE COMPLEMENT OF THE PEEL, exactly - so a road the block trace
     * runs ALONG is never taken out of the estate, and a road the trace refuses always is.
     *
     * The two are written from the same two terms (BlockGraph.AcceptWithin and
     * BlockGraph.SpurCorridorsOf) and this says out loud that they cannot disagree. Getting
     * it wrong either way is silent: name too few and a building stands on a spur again;
     * name too many and every block loses the streets that bound it.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void ASpurCorridorIsExactlyAnArmThePeelRemoved(string idString, float size)
    {
        foreach (var store in new[]
                 {
                     StreetHarness.Generate(idString, size),
                     StreetHarness.GenerateHeavyFirst(idString, size)
                 })
        {
            var core = BlockGraph.TwoCoreOf(store);
            var accept = BlockGraph.AcceptWithin(core);
            var spurs = new HashSet<Stroke>(BlockGraph.SpurCorridorsOf(store, core));

            foreach (var s in store.GetStrokes())
            {
                if (!BlockGraph.IsBlockEdge(s))
                {
                    /*
                     * A ramp, a deck or a bore is neither: ExcludeStructures owns it, and
                     * naming it here as well would subtract it twice.
                     */
                    Assert.DoesNotContain(s, spurs);
                    continue;
                }

                Assert.Equal(!accept(s), spurs.Contains(s));
            }
        }
    }


    /**
     * ⚠️ AND A ConnectorBridge IS A SPUR WHEN IT IS ONE, which is the opposite of the rule
     * ExcludeStructures applies to the same stroke kind - deliberately, and the two
     * statements are about the same thing seen from two sides.
     *
     * A ConnectorBridge is an ordinary ground road that exists in every shipped flat city
     * (§0.7). ExcludeStructures refuses to touch one because there it is a road a block is
     * BOUNDED by, and cutting a hole out of a block for one would change the default city.
     * Here it is a road standing INSIDE a block, and a building on it is the reported
     * symptom whatever the stroke's Kind says.
     */
    [Fact]
    public void AConnectorBridgeLeadingNowhereIsASpur()
    {
        var store = new StrokeStore(ClusterSize);

        var a = _pointAt(0f, 0f);
        var b = _pointAt(120f, 0f);
        var c = _pointAt(120f, 110f);
        var d = _pointAt(0f, 110f);
        var tip = _pointAt(60f, 60f);

        _strokeBetween(store, a, b, StrokeKind.Street);
        _strokeBetween(store, b, c, StrokeKind.Street);
        _strokeBetween(store, c, d, StrokeKind.Street);
        _strokeBetween(store, d, a, StrokeKind.Street);

        var stub = _strokeBetween(store, a, tip, StrokeKind.ConnectorBridge);

        var core = BlockGraph.TwoCoreOf(store);
        var spurs = BlockGraph.SpurCorridorsOf(store, core);

        Assert.Single(spurs);
        Assert.Same(stub, spurs[0]);

        /*
         * ...and ExcludeStructures still refuses it, on the same stroke, so the two rules
         * are asserted against each other rather than each on its own fixture.
         */
        var estate = new List<List<IntPoint>>
        {
            new()
            {
                new IntPoint(0, 0), new IntPoint(1200, 0),
                new IntPoint(1200, 1100), new IntPoint(0, 1100)
            }
        };

        Assert.Same(estate, BlockGraph.ExcludeStructures(estate, spurs, 2f));
        Assert.NotSame(estate, BlockGraph.ExcludeCarriageways(estate, spurs, 2f));
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


    /**
     * A block with the given outline, as a real Quarter - so that Quarter.SidewalkWidth is
     * the block's own number, read off the AABB its delimiters build, exactly as the
     * production block reads it.
     */
    private static Quarter _blockOf(ClusterDesc cluster, List<Vector2> ring)
    {
        var store = new StrokeStore(ClusterSize);
        var quarter = new Quarter { ClusterDesc = cluster };

        var points = ring.Select(v => _pointAt(v.X, v.Y)).ToList();

        for (int i = 0; i < points.Count; ++i)
        {
            var s = _strokeBetween(
                store, points[i], points[(i + 1) % points.Count], StrokeKind.Street);

            var delim = new QuarterDelim();
            delim.SetEdge(ring[i], points[i], s);
            quarter.AddQuarterDelim(delim);
        }

        return quarter;
    }


    /**
     * A dead-end spur hanging into a block from the edge at y = top, whose tip stops
     * `depth` short of the block's far side at y = 0.
     */
    private static Stroke _spurInto(float top, float depth, float x)
    {
        var store = new StrokeStore(ClusterSize);

        return _strokeBetween(store, _pointAt(x, top), _pointAt(x, depth), StrokeKind.Street);
    }


    private static StreetPoint _pointAt(float x, float y)
    {
        var sp = new StreetPoint { ClusterId = 0, Level = 0 };
        sp.SetPos(x, y);
        return sp;
    }


    private static Stroke _strokeBetween(
        StrokeStore store, StreetPoint a, StreetPoint b, StrokeKind kind)
    {
        var s = new Stroke
        {
            ClusterId = 0, IsPrimary = false, Weight = 1f, Kind = kind, Level = 0
        };
        s.A = a;
        s.B = b;
        store.AddStroke(s);
        return s;
    }


    internal static bool SelfCrossing(List<Vector3> ring)
    {
        int n = ring.Count;
        for (int i = 0; i < n; ++i)
        for (int j = i + 2; j < n; ++j)
        {
            if (0 == i && j == n - 1) continue;

            if (_crosses(
                    _plan(ring[i]), _plan(ring[(i + 1) % n]),
                    _plan(ring[j]), _plan(ring[(j + 1) % n])))
            {
                return true;
            }
        }

        return false;
    }


    private static Vector2 _plan(in Vector3 v) => new(v.X, v.Z);


    private static bool _crosses(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        static double Cross(Vector2 p, Vector2 q) => (double)p.X * q.Y - (double)p.Y * q.X;

        Vector2 r = b - a, s = d - c;
        double denominator = Cross(r, s);
        if (Math.Abs(denominator) < 1e-9) return false;

        double t = Cross(c - a, s) / denominator;
        double u = Cross(c - a, r) / denominator;

        return t > 1e-6 && t < 1.0 - 1e-6 && u > 1e-6 && u < 1.0 - 1e-6;
    }


    internal static float MinSideOf(List<IntPoint> poly)
    {
        float min = Single.MaxValue;
        for (int i = 0; i < poly.Count; ++i)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            float dx = (b.X - a.X) / 10f, dy = (b.Y - a.Y) / 10f;
            min = Single.Min(min, MathF.Sqrt(dx * dx + dy * dy));
        }

        return poly.Count > 0 ? min : 0f;
    }


    private static double _median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(x => x).ToList();
        return 0 == sorted.Count ? Double.NaN : sorted[sorted.Count / 2];
    }


    private static (long, long, long, long) _boxOf(List<IntPoint> poly)
    {
        long x0 = Int64.MaxValue, y0 = Int64.MaxValue;
        long x1 = Int64.MinValue, y1 = Int64.MinValue;
        foreach (var p in poly)
        {
            x0 = Math.Min(x0, p.X); x1 = Math.Max(x1, p.X);
            y0 = Math.Min(y0, p.Y); y1 = Math.Max(y1, p.Y);
        }

        return (x0, y0, x1, y1);
    }


    private static bool _hits((long, long, long, long) a, (long, long, long, long) b)
        => !(a.Item3 < b.Item1 || a.Item1 > b.Item3 || a.Item4 < b.Item2 || a.Item2 > b.Item4);


    private static double Overlap(List<IntPoint> poly, List<IntPoint> footprint)
    {
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
