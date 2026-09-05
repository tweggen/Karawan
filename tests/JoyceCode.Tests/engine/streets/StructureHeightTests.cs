using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using engine.streets;
using engine.streets.generation;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * WP-B3a — a grade separated structure is designed, and the relaxation settles around it.
 *
 * ⚠️ **No generated city contains a structure**, so real data cannot catch anything in
 * this file and every case below is a fixture. That is the same standing situation
 * WP-B1 worked in, and §9.2 of the plan records its sharper form: a branch reached by
 * no test and no recorded city passes every gate in the repository.
 *
 * The fixture is one corridor, straight along +X, with an ordinary street running away
 * from each foot:
 *
 *     W ------- F1 --ramp-- D1 ==deck== D2 --ramp-- F2 ------- E
 *     -300       0          100         200        300         600
 *
 * The corridor's weight is 1.3, the heaviest the ruleset builds, so the policy holds an
 * ordinary street of it to 5 % - and the ramps are built at 10 %. Everything in here
 * therefore depends on the relaxer NOT being allowed to iron the structure flat, rather
 * than on it happening to leave the structure alone.
 */
public class StructureHeightTests
{
    private const float ClusterSize = 2000f;
    private const float CorridorWeight = 1.3f;

    /**
     * 80 m is the shortest ramp that can lift a deck at MaxRampGrade; 100 m gives the
     * fixture a climb (10 m) that differs from the deck height (8 m), so an expression
     * that confused the two would come out wrong rather than merely equal.
     */
    private const float RampLength = 100f;


    private sealed class Fixture
    {
        internal global::engine.world.ClusterDesc Cluster;
        internal StrokeStore Store;
        internal StreetPoint West, Foot1, Deck1, Deck2, Foot2, East;
        internal Stroke ApproachWest, RampUp, Deck, RampDown, ApproachEast;
        internal Dictionary<int, float> Terrain;
    }


    private static StreetPoint _pointAt(float x, float y, sbyte level = 0)
    {
        var sp = new StreetPoint() { ClusterId = 0, Level = level };
        sp.SetPos(x, y);
        return sp;
    }


    private static Stroke _street(
        global::engine.world.ClusterDesc cd, StreetPoint a, StreetPoint b, float weight,
        StrokeKind kind, sbyte level)
    {
        var s = new Stroke()
        {
            ClusterId = cd.Id,
            IsPrimary = true,
            Weight = weight,
            Kind = kind,
            Level = level
        };
        s.A = a;
        s.B = b;
        s.PushCreator("fixture");
        return s;
    }


    /**
     * @param deckKind
     *     Bridge to go over, Tunnel to go under.
     * @param groundLevel
     *     Which deck the structure's feet stand on. Not always 0 on purpose: a foot on
     *     level 0 contributes LevelElevation == 0, so a profile that forgot to add the
     *     foot's own deck elevation would be right by coincidence on every structure
     *     the game will build first.
     * @param structureKind
     *     Ramp/deck as they really are, or Street throughout - which is B3a.5's control.
     */
    private static Fixture _corridor(
        StrokeKind deckKind, sbyte groundLevel, bool asStreets,
        float westHeight, float foot1Height, float foot2Height, float eastHeight)
    {
        var cd = StreetHarness.MakeCluster("structure", ClusterSize);
        var store = new StrokeStore(ClusterSize);

        sbyte deckLevel = deckKind == StrokeKind.Tunnel
            ? (sbyte)(groundLevel - 1)
            : (sbyte)(groundLevel + 1);

        var f = new Fixture { Cluster = cd, Store = store };

        f.West = _pointAt(-300f, 0f, groundLevel);
        f.Foot1 = _pointAt(0f, 0f, groundLevel);
        f.Deck1 = _pointAt(RampLength, 0f, asStreets ? groundLevel : deckLevel);
        f.Deck2 = _pointAt(RampLength + 100f, 0f, asStreets ? groundLevel : deckLevel);
        f.Foot2 = _pointAt(2f * RampLength + 100f, 0f, groundLevel);
        f.East = _pointAt(2f * RampLength + 400f, 0f, groundLevel);

        StrokeKind rampKind = asStreets ? StrokeKind.Street : StrokeKind.Ramp;
        StrokeKind spanKind = asStreets ? StrokeKind.Street : deckKind;

        f.ApproachWest = _street(cd, f.West, f.Foot1, CorridorWeight, StrokeKind.Street, groundLevel);
        f.RampUp = _street(cd, f.Foot1, f.Deck1, CorridorWeight, rampKind, groundLevel);
        f.Deck = _street(cd, f.Deck1, f.Deck2, CorridorWeight, spanKind,
            asStreets ? groundLevel : deckLevel);
        f.RampDown = _street(cd, f.Deck2, f.Foot2, CorridorWeight, rampKind, groundLevel);
        f.ApproachEast = _street(cd, f.Foot2, f.East, CorridorWeight, StrokeKind.Street, groundLevel);

        foreach (var s in new[]
                 { f.ApproachWest, f.RampUp, f.Deck, f.RampDown, f.ApproachEast })
        {
            store.AddStroke(s);
        }

        /*
         * Keyed after every AddStroke - a point's Id changes when it joins a store, and
         * GradeRelaxerTests records what happens to a table keyed before that.
         */
        f.Terrain = new Dictionary<int, float>
        {
            [f.West.Id] = westHeight,
            [f.Foot1.Id] = foot1Height,
            /*
             * Whatever the noise put under a bridge. StructureProfile overwrites both of
             * these, and a fixture that seeded them with the designed answer could not
             * tell "designed" from "left alone".
             */
            [f.Deck1.Id] = -17f,
            [f.Deck2.Id] = 44f,
            [f.Foot2.Id] = foot2Height,
            [f.East.Id] = eastHeight
        };

        return f;
    }


    /**
     * The default fixture: a bridge on the ground deck, over terrain that falls away to
     * the west and climbs hard to the east, so both approaches are well over the 5 % an
     * arterial is allowed and the relaxer has real work to do at both feet.
     */
    private static Fixture _bridge()
        => _corridor(StrokeKind.Bridge, 0, asStreets: false,
            westHeight: -60f, foot1Height: 0f, foot2Height: 8f, eastHeight: 100f);


    private static float _world(StreetPoint sp, Dictionary<int, float> heights)
        => heights[sp.Id] + sp.LevelElevation;


    private static float _grade(Stroke s, Dictionary<int, float> heights)
        => (_world(s.B, heights) - _world(s.A, heights)) / s.Length;


    /*
     * ---------------------------------------------------------------- B3a.1 --------
     */

    /**
     * B3a.1. Every junction a structure touches comes out of the relaxation at exactly
     * the height it went in with.
     *
     * Exact equality, not a tolerance: "immovable" is an identity claim, and a
     * tolerance would be satisfied by a junction the relaxer moved a little.
     */
    [Fact]
    public void AStructureJunctionIsImmovableUnderRelaxation()
    {
        var f = _bridge();
        var heights = new Dictionary<int, float>(f.Terrain);

        var boundary = _anchored(f);

        GradeRelaxer.Relax(f.Store.GetStrokes(), heights, new GradePolicy());

        foreach (var sp in new[] { f.Foot1, f.Deck1, f.Deck2, f.Foot2 })
        {
            Assert.Equal(boundary[sp.Id], heights[sp.Id]);
        }
    }


    /**
     * The boundary VALUE, rebuilt out of its two pieces: relax the city as if the
     * structure were not there, then design the structure onto the result. Anything the
     * relaxation then does to a structure junction shows up as a difference from this.
     *
     * Written here rather than read out of the relaxer so that the two are independent -
     * a test that asked the relaxer what it had pinned could not tell an immovable
     * junction from one it moved and then reported.
     */
    private static Dictionary<int, float> _anchored(Fixture f)
    {
        var anchored = new Dictionary<int, float>(f.Terrain);

        GradeRelaxer.Relax(
            f.Store.GetStrokes().Where(s => !StrokeKinds.IsStructure(s.Kind)).ToList(),
            anchored, new GradePolicy());

        StructureProfile.Design(
            f.Store.GetStrokes().OrderBy(s => s.Sid), anchored, new GradePolicy());

        return anchored;
    }


    /**
     * ...and the correction they refuse does not vanish: the free end of an approach
     * absorbs all of it, so the approach ends up at exactly the grade its weight allows.
     *
     * The half that a "did the structure move" test cannot see. A relaxer that simply
     * dropped every stroke touching a pinned junction would pass the test above and
     * leave both approaches at the 13 % and 31 % the terrain gave them.
     */
    [Fact]
    public void TheNeighboursAbsorbTheCorrectionTheStructureRefuses()
    {
        var f = _bridge();
        var policy = new GradePolicy();

        Assert.True(Single.Abs(_grade(f.ApproachWest, f.Terrain)) > 3f * policy.MaxGradeFor(f.ApproachWest),
            "the fixture must start with unbuildable approaches");
        Assert.True(Single.Abs(_grade(f.ApproachEast, f.Terrain)) > 3f * policy.MaxGradeFor(f.ApproachEast));

        /*
         * Driven through the sweep itself rather than through Relax, because Relax
         * anchors first and an anchored network has no unbuildable approach left for
         * the sweep to fix. The sweep is reached with an unsettled approach and a pinned
         * foot in every real city - MaxSweeps is 32 and the anchor pass over a
         * thousand-junction graph does not converge in 32 - but that is a property of
         * a city's size rather than something a six-junction fixture can show, so the
         * boundary rule is measured where it lives.
         */
        var ordered = f.Store.GetStrokes().OrderBy(s => s.Sid).ToList();
        var heights = new Dictionary<int, float>(f.Terrain);
        StructureProfile.Design(ordered, heights, policy);

        float footWest = heights[f.Foot1.Id];
        float footEast = heights[f.Foot2.Id];

        GradeRelaxer.RelaxAround(
            ordered, heights, policy, StructureProfile.PinnedJunctionsOf(ordered),
            policy.MaxSweeps);

        foreach (var s in new[] { f.ApproachWest, f.ApproachEast })
        {
            Assert.Equal(policy.MaxGradeFor(s), Single.Abs(_grade(s, heights)), 3);
        }

        /*
         * And it was the free end that did all of the moving: the feet did not budge.
         */
        Assert.Equal(footWest, heights[f.Foot1.Id]);
        Assert.Equal(footEast, heights[f.Foot2.Id]);
        Assert.NotEqual(f.Terrain[f.West.Id], heights[f.West.Id]);
        Assert.NotEqual(f.Terrain[f.East.Id], heights[f.East.Id]);
    }


    /**
     * ⚠️ The free end takes the WHOLE excess, not the share the resistance split would
     * have given it.
     *
     * Arithmetic, so that "absorb" means something a test can fail on. The west approach
     * is 300 m at weight 1.3, which the policy holds to 5 %, so a limit of 15 m; it
     * starts 60 m out of level, an excess of 45 m; the fixture's busiest junction has two
     * strokes, so one sweep applies half of whatever it was given. Handing the free end
     * the whole excess puts it at -60 + 22.5; handing it its half share - both ends carry
     * weight 1.3, so the split is 50/50 - would put it at -60 + 11.25, and the stroke
     * would creep to its limit over a dozen sweeps instead of reaching it.
     *
     * Which is why the settled version of this cannot catch it: after 32 sweeps the
     * geometric series has run and both rules agree to five decimal places.
     *
     * ⚠️ BOTH approaches, because the pinned end is `B` on one of them and `A` on the
     * other and those are two lines of code. Asserting only the west one leaves the east
     * rule free to keep the split - which is exactly what happened, and is the same shape
     * as §7q's symmetric-survivor lesson: an assertion that exercises one arm of a
     * mirrored pair says nothing about the other.
     */
    [Fact]
    public void AFreeEndTakesTheWholeExcessAndNotItsShare()
    {
        var f = _bridge();
        var policy = new GradePolicy { MaxSweeps = 1 };

        var ordered = f.Store.GetStrokes().OrderBy(s => s.Sid).ToList();
        var heights = new Dictionary<int, float>(f.Terrain);
        StructureProfile.Design(ordered, heights, policy);

        Assert.Same(f.Foot1, f.ApproachWest.B);
        Assert.Same(f.Foot2, f.ApproachEast.A);

        GradeRelaxer.RelaxAround(
            ordered, heights, policy, StructureProfile.PinnedJunctionsOf(ordered), 1);

        Assert.Equal(-60f + 45f / 2f, heights[f.West.Id], 3);

        /*
         * The east approach is 300 m from a foot at 8 m to ground at 100 m: 92 m of rise
         * against a 15 m limit, an excess of 77, halved by the same damping.
         */
        Assert.Equal(100f - 77f / 2f, heights[f.East.Id], 3);
    }


    /**
     * ...and the same sweep with an EMPTY boundary splits the correction between the two
     * ends, which is what it has always done and what the shipped city depends on.
     *
     * The control for the rule above: without it, "the free end absorbed everything"
     * could be satisfied by a sweep that simply hands every correction to the B end.
     */
    [Fact]
    public void WithNoBoundaryTheSweepStillSplitsACorrectionBetweenBothEnds()
    {
        var f = _bridge();
        var policy = new GradePolicy();

        var ordered = f.Store.GetStrokes()
            .Where(s => !StrokeKinds.IsStructure(s.Kind)).OrderBy(s => s.Sid).ToList();
        var heights = new Dictionary<int, float>(f.Terrain);

        GradeRelaxer.RelaxAround(ordered, heights, policy, new HashSet<int>(), policy.MaxSweeps);

        Assert.NotEqual(f.Terrain[f.West.Id], heights[f.West.Id]);
        Assert.NotEqual(f.Terrain[f.Foot1.Id], heights[f.Foot1.Id]);
        Assert.NotEqual(f.Terrain[f.East.Id], heights[f.East.Id]);
        Assert.NotEqual(f.Terrain[f.Foot2.Id], heights[f.Foot2.Id]);
    }


    /*
     * ---------------------------------------------------------------- B3a.5 --------
     */

    /**
     * B3a.5, the positive control. The SAME geometry, the SAME weights and the SAME
     * starting heights - the designed profile itself - with the three structure strokes
     * declared as ordinary streets instead. Now the junctions move.
     *
     * Seeded with the designed heights on purpose. A control that started from raw
     * terrain would differ from the real case in two ways at once (no design AND no
     * pin) and could not say which of them held the structure still.
     */
    [Fact]
    public void WithoutPinningTheSameStructureJunctionsDoMove()
    {
        var real = _bridge();
        var designed = _anchored(real);

        var control = _corridor(StrokeKind.Bridge, 0, asStreets: true,
            westHeight: -60f, foot1Height: 0f, foot2Height: 8f, eastHeight: 100f);

        /*
         * The two fixtures are built the same way in the same order, so their junctions
         * correspond one for one - asserted rather than assumed, since a pairing by
         * position that silently failed would make the control vacuous.
         */
        var realPoints = new[] { real.West, real.Foot1, real.Deck1, real.Deck2, real.Foot2, real.East };
        var controlPoints = new[]
            { control.West, control.Foot1, control.Deck1, control.Deck2, control.Foot2, control.East };

        var heights = new Dictionary<int, float>();
        for (int i = 0; i < realPoints.Length; ++i)
        {
            Assert.Equal(realPoints[i].Pos, controlPoints[i].Pos);
            heights[controlPoints[i].Id] = designed[realPoints[i].Id];
        }

        GradeRelaxer.Relax(control.Store.GetStrokes(), heights, new GradePolicy());

        foreach (int i in new[] { 1, 2, 3, 4 })
        {
            Assert.NotEqual(designed[realPoints[i].Id], heights[controlPoints[i].Id]);
        }
    }


    /**
     * The control, stated the other way round: what pinning actually buys is that the
     * ramp keeps its grade. Without it the relaxation pulls the ramp down toward the 5 %
     * an arterial is held to.
     */
    [Fact]
    public void WithoutPinningTheRampIsFlattenedTowardTheStreetLimit()
    {
        var policy = new GradePolicy();

        var control = _corridor(StrokeKind.Bridge, 0, asStreets: true,
            westHeight: -60f, foot1Height: 0f, foot2Height: 8f, eastHeight: 100f);

        var designed = new Dictionary<int, float>(control.Terrain);
        designed[control.Deck1.Id] = 0f + policy.MaxRampGrade * RampLength;
        designed[control.Deck2.Id] = 8f + policy.MaxRampGrade * RampLength;

        Assert.Equal(policy.MaxRampGrade, Single.Abs(_grade(control.RampUp, designed)), 4);

        /*
         * Left to settle - 500 sweeps rather than the shipped 32 - the unpinned ramp
         * comes to rest at 5.1 %, which is the grade this corridor's weight of 1.3
         * entitles an ordinary street to and not the 10 % it was built at.
         */
        var settled = new Dictionary<int, float>(designed);
        GradeRelaxer.Relax(
            control.Store.GetStrokes(), settled, new GradePolicy { MaxSweeps = 500 });

        Assert.Equal(
            policy.MaxGradeFor(control.ApproachWest),
            Single.Abs(_grade(control.RampUp, settled)), 2);

        /*
         * And under the budget the game actually runs it is already most of the way
         * there - 6.7 % - so this is not an artefact of the longer run.
         */
        var shipped = new Dictionary<int, float>(designed);
        GradeRelaxer.Relax(control.Store.GetStrokes(), shipped, policy);

        float after = Single.Abs(_grade(control.RampUp, shipped));
        Assert.True(after < 0.75f * policy.MaxRampGrade,
            $"an unpinned ramp came out at {after:F4}, so the relaxer was never going to "
            + "flatten it and the pinning test proves nothing");
    }


    /*
     * ---------------------------------------------------------------- B3a.3 --------
     */

    /**
     * B3a.3. Measured on the relaxed heights, on the WORLD profile - ground plus the
     * junction's own deck elevation - because that is what a vehicle drives on.
     */
    [Theory]
    [InlineData(StrokeKind.Bridge, (sbyte)0)]
    [InlineData(StrokeKind.Tunnel, (sbyte)0)]
    [InlineData(StrokeKind.Bridge, (sbyte)1)]
    [InlineData(StrokeKind.Tunnel, (sbyte)2)]
    [InlineData(StrokeKind.Bridge, (sbyte)(-1))]
    public void APinnedRampCarriesExactlyTheGradeItWasDesignedFor(StrokeKind deckKind, sbyte groundLevel)
    {
        var f = _corridor(deckKind, groundLevel, asStreets: false,
            westHeight: -60f, foot1Height: 0f, foot2Height: 8f, eastHeight: 100f);

        var heights = new Dictionary<int, float>(f.Terrain);
        var policy = new GradePolicy();

        GradeRelaxer.Relax(f.Store.GetStrokes(), heights, policy);

        float expected = deckKind == StrokeKind.Tunnel
            ? -policy.MaxRampGrade
            : policy.MaxRampGrade;

        /*
         * RampUp leaves its foot; RampDown arrives at its foot, so its A-to-B grade is
         * the opposite sign. Both are the same climb away from the ground deck.
         */
        Assert.Equal(expected, _grade(f.RampUp, heights), 5);
        Assert.Equal(-expected, _grade(f.RampDown, heights), 5);
    }


    /**
     * ...and the design itself, as an identity rather than as a number close to one.
     *
     * The expression is written out here in the terms it is made of - the foot's ground
     * height, the foot's own deck elevation, the climb, and the deck end's deck
     * elevation - so that a profile which dropped one of those terms fails on the term
     * rather than on a tolerance. Dropping foot.LevelElevation is invisible on a
     * structure whose feet are on the ground, which is why groundLevel varies here.
     */
    [Theory]
    [InlineData(StrokeKind.Bridge, (sbyte)0)]
    [InlineData(StrokeKind.Tunnel, (sbyte)0)]
    [InlineData(StrokeKind.Bridge, (sbyte)1)]
    [InlineData(StrokeKind.Tunnel, (sbyte)2)]
    public void ADeckEndStandsOneRampAboveItsOwnFoot(StrokeKind deckKind, sbyte groundLevel)
    {
        var f = _corridor(deckKind, groundLevel, asStreets: false,
            westHeight: -60f, foot1Height: 0f, foot2Height: 8f, eastHeight: 100f);

        var heights = new Dictionary<int, float>(f.Terrain);
        var policy = new GradePolicy();
        float sign = deckKind == StrokeKind.Tunnel ? -1f : 1f;

        GradeRelaxer.Relax(f.Store.GetStrokes(), heights, policy);

        Assert.Equal(
            heights[f.Foot1.Id] + f.Foot1.LevelElevation
            + sign * policy.MaxRampGrade * f.RampUp.Length - f.Deck1.LevelElevation,
            heights[f.Deck1.Id]);

        Assert.Equal(
            heights[f.Foot2.Id] + f.Foot2.LevelElevation
            + sign * policy.MaxRampGrade * f.RampDown.Length - f.Deck2.LevelElevation,
            heights[f.Deck2.Id]);
    }


    /**
     * ⚠️ A structure's FEET stand where the city without the structure stands, and NOT
     * where the raw terrain sample under them is.
     *
     * The anchor pass. Measured over six generated cities on the shipped terrain, the
     * two differ by up to 27.1 m at a single foot, so this is not a refinement: without
     * it the structure drags its approaches, the blocks cornering on them and the
     * buildings on those down into the noise the relaxation exists to remove.
     */
    [Fact]
    public void TheFeetStandWhereTheCityWithoutTheStructureStands()
    {
        var f = _bridge();
        var heights = new Dictionary<int, float>(f.Terrain);

        var withoutTheStructure = new Dictionary<int, float>(f.Terrain);
        GradeRelaxer.Relax(
            f.Store.GetStrokes().Where(s => !StrokeKinds.IsStructure(s.Kind)).ToList(),
            withoutTheStructure, new GradePolicy());

        GradeRelaxer.Relax(f.Store.GetStrokes(), heights, new GradePolicy());

        Assert.Equal(withoutTheStructure[f.Foot1.Id], heights[f.Foot1.Id]);
        Assert.Equal(withoutTheStructure[f.Foot2.Id], heights[f.Foot2.Id]);

        /*
         * ...and that is not the same thing as the terrain, which is the whole point.
         */
        Assert.NotEqual(f.Terrain[f.Foot1.Id], heights[f.Foot1.Id]);
        Assert.NotEqual(f.Terrain[f.Foot2.Id], heights[f.Foot2.Id]);
    }


    /**
     * The other half of the anchor pass, and the property it buys: adding a structure
     * to a city does not move the city.
     *
     * Every ordinary junction comes out exactly where it came out without the structure.
     * A single pass that pinned the feet at their raw terrain heights fails this by
     * tens of metres.
     */
    [Fact]
    public void AddingAStructureDoesNotMoveTheCityAroundIt()
    {
        var f = _bridge();

        var withStructure = new Dictionary<int, float>(f.Terrain);
        GradeRelaxer.Relax(f.Store.GetStrokes(), withStructure, new GradePolicy());

        var withoutStructure = new Dictionary<int, float>(f.Terrain);
        GradeRelaxer.Relax(
            f.Store.GetStrokes().Where(s => !StrokeKinds.IsStructure(s.Kind)).ToList(),
            withoutStructure, new GradePolicy());

        foreach (var sp in new[] { f.West, f.Foot1, f.Foot2, f.East })
        {
            Assert.Equal(withoutStructure[sp.Id], withStructure[sp.Id]);
        }
    }


    /**
     * The deck between the two ramps is whatever the two feet make it, and this test
     * exists to say that OUT LOUD rather than to leave it implied.
     *
     * ⚠️ It is not bounded by anything. Two feet 8 m apart in height 100 m of deck apart
     * give an 8 % span; the same two feet on a real hillside can give far worse, and
     * nothing in WP-B3a refuses it - refusing a corridor is WP-B3b's job and it needs
     * this number to do it with.
     */
    [Fact]
    public void TheDeckSpanFollowsItsTwoFeetAndIsNotItselfLimited()
    {
        var policy = new GradePolicy();

        var f = _corridor(StrokeKind.Bridge, 0, asStreets: false,
            westHeight: -40f, foot1Height: 0f, foot2Height: 40f, eastHeight: 100f);

        var heights = new Dictionary<int, float>(f.Terrain);
        GradeRelaxer.Relax(f.Store.GetStrokes(), heights, policy);

        float span = _grade(f.Deck, heights);

        /*
         * Exactly the difference between the two FEET, spread over the deck: the two
         * ramps climb the same amount from each of them, so the deck carries whatever
         * they disagree by and nothing else.
         */
        Assert.Equal(
            (heights[f.Foot2.Id] - heights[f.Foot1.Id]) / f.Deck.Length, span, 5);
        Assert.True(Single.Abs(span) > policy.MaxRampGrade,
            $"the fixture was supposed to produce a deck steeper than a ramp may be, "
            + $"and it came out at {span:F4}");
    }


    /**
     * The sweep budget is one allowance over the whole relaxation, not one per pass.
     *
     * WP-B2.6 made the same call about the generation budget. Here it is what makes
     * AddingAStructureDoesNotMoveTheCityAroundIt true on a real city: GradeRelaxer
     * exhausts all 32 sweeps on every generated network, so an anchor pass with its own
     * allowance would hand the whole city a second relaxation and settle it further -
     * measured at up to 7.5 m on Yelukhdidru@3000 before the budget was shared.
     */
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void TheSweepBudgetIsSpentOncePerRelaxationAndNotPerPass(int allowance)
    {
        var f = _bridge();

        /*
         * An allowance the anchor pass alone spends, which is the case a real city is
         * in - so the second pass gets nothing, and a second pass with its own
         * allowance would show up here as one sweep too many.
         */
        var policy = new GradePolicy { MaxSweeps = allowance };

        var heights = new Dictionary<int, float>(f.Terrain);
        int sweeps = GradeRelaxer.Relax(f.Store.GetStrokes(), heights, policy);

        Assert.Equal(allowance, sweeps);

        /*
         * ...and a second pass with no sweeps left still leaves the structure designed
         * and standing, rather than half applied.
         */
        var policyForDesign = new GradePolicy { MaxSweeps = allowance };
        Assert.Equal(
            heights[f.Foot1.Id] + f.Foot1.LevelElevation
            + policyForDesign.MaxRampGrade * f.RampUp.Length - f.Deck1.LevelElevation,
            heights[f.Deck1.Id]);
    }


    /*
     * ------------------------------------------------ the policy and the malformed --
     */

    /**
     * A ramp's grade does not interpolate over weight, and every other kind's does.
     *
     * ConnectorBridge is in here because a rule phrased against "not a Street" would
     * catch it, and one to three of them exist in every shipped city (§0.7).
     */
    [Theory]
    [InlineData(StrokeKind.Street, false)]
    [InlineData(StrokeKind.ConnectorBridge, false)]
    [InlineData(StrokeKind.Bridge, false)]
    [InlineData(StrokeKind.Tunnel, false)]
    [InlineData(StrokeKind.Ramp, true)]
    public void OnlyARampIsGradedAsARamp(StrokeKind kind, bool isRamp)
    {
        var cd = StreetHarness.MakeCluster("policy", ClusterSize);
        var policy = new GradePolicy();

        var light = _street(cd, _pointAt(0f, 0f), _pointAt(100f, 0f), policy.WeightMin, kind, 0);
        var heavy = _street(cd, _pointAt(0f, 0f), _pointAt(100f, 0f), policy.WeightMax, kind, 0);

        if (isRamp)
        {
            Assert.Equal(policy.MaxRampGrade, policy.MaxGradeFor(light));
            Assert.Equal(policy.MaxRampGrade, policy.MaxGradeFor(heavy));
        }
        else
        {
            Assert.Equal(policy.MaxGradeAtMinWeight, policy.MaxGradeFor(light), 6);
            Assert.Equal(policy.MaxGradeAtMaxWeight, policy.MaxGradeFor(heavy), 6);
        }
    }


    /**
     * The pinned set is every junction of a structure, deck span included - not only the
     * ones a ramp touches.
     */
    [Fact]
    public void EveryJunctionOfAStructureIsPinnedAndNothingElseIs()
    {
        var f = _bridge();

        var pinned = StructureProfile.PinnedJunctionsOf(f.Store.GetStrokes());

        Assert.Equal(
            new[] { f.Foot1.Id, f.Deck1.Id, f.Deck2.Id, f.Foot2.Id }.OrderBy(i => i),
            pinned.OrderBy(i => i));
    }


    /**
     * A network with no structure in it pins nothing and writes nothing - the case
     * every city the shipped ruleset builds is in.
     */
    [Fact]
    public void ANetworkWithNoStructureIsLeftEntirelyAlone()
    {
        var control = _corridor(StrokeKind.Bridge, 0, asStreets: true,
            westHeight: -60f, foot1Height: 0f, foot2Height: 8f, eastHeight: 100f);

        var heights = new Dictionary<int, float>(control.Terrain);

        Assert.Empty(StructureProfile.PinnedJunctionsOf(control.Store.GetStrokes()));

        StructureProfile.Design(
            control.Store.GetStrokes().OrderBy(s => s.Sid), heights, new GradePolicy());

        Assert.Equal(control.Terrain, heights);
    }


    /**
     * A ramp whose two ends are on one deck climbs nothing, so there is no foot to
     * design from. It is left where the terrain put it - and it is still PINNED, because
     * a malformed structure the relaxer then bends into shape is worse than one that
     * visibly stands where it was put.
     */
    [Fact]
    public void ARampThatDoesNotChangeLevelIsLeftAloneAndStillPinned()
    {
        var cd = StreetHarness.MakeCluster("malformed", ClusterSize);
        var store = new StrokeStore(ClusterSize);

        var a = _pointAt(0f, 0f);
        var b = _pointAt(100f, 0f);
        store.AddStroke(_street(cd, a, b, CorridorWeight, StrokeKind.Ramp, 0));

        var heights = new Dictionary<int, float> { [a.Id] = 0f, [b.Id] = 60f };
        var pinned = StructureProfile.PinnedJunctionsOf(store.GetStrokes());
        StructureProfile.Design(
            store.GetStrokes().OrderBy(s => s.Sid), heights, new GradePolicy());

        Assert.Equal(new[] { a.Id, b.Id }.OrderBy(i => i), pinned.OrderBy(i => i));
        Assert.Equal(0f, heights[a.Id]);
        Assert.Equal(60f, heights[b.Id]);

        GradeRelaxer.Relax(store.GetStrokes(), heights, new GradePolicy());

        Assert.Equal(0f, heights[a.Id]);
        Assert.Equal(60f, heights[b.Id]);
    }


    /**
     * Two ramps arriving at one deck junction design it from two different feet. The
     * first by Sid keeps it, so the answer does not depend on enumeration order.
     *
     * A hump rather than a bridge: two feet, one summit. Nothing builds this today,
     * which is exactly why it is here - the branch would otherwise be reached by
     * nothing, and §9.2 of the plan is what that costs.
     */
    [Fact]
    public void WhenTwoRampsClaimOneDeckJunctionTheFirstBySidWins()
    {
        var cd = StreetHarness.MakeCluster("hump", ClusterSize);
        var store = new StrokeStore(ClusterSize);

        var footWest = _pointAt(0f, 0f);
        var summit = _pointAt(100f, 0f, 1);
        var footEast = _pointAt(200f, 0f);

        var first = _street(cd, footWest, summit, CorridorWeight, StrokeKind.Ramp, 0);
        var second = _street(cd, footEast, summit, CorridorWeight, StrokeKind.Ramp, 0);
        store.AddStroke(first);
        store.AddStroke(second);

        Assert.True(first.Sid < second.Sid, "the fixture depends on the order they were added");

        var heights = new Dictionary<int, float>
        {
            [footWest.Id] = 0f, [summit.Id] = -99f, [footEast.Id] = 25f
        };

        var policy = new GradePolicy();
        StructureProfile.Design(store.GetStrokes().OrderBy(s => s.Sid), heights, policy);

        Assert.Equal(
            0f + policy.MaxRampGrade * first.Length - summit.LevelElevation,
            heights[summit.Id]);
    }


    /**
     * ⚠️ Only a RAMP is given a ramp's profile, and a deck straddling two levels is not
     * quietly turned into one.
     *
     * The fixture is deliberately malformed - a Bridge filed on level 0 whose far end is
     * on level 1 - because a well-formed bridge cannot tell the two rules apart:
     * OverpassBuilder puts both of a deck's ends on the deck's own level, so a profile
     * that accepted any structure would find neither end a foot and refuse anyway. That
     * equivalence is exactly why widening the guard to StrokeKinds.IsStructure survived
     * every other test in this file.
     */
    [Fact]
    public void ADeckStraddlingTwoLevelsIsStillNotARamp()
    {
        var cd = StreetHarness.MakeCluster("straddle", ClusterSize);
        var store = new StrokeStore(ClusterSize);

        var low = _pointAt(0f, 0f);
        var high = _pointAt(100f, 0f, 1);
        store.AddStroke(_street(cd, low, high, CorridorWeight, StrokeKind.Bridge, 0));

        var heights = new Dictionary<int, float> { [low.Id] = 0f, [high.Id] = 5f };

        StructureProfile.Design(
            store.GetStrokes().OrderBy(s => s.Sid), heights, new GradePolicy());

        Assert.Equal(0f, heights[low.Id]);
        Assert.Equal(5f, heights[high.Id]);
    }


    /**
     * A log target that keeps what was written to it, for the one property here that is
     * only observable as a log line.
     *
     * engine.Logger.SetLogTarget is a process global. Every test that installs one is in
     * this class, so xUnit serialises them against each other, and nothing else in the
     * assembly installs one at all - what other classes log during the window is simply
     * captured and ignored.
     */
    private sealed class LogCapture : global::engine.ILogTarget, IDisposable
    {
        private readonly List<string> _lines = new();
        private readonly object _lo = new();

        internal LogCapture()
        {
            global::engine.Logger.SetLogTarget(this);
        }

        public void AddLogEntry(
            in global::engine.Logger.Level level, in string logEntry)
        {
            lock (_lo)
            {
                _lines.Add($"{level}|{logEntry}");
            }
        }

        internal bool Saw(string fragment)
        {
            lock (_lo)
            {
                return _lines.Any(l => l.Contains(fragment, StringComparison.Ordinal));
            }
        }

        public void Dispose() => global::engine.Logger.SetLogTarget(null);
    }


    /**
     * ⚠️ A stroke whose endpoint has no starting height is REPORTED, not absorbed.
     *
     * Pre-existing behaviour of GradeRelaxer and the reason the counter is there at all:
     * quietly skipping such a stroke leaves exactly the unbuildable grade the pass exists
     * to remove, with nothing in the log to say so. It had no test - deleting the counter
     * and the Warning passed everything - and it is the kind of thing a boundary rule
     * added right beside it could take out by accident.
     *
     * Asserted on the log because that is where the property lives; a source scan would
     * be satisfied by the identifier surviving in a comment.
     */
    [Fact]
    public void AStrokeWithNoStartingHeightIsReportedRatherThanSkipped()
    {
        var cd = StreetHarness.MakeCluster("unheighted-report", ClusterSize);
        var store = new StrokeStore(ClusterSize);

        var a = _pointAt(0f, 0f);
        var b = _pointAt(300f, 0f);
        store.AddStroke(_street(cd, a, b, CorridorWeight, StrokeKind.Street, 0));

        using var log = new LogCapture();

        /*
         * b is deliberately absent from the table, which is what the counter is for.
         */
        var heights = new Dictionary<int, float> { [a.Id] = 0f };
        GradeRelaxer.Relax(store.GetStrokes(), heights, new GradePolicy());

        Assert.True(log.Saw("no starting height"),
            "GradeRelaxer skipped a stroke it could not relax and said nothing about it");
    }


    /**
     * ...and the control: a network whose heights are all present says nothing, so the
     * assertion above cannot be satisfied by a warning that always fires.
     */
    [Fact]
    public void ANetworkWithEveryHeightPresentIsNotReported()
    {
        var f = _bridge();

        using var log = new LogCapture();

        var heights = new Dictionary<int, float>(f.Terrain);
        GradeRelaxer.Relax(f.Store.GetStrokes(), heights, new GradePolicy());

        Assert.False(log.Saw("no starting height"));
    }


    /**
     * The unheighted warning GradeRelaxer has always carried still fires. A structure
     * whose foot is missing from the table cannot be designed either, and both halves
     * have to keep saying so rather than absorbing it.
     */
    [Fact]
    public void ARampWhoseFootHasNoHeightIsNotInvented()
    {
        var cd = StreetHarness.MakeCluster("unheighted", ClusterSize);
        var store = new StrokeStore(ClusterSize);

        var foot = _pointAt(0f, 0f);
        var deck = _pointAt(100f, 0f, 1);
        store.AddStroke(_street(cd, foot, deck, CorridorWeight, StrokeKind.Ramp, 0));

        var heights = new Dictionary<int, float> { [deck.Id] = 3f };

        StructureProfile.Design(store.GetStrokes().OrderBy(s => s.Sid), heights, new GradePolicy());

        Assert.False(heights.ContainsKey(foot.Id));
        Assert.Equal(3f, heights[deck.Id]);
    }


    /*
     * ---------------------------------------------- the builder's own structure -----
     */

    /**
     * The shape OverpassBuilder actually produces, rather than the one this file builds
     * by hand - so that the profile's "the foot is the end whose level is the ramp's"
     * rule is measured against the builder's own convention instead of against a
     * restatement of it.
     */
    [Theory]
    [InlineData(StrokeKind.Bridge)]
    [InlineData(StrokeKind.Tunnel)]
    public void TheProfileFitsTheStructureTheBuilderBuilds(StrokeKind deckKind)
    {
        var cd = StreetHarness.MakeCluster("builder", ClusterSize);
        var store = new StrokeStore(ClusterSize);
        var policy = new GradePolicy();

        var from = _pointAt(0f, 0f);
        var to = _pointAt(400f, 0f);

        var chain = new OverpassBuilder(cd.Id).Build(
            from, to, deckKind, RampLength / 400f, CorridorWeight);
        Assert.Equal(3, chain.Count);

        foreach (var s in chain)
        {
            store.AddStroke(s);
        }

        var heights = new Dictionary<int, float>();
        foreach (var sp in store.GetStreetPoints())
        {
            heights[sp.Id] = 0f;
        }

        heights[from.Id] = 12f;
        heights[to.Id] = 20f;

        GradeRelaxer.Relax(store.GetStrokes(), heights, policy);

        float sign = deckKind == StrokeKind.Tunnel ? -1f : 1f;

        Assert.Equal(sign * policy.MaxRampGrade, _grade(chain[0], heights), 5);
        Assert.Equal(-sign * policy.MaxRampGrade, _grade(chain[2], heights), 5);

        Assert.Equal(12f, heights[from.Id]);
        Assert.Equal(20f, heights[to.Id]);
    }
}
