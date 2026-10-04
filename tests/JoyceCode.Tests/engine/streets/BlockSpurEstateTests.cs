using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * ⚠️ ONE ESTATE WITH A NOTCH IN IT, OR TWO ESTATES EITHER SIDE OF THE SPUR? Measured over
 * the shipped world, and the answer is not close.
 *
 * §7t established what the reported street-through-a-building is: a dead-end spur cuts a
 * slit into a city block, QuarterGenerator's face walk stops half way round because it
 * terminates on a VERTEX, and the ring is closed by a chord across the slit. §7t.5 left
 * two repairs and the owner a decision. This file measures the one the owner then
 * proposed on top of repair (b) - peel the block graph to its 2-core, so a spur is not a
 * block edge, and then decide per block whether the estate is one piece with a notch or
 * two pieces.
 *
 * NOTHING IS FIXED HERE. No file under JoyceCode is touched and nothing below is wired
 * into the game; SpurBlocks reconstructs the 2-cored block off to one side, out of the
 * production expressions, and this file records what came back.
 *
 * ⚠️ THE HEADLINE IS THAT THE POLICY IS ALMOST ALWAYS "NOTCH": 4757 of 4778 blocks flag
 * off and 4289 of 4439 flag on come back as ONE polygon. See
 * TheEstateComesBackInOnePieceAlmostEverywhere.
 *
 * ⚠️ THE SECOND HEADLINE IS THE BLAST RADIUS, and it is 20x what §7t's numbers suggest.
 * §7t counted 222 / 272 spurs inside a stored block. But a third of all faces are
 * discarded today (§7t.4), so most spurs stand in ground that has no block at all - and
 * the 2-core gives every one of them a block. Repair (b) therefore touches 4778 / 4439
 * blocks, of which 4559 / 4167 are holes in the city today. See
 * TheTwoCoreGivesABlockToGroundThatHasNoneToday.
 *
 * ⚠️ AND THE STRUCTURAL CLAIM IS HALF RIGHT, which is a finding rather than a footnote.
 * "A dead-end spur cannot split a polygon by subtraction alone" is true 9998 times in
 * 10000 and NOT universally (ADeadEndCorridorHardlyEverSplitsTheBlockOnItsOwn); and where
 * the estate does come back in two, the land bridge that closed is the one round the tip
 * in only 7 of 21 and 78 of 150 cases (TheSplitIsNotUsuallyAtTheSpurTip). The conclusion
 * the claim was made for still stands - no new constant is needed, the pavement width the
 * estate is already inset by decides it - but the mechanism it names is not the one doing
 * most of the work.
 */
public class BlockSpurEstateTests
{
    private static readonly object _lo = new();
    private static readonly Dictionary<bool, Measured> _measured = new();


    private sealed class Measured
    {
        internal int CoreFaces;
        internal int SpurFreeFaces;
        internal int StoredQuarters;
        internal int ClosedStoredQuarters;
        internal int Tips;
        internal int TipsInsideAFace;
        internal List<SpurBlocks.BlockInfo> Blocks = new();
    }


    /**
     * The whole shipped world, 2-cored, with every block that has a spur standing in it
     * measured. One pass, cached, because the world itself takes half a minute to build
     * and every assertion below is a different reading of the same pass.
     *
     * The height table is the shipped terrain's - ShippedTerrain.RelaxedHeightsOf, i.e.
     * what RelaxedStreetHeight over TerrainStreetHeight answers in the game - because
     * joyce.DisableClusterFlattening defaults to true and that is the city being shipped.
     * §7t showed the flag-off plan NETWORK is the same graph flat or on terrain, so the
     * flag-off half is the flat city's blocks with the shipped ground under them.
     */
    private static Measured World(bool gradeSeparation)
    {
        lock (_lo)
        {
            if (_measured.TryGetValue(gradeSeparation, out var cached)) return cached;

            var m = new Measured();
            foreach (var city in BlockRingClosureTests.World(gradeSeparation))
            {
                var report = SpurBlocks.Analyse(
                    city, ShippedTerrain.RelaxedHeightsOf(city.Cluster, city.Store));

                m.CoreFaces += report.CoreFaces;
                m.SpurFreeFaces += report.SpurFreeFaces;
                m.Tips += report.Tips;
                m.TipsInsideAFace += report.TipsInsideAFace;
                m.StoredQuarters += city.Quarters.GetQuarters().Count;
                m.ClosedStoredQuarters += city.Quarters.GetQuarters()
                    .Count(q => !BlockRingClosureTests.RingIsBroken(q));

                m.Blocks.AddRange(report.Blocks);
            }

            _measured[gradeSeparation] = m;
            return m;
        }
    }


    /*
     * ============================================== Q1: the blast radius =============
     */

    /**
     * ⚠️ HOW MANY BLOCKS REPAIR (b) TOUCHES, and it is not the 222 / 272 §7t.3a counted.
     *
     * §7t asked "is this spur inside a stored quarter" and got 222 / 272, because a face
     * that touches a spur is discarded for hasNullSection and most spurs therefore stand
     * in ground with no block on it. The 2-core has no such face: peeling the spur out of
     * the block graph leaves a ring that closes, so EVERY one of these gets a block.
     *
     * 4778 / 4439 blocks, of which 4559 / 4167 are holes in the city today. That is the
     * cost side of §7t.4's third-of-the-city read forwards: repair (b) is not a repair of
     * 222 blocks, it is 4778 new ones - and the 219 / 272 that do exist today are exactly
     * §7t's broken rings, which is the control that says the two files are counting the
     * same thing.
     *
     * ⚠️ SUPERSEDED IN PART BY WP-O1 (§7u), NOT RE-BASELINED. When this was written the
     * production trace still ran over the whole block graph, so a spur was inside a stored
     * quarter on only 219 / 272 of these blocks and 4559 / 4167 were holes in the pavement.
     * WP-O1 peeled the block graph to its 2-core, which is exactly what this file
     * reconstructed, so those two counts are now "all of them" and "none". What it held:
     *
     *      in a stored quarter today         219      272
     *      ...whose ring is broken           219      271
     *      in NO stored quarter (a hole)    4559     4167
     *      spur-free faces against stored      5       12  (i.e. five/twelve apart)
     *
     * ⚠️ THE CONTROL IS STRONGER NOW AND IT IS THE POINT OF KEEPING THIS FILE. The
     * reconstruction here is written independently of QuarterGenerator - its own face walk
     * over its own copy of the accept rule - so "every 2-cored face is a stored quarter,
     * and every stored quarter is a 2-cored face" says the production trace and this
     * measurement agree face for face. That is what makes every notch/split number below
     * a statement about the city the game now builds rather than about a hypothetical.
     */
    [Theory]
    //                  faces  spur-free  blocks   tips  inside
    [InlineData(false, 40891, 36113, 4778, 9840, 6422)]
    [InlineData(true, 37609, 33170, 4439, 8100, 5498)]
    public void TheTwoCoreGivesABlockToGroundThatHadNoneBeforeWpO1(
        bool gradeSeparation, int faces, int spurFree, int blocks, int tips, int tipsInside)
    {
        var m = World(gradeSeparation);

        Assert.Equal(faces, m.CoreFaces);
        Assert.Equal(spurFree, m.SpurFreeFaces);
        Assert.Equal(blocks, m.Blocks.Count);
        Assert.Equal(faces, spurFree + blocks);

        Assert.Equal(tips, m.Tips);
        Assert.Equal(tipsInside, m.TipsInsideAFace);

        /*
         * Every one of these blocks is a block the game stores now, none of them stands in
         * a broken ring because there are none, and the reconstruction has exactly as many
         * faces as the production trace has quarters.
         */
        Assert.Equal(blocks, m.Blocks.Count(b => b.InAStoredBlock));
        Assert.Equal(0, m.Blocks.Count(b => b.InABrokenStoredBlock));
        Assert.Equal(0, m.Blocks.Count(b => !b.InAStoredBlock));

        Assert.Equal(m.StoredQuarters, m.CoreFaces);
        Assert.Equal(m.StoredQuarters, m.ClosedStoredQuarters);
    }


    /*
     * ============================================== Q2: the headline ==================
     */

    /**
     * ⚠️ THE ANSWER, AND IT IS MOSTLY-NOTCH: 4757 of 4778 and 4289 of 4439 estates come
     * back as ONE polygon with a slot cut out of it. Two pieces in 21 and 142, three in 0
     * and 8, and nothing ever comes back empty.
     *
     * So the policy the owner asked about is not a balance to be struck. It is a rule with
     * a rare exception, and the exception announces itself: Clipper returns two polygons.
     *
     * ⚠️ THE ORDER OF THE TWO STEPS MATTERS AND THE PRODUCTION ORDER IS THE ONE MEASURED.
     * _createBuildings insets first and BlockGraph.ExcludeStructures subtracts afterwards,
     * so that is what is counted here. Subtracting first and insetting the remainder insets
     * the notch as well, which widens it by a second pavement width and turns 163 / 646
     * blocks into two - eight times as many, from the same geometry. If repair (b) is
     * built, it goes where ExcludeStructures already is.
     *
     * ⚠️ 43 of the 150 flag-on splits are not the spur's doing at all: the inset ALONE
     * already splits those blocks, which is §14.4's pre-existing pinch (4 of 714 on the
     * pinned seeds) seen over the world. BlockGraph.LargestOf throws the second piece away
     * today.
     */
    [Theory]
    //                   one   two  three  zero  inset alone  alt: two  alt: three+
    [InlineData(false, 4757, 21, 0, 0, 1, 163, 14)]
    [InlineData(true, 4289, 142, 8, 0, 43, 646, 113)]
    public void TheEstateComesBackInOnePieceAlmostEverywhere(
        bool gradeSeparation, int one, int two, int three, int zero, int insetAlone,
        int altTwo, int altThreeOrMore)
    {
        var blocks = World(gradeSeparation).Blocks;

        Assert.Equal(one, blocks.Count(b => 1 == b.Pieces));
        Assert.Equal(two, blocks.Count(b => 2 == b.Pieces));
        Assert.Equal(three, blocks.Count(b => 3 == b.Pieces));
        Assert.Equal(zero, blocks.Count(b => 0 == b.Pieces));

        Assert.Equal(insetAlone, blocks.Count(b => b.InsetPieces > 1));

        Assert.Equal(altTwo, blocks.Count(b => 2 == b.AltPieces));
        Assert.Equal(altThreeOrMore, blocks.Count(b => b.AltPieces >= 3));
    }


    /**
     * ⚠️ THE STRUCTURAL CLAIM'S FIRST HALF IS ALMOST TRUE AND NOT QUITE, and "not quite"
     * is worth a line because it is the half that was offered as certain.
     *
     * *"A dead-end spur cannot split a polygon by subtraction alone - the corridor stops at
     * the tip, so the land wraps round the tip and one polygon comes back with a slot in
     * it."* Subtract each spur's BARE carriageway from the block outline, no inset, no
     * margin, and two polygons come back on **9 of 4778** blocks flag off and **11 of
     * 4439** flag on. Widen the carriageway by the one pavement width the estate would use
     * and it is 13 and 26.
     *
     * The exceptions are what they sound like: a block with several spur trees in it whose
     * carriageways meet in the middle - 5 of the 9 and 3 of the 11 have more than one root
     * on the ring - and a corridor whose own carriageway is wide enough to reach the
     * block's far side. Neither needs a new rule, because both come back as two polygons
     * and the rule is to count polygons; but "cannot" is the wrong word and the count is
     * recorded so nobody builds on it.
     */
    [Theory]
    //                 bare: two  bare: two with >1 root   widened: two  widened: three+
    [InlineData(false, 9, 5, 13, 0)]
    [InlineData(true, 11, 3, 25, 1)]
    public void ADeadEndCorridorHardlyEverSplitsTheBlockOnItsOwn(
        bool gradeSeparation, int bareTwo, int bareTwoMultiRoot, int widenedTwo,
        int widenedThreeOrMore)
    {
        var blocks = World(gradeSeparation).Blocks;

        Assert.Equal(bareTwo, blocks.Count(b => b.BareSubtractPieces >= 2));
        Assert.Equal(bareTwoMultiRoot,
            blocks.Count(b => b.BareSubtractPieces >= 2 && b.Roots > 1));

        Assert.Equal(widenedTwo, blocks.Count(b => 2 == b.SubtractOnlyPieces));
        Assert.Equal(widenedThreeOrMore, blocks.Count(b => b.SubtractOnlyPieces >= 3));
    }


    /*
     * ============================================== Q3: the sensitivity ===============
     */

    /**
     * ⚠️ THE SPLIT IS NOT USUALLY AT THE SPUR TIP, which is the other half of the
     * structural claim and it does not hold.
     *
     * The claim's mechanism is that the land bridge round the tip is (tip to block
     * boundary) less two pavement widths, and that the estate splits when that goes
     * negative. Measured along the spur's own direction, that gap is NEVER negative on a
     * block that comes back in one piece - 0 of 4757 and 0 of 4289, which is the half that
     * holds - but it is also positive on 12 of 21 and 139 of 150 blocks that DO split, by
     * a median 5.6 and 28.9 m.
     *
     * Asked the other way round, from the estate rather than from the tip: the narrowest
     * gate between the two pieces lies beyond the tip - i.e. it IS the land bridge round
     * the tip - on 7 of 21 and 78 of 150. The rest are pinched somewhere along the
     * corridor's flank, where a spur runs close to a block edge or two spurs run towards
     * each other.
     *
     * ⚠️ It does not change the answer to the owner's question, and that is the point of
     * measuring it. The rule is still "inset, then count polygons", and counting polygons
     * does not care where the split is. What is refuted is the reasoning, not the rule -
     * and a rule believed for a reason that is false is one ruleset change away from being
     * believed for nothing.
     */
    [Theory]
    //                one-piece with a closed gap  split with an open gap  gate beyond tip
    [InlineData(false, 0, 12, 7, 21)]
    [InlineData(true, 0, 139, 78, 150)]
    public void TheSplitIsNotUsuallyAtTheSpurTip(
        bool gradeSeparation, int onePieceClosed, int splitOpen, int gateBeyond, int splits)
    {
        var blocks = World(gradeSeparation).Blocks;

        Assert.Equal(onePieceClosed,
            blocks.Count(b => 1 == b.Pieces && b.AxialGap <= 0f));
        Assert.Equal(splitOpen, blocks.Count(b => b.Pieces >= 2 && b.AxialGap > 0f));

        Assert.Equal(splits, blocks.Count(b => b.Pieces >= 2));
        Assert.Equal(gateBeyond, blocks.Count(b => b.GateBeyondTip));
    }


    /**
     * ⚠️ DOES THE VERDICT FLIP ON A DECIMETRE? For the notch, no. For the split, yes, in
     * a third of its own cases.
     *
     * Both halves of the rule ARE Quarter.SidewalkWidth - the estate is inset by it and the
     * corridor would be widened by it - so moving that one number moves the whole rule, and
     * how far it has to move before the answer changes is the whole sensitivity of the
     * policy. Measured per block, by bisection, wherever in the block the narrowest place
     * happens to be.
     *
     * A block that comes back as ONE piece is nowhere near the decision: the pavement would
     * have to be a median 9.4 / 7.1 m wider, and only 6 of 4757 and 91 of 4289 would flip
     * within half a metre.
     *
     * ⚠️ A block that comes back as TWO is a different story: 4 of 21 and 58 of 150 become
     * one piece again if the pavement is half a metre narrower, and 15 of the 150 flag-on
     * splits turn on a single decimetre. So the minority verdict is the fragile one - which
     * is an argument for the notch being the DEFAULT and the split being the exception it
     * already is, and against reading any individual split as a design statement.
     *
     * 117 / 122 blocks never split however wide the pavement is made, up to the 32 m cap
     * the bisection stops at.
     */
    [Theory]
    //               notch: <0.5  <1   <2   never   split: >-0.1  >-0.25  >-0.5  >-1
    [InlineData(false, 6, 14, 41, 117, 3, 3, 4, 5)]
    [InlineData(true, 91, 184, 413, 122, 15, 34, 58, 86)]
    public void OnlyTheSplitVerdictIsFragile(
        bool gradeSeparation, int notchHalf, int notchOne, int notchTwo, int notchNever,
        int splitDecimetre, int splitQuarter, int splitHalf, int splitOne)
    {
        var blocks = World(gradeSeparation).Blocks;

        var notch = blocks.Where(b => 1 == b.Pieces).Select(b => b.FlipMargin).ToList();
        var split = blocks.Where(b => b.Pieces >= 2).Select(b => b.FlipMargin).ToList();

        Assert.Equal(notchHalf, notch.Count(x => x < 0.5f));
        Assert.Equal(notchOne, notch.Count(x => x < 1f));
        Assert.Equal(notchTwo, notch.Count(x => x < 2f));
        Assert.Equal(notchNever, notch.Count(x => x >= SpurBlocks.FlipMarginCap));

        Assert.Equal(splitDecimetre, split.Count(x => x > -0.1f));
        Assert.Equal(splitQuarter, split.Count(x => x > -0.25f));
        Assert.Equal(splitHalf, split.Count(x => x > -0.5f));
        Assert.Equal(splitOne, split.Count(x => x > -1f));

        /*
         * Not vacuous in the other direction either: no notch is within a millimetre of
         * splitting and no split within a millimetre of closing up, so the bisection is
         * measuring a distance rather than reporting its own bracket.
         */
        Assert.True(notch.All(x => x > 0f));
        Assert.True(split.All(x => x < 0f));
    }


    /**
     * The distributions behind the two tests above, as bounds rather than as floats -
     * a percentile of a Clipper offset over seventy generated cities is not a number to
     * pin to the bit, and what the decision needs is the shape.
     */
    [Theory]
    //                    gap p50    flip p50 (notch)   flip p50 (split)
    [InlineData(false, 45f, 60f, 8f, 11f, -3f, -1.5f)]
    [InlineData(true, 38f, 48f, 6f, 8f, -1.2f, -0.4f)]
    public void TheShapeOfThoseDistributions(
        bool gradeSeparation, float gapLo, float gapHi, float notchLo, float notchHi,
        float splitLo, float splitHi)
    {
        var blocks = World(gradeSeparation).Blocks;

        float gap = _median(blocks.Where(b => 1 == b.Pieces).Select(b => b.AxialGap));
        float notch = _median(blocks.Where(b => 1 == b.Pieces).Select(b => b.FlipMargin));
        float split = _median(blocks.Where(b => b.Pieces >= 2).Select(b => b.FlipMargin));

        Assert.InRange(gap, gapLo, gapHi);
        Assert.InRange(notch, notchLo, notchHi);
        Assert.InRange(split, splitLo, splitHi);
    }


    /*
     * ============================================== Q4: is it worth having ============
     */

    /**
     * ⚠️ WHEN IT DOES SPLIT, THE SECOND ESTATE IS WORTH HAVING - which is the other thing
     * the decision needs, because a second estate that yields a shed is a lot of blast
     * radius for nothing.
     *
     * The smaller of the two pieces is a median 2412 m² flag off and 1671 m² flag on -
     * comparable with a whole ordinary block - and its shortest side is a median 19.3 and
     * 18.1 m. QuarterGenerator._createBuildings caps a building at one storey when
     * minHouseSide <= 2 m, and that bites on 2 of 21 and 11 of 150. Nothing comes back
     * with no points at all, so the `mn == 0` return is never reached.
     *
     * The tail is real and small: 1 of 21 and 20 of 150 second pieces are under 100 m².
     */
    [Theory]
    //           one storey  not downtown  under 100 m²  empty   area p50      side p50
    [InlineData(false, 2, 2, 1, 0, 1800f, 3200f, 15f, 25f)]
    [InlineData(true, 11, 0, 20, 0, 1200f, 2200f, 14f, 23f)]
    public void TheSecondEstateIsBigEnoughToBuildOn(
        bool gradeSeparation, int oneStorey, int notDowntown, int tiny, int empty,
        float areaLo, float areaHi, float sideLo, float sideHi)
    {
        var splits = World(gradeSeparation).Blocks.Where(b => b.Pieces >= 2).ToList();

        Assert.Equal(oneStorey, splits.Count(b => b.SmallMinSide <= 2.0f));

        /*
         * The other way _createBuildings caps a building at one storey, and it is the
         * block's own downtownness rather than anything about the piece - recorded so that
         * "the second estate carries a real building" is not read past the two blocks
         * where it would have been one storey anyway.
         */
        Assert.Equal(notDowntown, splits.Count(b => b.Downtownness < 0.3f));
        Assert.Equal(tiny, splits.Count(b => b.SmallArea < 100f));
        Assert.Equal(empty, splits.Count(b => b.SmallArea <= 0f));

        Assert.InRange(_median(splits.Select(b => b.SmallArea)), areaLo, areaHi);
        Assert.InRange(_median(splits.Select(b => b.SmallMinSide)), sideLo, sideHi);

        /*
         * ...and the larger piece is always the larger one, which is what makes "smaller"
         * mean anything above.
         */
        Assert.True(splits.All(b => b.LargeArea >= b.SmallArea));
    }


    /*
     * ============================================== Q5: how many spurs per block ======
     */

    /**
     * ⚠️ A QUARTER OF THESE BLOCKS HOLD MORE THAN ONE SPUR - 1161 of 4778 and 811 of 4439,
     * up to eight and nine tips in one block. A policy phrased as "one estate either side
     * of the spur" therefore has to mean three or more estates a quarter of the time, and
     * it has no way to say which side is which.
     *
     * Counting polygons has no such problem, which is the third reason to prefer it: the
     * question "how many estates" is answered by the geometry rather than by the policy.
     *
     * The pinch §7t.2 is about is nearly absent from the 2-cored ring, which is what
     * peeling is for: the spur's root junction appears twice on its own block's ring 3
     * times in 6220 and once in 5333, and only 21 / 5 blocks revisit any junction at all.
     */
    [Theory]
    //              one tip  >1 tip  most tips  roots  roots twice  rings that revisit
    [InlineData(false, 3617, 1161, 8, 6220, 3, 21)]
    [InlineData(true, 3628, 811, 9, 5333, 1, 5)]
    public void ABlockOftenHoldsMoreThanOneSpur(
        bool gradeSeparation, int oneTip, int manyTips, int mostTips, int roots,
        int rootsTwice, int revisiting)
    {
        var blocks = World(gradeSeparation).Blocks;

        Assert.Equal(oneTip, blocks.Count(b => 1 == b.Tips.Count));
        Assert.Equal(manyTips, blocks.Count(b => b.Tips.Count > 1));
        Assert.Equal(mostTips, blocks.Max(b => b.Tips.Count));

        Assert.Equal(roots, blocks.Sum(b => b.Roots));
        Assert.Equal(rootsTwice, blocks.Sum(b => b.RootsTwice));
        Assert.Equal(revisiting, blocks.Count(b => b.RingRevisits));
    }


    /*
     * ============================================== Q6: the coupling ==================
     */

    /**
     * ⚠️ THE COUPLING THAT COMES WITH A SECOND ESTATE, AND IT IS NOT SMALL.
     *
     * BuildingFooting.BaseHeightOf answers the BLOCK's lowest corner and everything on the
     * block stands on it. Its justification is written down and is about to stop being
     * true: *"a block carries exactly one estate and at most one building"*, which is what
     * made the block-wide minimum only 0.19-0.61 m below the exact footprint minimum at the
     * median.
     *
     * Split the block in two and the higher piece is buried by the difference between the
     * two pieces' own lowest corners. On the shipped terrain that is a median 7.5 m flag
     * off and 6.2 m flag on, up to 21.2 and 28.1 m, and it is over 5 m on 12 of 21 and 84
     * of 150 blocks.
     *
     * So a "two estates" policy cannot be built without giving each estate its own footing.
     * Measured rather than argued, on the shipped ground, because the flat city has no
     * corner spread at all and would have said the coupling costs nothing.
     */
    [Theory]
    //                over 0.5 m  over 2 m  over 5 m   median range
    [InlineData(false, 20, 17, 12, 5f, 10f)]
    [InlineData(true, 145, 123, 84, 4f, 9f)]
    public void TheBlockWideFloorWouldBuryTheHigherOfTwoEstates(
        bool gradeSeparation, int overHalf, int overTwo, int overFive,
        float medianLo, float medianHi)
    {
        var burial = World(gradeSeparation).Blocks
            .Where(b => b.Pieces >= 2 && b.ExtraBurial >= 0f)
            .Select(b => b.ExtraBurial)
            .ToList();

        Assert.Equal(overHalf, burial.Count(x => x > 0.5f));
        Assert.Equal(overTwo, burial.Count(x => x > 2f));
        Assert.Equal(overFive, burial.Count(x => x > 5f));

        Assert.InRange(_median(burial), medianLo, medianHi);
    }


    private static float _median(IEnumerable<float> values)
    {
        var sorted = values.OrderBy(x => x).ToList();
        return 0 == sorted.Count ? Single.NaN : sorted[sorted.Count / 2];
    }
}
