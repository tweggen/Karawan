using System;
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
