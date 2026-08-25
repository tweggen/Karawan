using System;
using System.Collections.Generic;
using System.Linq;
using engine.streets;
using engine.streets.generation;
using LiteDB;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * Levels, and the one property that makes them worth having: two streets on different
 * decks cross on the map without meeting in the world.
 */
public class MultilayerTests
{
    private const float ClusterSize = 1000f;


    private static StreetPoint _pointAt(float x, float y, sbyte level)
    {
        var sp = new StreetPoint() { ClusterId = 0, Level = level };
        sp.SetPos(x, y);
        return sp;
    }


    /**
     * A stroke between two explicit positions on one deck.
     *
     * Built by assigning endpoints directly rather than through
     * Stroke.CreateByAngleFrom, which derives B's position from an angle and a length
     * and would silently overwrite the geometry this test is trying to set up.
     */
    private static Stroke _candidate(float x0, float y0, float x1, float y1, sbyte level)
    {
        var a = _pointAt(x0, y0, level);
        var b = _pointAt(x1, y1, level);

        var stroke = new Stroke() { ClusterId = 0, IsPrimary = true, Weight = 1.0f, Level = level };
        stroke.A = a;
        stroke.B = b;
        return stroke;
    }


    private static Stroke _addStroke(StrokeStore store,
        float x0, float y0, float x1, float y1, sbyte level)
    {
        var stroke = _candidate(x0, y0, x1, y1, level);
        store.AddStroke(stroke);
        return stroke;
    }


    private static GenerationContext _ctx() => ConstraintFixture.Context();


    /**
     * AC-4.3, and the whole point of the exercise.
     */
    [Fact]
    public void StrokesOnDifferentLevelsCrossWithoutSplitting()
    {
        var store = new StrokeStore(ClusterSize);
        _addStroke(store, 0f, 0f, 100f, 0f, level: 0);

        /*
         * Vertical, straight over the middle of the ground stroke, one deck up.
         */
        var cand = _candidate(50f, -50f, 50f, 50f, level: 1);

        var verdict = new IntersectionConstraint().Check(cand, store, _ctx());

        Assert.Equal(VerdictKind.Accept, verdict.Kind);
        Assert.Equal(1, store.GetStrokes().Count);
        Assert.Equal(2, store.GetStreetPoints().Count);
    }


    /**
     * The control: identical geometry on the same deck must still split, or the test
     * above would prove nothing.
     */
    [Fact]
    public void TheSameCrossingOnOneLevelStillSplits()
    {
        var store = new StrokeStore(ClusterSize);
        _addStroke(store, 0f, 0f, 100f, 0f, level: 0);

        var cand = _candidate(50f, -50f, 50f, 50f, level: 0);

        var verdict = new IntersectionConstraint().Check(cand, store, _ctx());

        Assert.Equal(VerdictKind.Split, verdict.Kind);
    }


    [Fact]
    public void AnEndpointIsNeverSnappedOntoAJunctionOnAnotherLevel()
    {
        var store = new StrokeStore(ClusterSize);
        _addStroke(store, 0f, 0f, 100f, 0f, level: 0);

        /*
         * Ends 5 m from the ground junction at (100,0), but a deck up.
         */
        var cand = _candidate(0f, 0f, 95f, 0f, level: 1);

        Assert.Equal(VerdictKind.Accept,
            new SnapToNearbyPointConstraint().Check(cand, store, _ctx()).Kind);
    }


    [Fact]
    public void AStrokeIsNotConsideredNearAJunctionOnAnotherLevel()
    {
        var store = new StrokeStore(ClusterSize);
        _addStroke(store, 0f, 0f, 100f, 0f, level: 0);

        /*
         * Passes 10 m under the ground junctions, but on its own deck.
         */
        var cand = _candidate(0f, 10f, 200f, 10f, level: 1);

        Assert.Equal(VerdictKind.Accept,
            new StrokeNearPointConstraint().Check(cand, store, _ctx()).Kind);
    }


    [Fact]
    public void AnEndpointIsNotConsideredNearAStrokeOnAnotherLevel()
    {
        var store = new StrokeStore(ClusterSize);
        _addStroke(store, 0f, 0f, 100f, 0f, level: 0);

        var cand = _candidate(200f, 5f, 50f, 5f, level: 1);

        Assert.Equal(VerdictKind.Accept,
            new PointNearStrokeConstraint().Check(cand, store, _ctx()).Kind);
    }


    /**
     * Successors inherit their parent junction's deck. Changing level is a ramp's job
     * and nothing else's.
     */
    [Fact]
    public void ASuccessorInheritsTheLevelOfTheJunctionItGrowsFrom()
    {
        var clusterDesc = StreetHarness.MakeCluster("multilayer", ClusterSize);
        var a = _pointAt(0f, 0f, level: 2);
        var b = new StreetPoint() { ClusterId = 0 };

        var stroke = Stroke.CreateByAngleFrom(clusterDesc, a, b, 0f, 100f, true, 1.0f);

        Assert.Equal((sbyte)2, stroke.Level);
        Assert.Equal((sbyte)2, b.Level);
    }


    [Fact]
    public void SplittingAStrokeKeepsBothHalvesOnItsLevel()
    {
        var store = new StrokeStore(ClusterSize);
        var stored = _addStroke(store, 0f, 0f, 100f, 0f, level: 1);

        var at = _pointAt(50f, 0f, level: 1);
        var tail = new NetworkBuilder(store).SplitStrokeAt(stored, at);

        Assert.Equal((sbyte)1, stored.Level);
        Assert.Equal((sbyte)1, tail.Level);
    }


    /**
     * AC-4.8. A cluster cached before multilayer existed has no Level field at all in
     * its stored documents; it must come back on the ground rather than fail to load.
     */
    [Fact]
    public void ClustersCachedBeforeMultilayerDeserialiseOntoTheGround()
    {
        var mapper = new BsonMapper();

        var withoutLevel = new BsonDocument
        {
            ["_id"] = 4711,
            ["ClusterId"] = 0,
            ["IsPrimary"] = true,
            ["Weight"] = 1.0
        };

        var stroke = mapper.ToObject<Stroke>(withoutLevel);
        Assert.Equal((sbyte)0, stroke.Level);

        var pointWithoutLevel = new BsonDocument
        {
            ["_id"] = 4712,
            ["ClusterId"] = 0
        };

        var point = mapper.ToObject<StreetPoint>(pointWithoutLevel);
        Assert.Equal((sbyte)0, point.Level);
    }


    /**
     * V1 is the ground-only gate and must stay blind to levels; V2 is what tells two
     * decks apart.
     */
    [Fact]
    public void V1IgnoresLevelsAndV2DoesNot()
    {
        var ground = new StrokeStore(ClusterSize);
        _addStroke(ground, 0f, 0f, 100f, 0f, level: 0);

        var elevated = new StrokeStore(ClusterSize);
        _addStroke(elevated, 0f, 0f, 100f, 0f, level: 1);

        Assert.Equal(
            StreetNetworkFingerprint.V1(ground),
            StreetNetworkFingerprint.V1(elevated));

        Assert.NotEqual(
            StreetNetworkFingerprint.V2(ground),
            StreetNetworkFingerprint.V2(elevated));
    }
}


/**
 * The construction primitives: ramps, atomic chains, clearance and span.
 */
public class OverpassTests
{
    private const float ClusterSize = 1000f;

    private static StreetPoint _pointAt(float x, float y, sbyte level)
    {
        var sp = new StreetPoint() { ClusterId = 0, Level = level };
        sp.SetPos(x, y);
        return sp;
    }

    private static Stroke _stroke(StreetPoint a, StreetPoint b, StrokeKind kind, sbyte level)
    {
        var s = new Stroke() { ClusterId = 0, IsPrimary = true, Weight = 1f, Kind = kind, Level = level };
        s.A = a;
        s.B = b;
        return s;
    }

    private static List<Stroke> _overpass()
    {
        return new OverpassBuilder(0).Build(
            _pointAt(0f, 0f, 0), _pointAt(200f, 0f, 0),
            StrokeKind.Bridge, rampFraction: 0.25f, weight: 1f);
    }


    /**
     * AC-4.4 and AC-4.5, on the structure the builder actually produces.
     */
    [Fact]
    public void EveryCrossLevelJointInAnOverpassIsAnAdjacentLevelRamp()
    {
        var chain = _overpass();
        Assert.Equal(3, chain.Count);

        foreach (var s in chain)
        {
            if (s.A.Level == s.B.Level) continue;

            Assert.Equal(StrokeKind.Ramp, s.Kind);
            Assert.Equal(1, Math.Abs(s.A.Level - s.B.Level));
        }

        Assert.Equal(StrokeKind.Ramp, chain[0].Kind);
        Assert.Equal(StrokeKind.Bridge, chain[1].Kind);
        Assert.Equal(StrokeKind.Ramp, chain[2].Kind);

        /* the deck is one level up, and level with itself */
        Assert.Equal((sbyte)1, chain[1].A.Level);
        Assert.Equal((sbyte)1, chain[1].B.Level);
    }


    [Fact]
    public void ATunnelGoesDownInsteadOfUp()
    {
        var chain = new OverpassBuilder(0).Build(
            _pointAt(0f, 0f, 0), _pointAt(200f, 0f, 0),
            StrokeKind.Tunnel, rampFraction: 0.25f, weight: 1f);

        Assert.Equal((sbyte)-1, chain[1].A.Level);
        Assert.Equal(StrokeKind.Tunnel, chain[1].Kind);
    }


    [Fact]
    public void TheStructureKeepsThePlanRouteItReplaces()
    {
        var chain = _overpass();

        Assert.Equal(0f, chain[0].A.Pos.X, 1);
        Assert.Equal(200f, chain[2].B.Pos.X, 1);
        foreach (var s in chain)
        {
            Assert.Equal(0f, s.A.Pos.Y, 1);
            Assert.Equal(0f, s.B.Pos.Y, 1);
        }
    }


    [Fact]
    public void ACommittedOverpassSatisfiesTheLevelInvariants()
    {
        var store = new StrokeStore(ClusterSize);
        new NetworkBuilder(store).CommitChain(_overpass());

        Assert.Equal(3, store.GetStrokes().Count);

        foreach (var s in store.GetStrokes())
        {
            if (s.A.Level != s.B.Level)
            {
                Assert.Equal(StrokeKind.Ramp, s.Kind);
                Assert.Equal(1, Math.Abs(s.A.Level - s.B.Level));
            }
        }
    }


    /**
     * AC-4.6. The whole reason CommitChain exists.
     */
    [Fact]
    public void AChainWithAnInadmissibleMemberLeavesNothingBehind()
    {
        var store = new StrokeStore(ClusterSize);
        var chain = _overpass();

        /*
         * Break the last member: a deck-to-ground joint that claims to be an ordinary
         * street. It is the third of three, so a non-atomic commit would already have
         * added the first two by the time it is rejected.
         */
        chain[2].Kind = StrokeKind.Street;

        Assert.Throws<InvalidOperationException>(
            () => new NetworkBuilder(store).CommitChain(chain));

        Assert.Empty(store.GetStrokes());
        Assert.Empty(store.GetStreetPoints());
    }


    [Fact]
    public void AnOrdinaryStreetMayNotJoinTwoLevels()
    {
        var store = new StrokeStore(ClusterSize);
        var bad = _stroke(_pointAt(0f, 0f, 0), _pointAt(100f, 0f, 1), StrokeKind.Street, 0);

        Assert.Throws<InvalidOperationException>(() => new NetworkBuilder(store).Commit(bad));
        Assert.Empty(store.GetStrokes());
    }


    [Fact]
    public void ARampMayNotSkipALevel()
    {
        var store = new StrokeStore(ClusterSize);
        var bad = _stroke(_pointAt(0f, 0f, 0), _pointAt(100f, 0f, 2), StrokeKind.Ramp, 0);

        Assert.Throws<InvalidOperationException>(() => new NetworkBuilder(store).Commit(bad));
    }


    [Fact]
    public void ARampThatDoesNotChangeLevelIsRefused()
    {
        var store = new StrokeStore(ClusterSize);
        var bad = _stroke(_pointAt(0f, 0f, 0), _pointAt(100f, 0f, 0), StrokeKind.Ramp, 0);

        Assert.Throws<InvalidOperationException>(() => new NetworkBuilder(store).Commit(bad));
    }


    /**
     * AC-4.7.
     */
    [Fact]
    public void AStrokeRunningAlongsideARampIsRejectedForClearance()
    {
        var store = new StrokeStore(ClusterSize);
        new NetworkBuilder(store).CommitChain(_overpass());

        var ctx = ConstraintFixture.Context();
        ctx.RampClearance = 20f;

        /*
         * Parallel to the first ramp and 5 m from it, on the ground.
         */
        var cand = _stroke(_pointAt(0f, 5f, 0), _pointAt(50f, 5f, 0), StrokeKind.Street, 0);

        var verdict = new ClearanceConstraint().Check(cand, store, ctx);
        Assert.Equal(VerdictKind.Reject, verdict.Kind);
        Assert.Equal("too close to a ramp", verdict.Reason);
    }


    [Fact]
    public void ClearanceIsInactiveWhenNotConfigured()
    {
        var store = new StrokeStore(ClusterSize);
        new NetworkBuilder(store).CommitChain(_overpass());

        var ctx = ConstraintFixture.Context();
        Assert.Equal(0f, ctx.RampClearance);

        var cand = _stroke(_pointAt(0f, 5f, 0), _pointAt(50f, 5f, 0), StrokeKind.Street, 0);

        Assert.Equal(VerdictKind.Accept,
            new ClearanceConstraint().Check(cand, store, ctx).Kind);
    }


    [Fact]
    public void ADeckMustBeLongEnoughAndShortEnough()
    {
        var ctx = ConstraintFixture.Context();
        ctx.MinSpanLength = 40f;
        ctx.MaxSpanLength = 300f;

        var c = new SpanLengthConstraint();

        var tooShort = _stroke(_pointAt(0f, 0f, 1), _pointAt(10f, 0f, 1), StrokeKind.Bridge, 1);
        Assert.Equal(VerdictKind.Reject, c.Check(tooShort, null, ctx).Kind);

        var tooLong = _stroke(_pointAt(0f, 0f, 1), _pointAt(500f, 0f, 1), StrokeKind.Bridge, 1);
        Assert.Equal(VerdictKind.Reject, c.Check(tooLong, null, ctx).Kind);

        var justRight = _stroke(_pointAt(0f, 0f, 1), _pointAt(100f, 0f, 1), StrokeKind.Bridge, 1);
        Assert.Equal(VerdictKind.Accept, c.Check(justRight, null, ctx).Kind);

        /* an ordinary street is none of this constraint's business */
        var street = _stroke(_pointAt(0f, 0f, 0), _pointAt(10f, 0f, 0), StrokeKind.Street, 0);
        Assert.Equal(VerdictKind.Accept, c.Check(street, null, ctx).Kind);
    }


    [Fact]
    public void ADegenerateStructureIsRefusedRatherThanBuilt()
    {
        /*
         * So short that the two deck points quantise onto the same spot. Note the
         * builder only refuses what is geometrically impossible; refusing a span that
         * is merely too short to be sensible is SpanLengthConstraint's job, and a
         * 20 cm structure is built happily here for exactly that reason.
         */
        Assert.Null(new OverpassBuilder(0).Build(
            _pointAt(0f, 0f, 0), _pointAt(0.1f, 0f, 0), StrokeKind.Bridge, 0.25f, 1f));

        /* feet on different decks */
        Assert.Null(new OverpassBuilder(0).Build(
            _pointAt(0f, 0f, 0), _pointAt(200f, 0f, 1), StrokeKind.Bridge, 0.25f, 1f));

        /* a ramp fraction that leaves no deck */
        Assert.Null(new OverpassBuilder(0).Build(
            _pointAt(0f, 0f, 0), _pointAt(200f, 0f, 0), StrokeKind.Bridge, 0.5f, 1f));
    }
}
