using System;
using System.Collections.Generic;
using System.Linq;
using engine.streets;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * Gradient limiting over the stroke graph.
 *
 * A pure function of (graph, starting heights, policy), so all of this runs with no
 * terrain, no fragments and no engine - which is the reason the relaxation was written
 * as its own pass rather than folded into the terrain source.
 */
public class GradeRelaxerTests
{
    private const float ClusterSize = 2000f;


    private static StreetPoint _pointAt(float x, float y)
    {
        var sp = new StreetPoint() { ClusterId = 0 };
        sp.SetPos(x, y);
        return sp;
    }


    /**
     * A chain of n strokes running along +X, each `length` long, all of one weight.
     */
    private static (StrokeStore Store, List<StreetPoint> Points) _chain(
        int n, float length, float weight)
    {
        var cd = StreetHarness.MakeCluster("relax", ClusterSize);
        var store = new StrokeStore(ClusterSize);

        var points = new List<StreetPoint> { _pointAt(0f, 0f) };
        for (int i = 0; i < n; ++i)
        {
            var next = _pointAt(0f, 0f);
            var stroke = Stroke.CreateByAngleFrom(
                cd, points[i], next, 0f, length, true, weight);
            store.AddStroke(stroke);
            points.Add(next);
        }

        return (store, points);
    }


    private static Dictionary<int, float> _heights(IEnumerable<StreetPoint> points, params float[] hs)
    {
        var list = points.ToList();
        var d = new Dictionary<int, float>();
        for (int i = 0; i < list.Count; ++i)
        {
            d[list[i].Id] = hs[i];
        }

        return d;
    }


    private static float _grade(Stroke s, Dictionary<int, float> h)
        => Single.Abs(h[s.B.Id] - h[s.A.Id]) / s.Length;


    /**
     * The whole point. A cliff between two junctions comes back as a slope the policy
     * allows.
     */
    [Fact]
    public void AnImpossibleGradeIsBroughtWithinTheLimit()
    {
        var (store, points) = _chain(1, 100f, 0.5f);
        var heights = _heights(points, 0f, 60f);
        var policy = new GradePolicy();

        float before = _grade(store.GetStrokes()[0], heights);
        Assert.True(before > 0.5f, "the fixture must start with an unbuildable grade");

        GradeRelaxer.Relax(store.GetStrokes(), heights, policy);

        float after = _grade(store.GetStrokes()[0], heights);
        Assert.True(after <= policy.MaxGradeFor(store.GetStrokes()[0]) + 0.001f,
            $"grade is still {after:F3}");
    }


    /**
     * The correction stops at the limit rather than continuing to flat.
     *
     * This is the difference between a city that stands on its terrain and one that has
     * been ironed flat again by a different mechanism - and "the grade is now within the
     * limit" is true of a flattened network too, so asserting only that cannot tell them
     * apart. A mutation that relaxed all the way to zero passed every other test here.
     */
    [Fact]
    public void TheCorrectedGradeStopsAtTheLimitRatherThanGoingFlat()
    {
        var (store, points) = _chain(1, 100f, 0.5f);
        var heights = _heights(points, 0f, 60f);
        var policy = new GradePolicy();

        GradeRelaxer.Relax(store.GetStrokes(), heights, policy);

        var stroke = store.GetStrokes()[0];
        float limit = policy.MaxGradeFor(stroke);
        float after = _grade(stroke, heights);

        Assert.True(after > limit * 0.9f,
            $"the road came out at {after:F3} where {limit:F3} was allowed - "
            + "the excess should come off, not the whole slope");
    }


    /**
     * A network already within its limits is left exactly alone.
     *
     * This is what makes the pass safe to have in the chain unconditionally: a flat city
     * is trivially within any grade limit, so nothing is computed and nothing moves.
     */
    [Fact]
    public void AnAcceptableNetworkIsNotTouched()
    {
        var (store, points) = _chain(3, 100f, 0.5f);
        var heights = _heights(points, 0f, 2f, 4f, 6f);
        var before = new Dictionary<int, float>(heights);

        int sweeps = GradeRelaxer.Relax(store.GetStrokes(), heights, new GradePolicy());

        Assert.Equal(1, sweeps);
        foreach (var kv in before)
        {
            Assert.Equal(kv.Value, heights[kv.Key]);
        }
    }


    /**
     * A flat network is the case the shipped game is in, so it gets its own assertion
     * rather than relying on the one above to cover it by implication.
     */
    [Fact]
    public void AFlatNetworkComesBackFlat()
    {
        var (store, points) = _chain(4, 120f, 0.9f);
        var heights = _heights(points, 7f, 7f, 7f, 7f, 7f);

        GradeRelaxer.Relax(store.GetStrokes(), heights, new GradePolicy());

        Assert.All(heights.Values, h => Assert.Equal(7f, h));
    }


    /**
     * Where a heavy street meets a light one, the light one does the climbing.
     *
     * Not a stylistic choice - it is why arterials stay flat and side streets fall away
     * from them in a real city. Set up so both strokes are over their limit and would
     * otherwise be corrected symmetrically.
     */
    [Fact]
    public void TheLighterEndOfAStrokeDoesMoreOfTheMoving()
    {
        var cd = StreetHarness.MakeCluster("relax-weights", ClusterSize);
        var store = new StrokeStore(ClusterSize);

        /*
         * One steep stroke between p and q. What makes the two ends differ is not this
         * stroke - both its ends carry the same weight - but what ELSE meets them: an
         * arterial at p, an alley at q. A junction resists by the heaviest street on it,
         * so p is hard to move and q is easy.
         */
        var p = _pointAt(0f, 0f);
        var q = _pointAt(0f, 0f);
        var arterialAnchor = _pointAt(0f, 0f);
        var alleyAnchor = _pointAt(0f, 0f);

        var steep = Stroke.CreateByAngleFrom(cd, p, q, 0f, 100f, true, 0.5f);
        store.AddStroke(steep);
        store.AddStroke(Stroke.CreateByAngleFrom(cd, p, arterialAnchor, 90f, 400f, true, 1.3f));
        store.AddStroke(Stroke.CreateByAngleFrom(cd, q, alleyAnchor, 270f, 400f, true, 0.2f));

        var heights = new Dictionary<int, float>
        {
            [p.Id] = 0f,
            [q.Id] = 40f,
            [arterialAnchor.Id] = 0f,
            [alleyAnchor.Id] = 40f
        };

        /*
         * A single sweep, so this measures the split rule itself rather than where the
         * network eventually settles - by the end the anchors have moved too and the
         * comparison stops being about one stroke.
         */
        GradeRelaxer.Relax(store.GetStrokes(), heights, new GradePolicy { MaxSweeps = 1 });

        float arterialEndMoved = Single.Abs(heights[p.Id] - 0f);
        float alleyEndMoved = Single.Abs(heights[q.Id] - 40f);

        Assert.True(alleyEndMoved > arterialEndMoved * 2f,
            $"the alley end moved {alleyEndMoved:F2} but the arterial end moved "
            + $"{arterialEndMoved:F2}; the split should be roughly 0.5 to 1.3");
    }


    /**
     * The limit itself follows the hierarchy, which is what the test above depends on.
     */
    [Fact]
    public void AHeavierStreetIsHeldToAShallowerGrade()
    {
        var cd = StreetHarness.MakeCluster("relax-policy", ClusterSize);
        var policy = new GradePolicy();

        var alley = Stroke.CreateByAngleFrom(cd, _pointAt(0f, 0f), _pointAt(0f, 0f), 0f, 100f, true, 0.2f);
        var arterial = Stroke.CreateByAngleFrom(cd, _pointAt(0f, 0f), _pointAt(0f, 0f), 0f, 100f, true, 1.3f);

        Assert.True(policy.MaxGradeFor(alley) > policy.MaxGradeFor(arterial));
        Assert.Equal(policy.MaxGradeAtMinWeight, policy.MaxGradeFor(alley), 4);
        Assert.Equal(policy.MaxGradeAtMaxWeight, policy.MaxGradeFor(arterial), 4);
    }


    /**
     * Same graph and same starting heights give the same answer, every time. Cities are
     * regenerated from a seed rather than stored, so a relaxation that drifted would
     * make a city different each time it was visited.
     */
    [Fact]
    public void RelaxationIsDeterministic()
    {
        var (storeA, pointsA) = _chain(6, 90f, 0.6f);
        var (storeB, pointsB) = _chain(6, 90f, 0.6f);

        float[] rough = { 0f, 45f, 12f, 70f, 20f, 5f, 55f };

        var hA = _heights(pointsA, rough);
        var hB = _heights(pointsB, rough);

        GradeRelaxer.Relax(storeA.GetStrokes(), hA, new GradePolicy());
        GradeRelaxer.Relax(storeB.GetStrokes(), hB, new GradePolicy());

        for (int i = 0; i < pointsA.Count; ++i)
        {
            Assert.Equal(hA[pointsA[i].Id], hB[pointsB[i].Id]);
        }
    }


    /**
     * Visiting order must not change the answer, which is what Jacobi buys and what a
     * Gauss-Seidel version of the same loop would quietly lose.
     */
    [Fact]
    public void TheOrderStrokesAreVisitedInDoesNotMatter()
    {
        var (store, points) = _chain(6, 90f, 0.6f);
        float[] rough = { 0f, 45f, 12f, 70f, 20f, 5f, 55f };

        var forward = _heights(points, rough);
        var backward = _heights(points, rough);

        GradeRelaxer.Relax(store.GetStrokes(), forward, new GradePolicy());
        GradeRelaxer.Relax(
            store.GetStrokes().AsEnumerable().Reverse().ToList(), backward, new GradePolicy());

        foreach (var sp in points)
        {
            Assert.Equal(forward[sp.Id], backward[sp.Id], 4);
        }
    }


    /**
     * A whole ridge of unbuildable terrain settles rather than oscillating, and does so
     * without using up the sweep budget.
     */
    [Fact]
    public void ARoughProfileSettlesWithinTheSweepBudget()
    {
        var (store, points) = _chain(10, 80f, 0.5f);

        var heights = new Dictionary<int, float>();
        for (int i = 0; i < points.Count; ++i)
        {
            /*
             * Alternating spikes: the hardest case for a relaxation, since every stroke
             * is over its limit and each correction disturbs its neighbours.
             */
            heights[points[i].Id] = (0 == (i & 1)) ? 0f : 50f;
        }

        var policy = new GradePolicy();
        int sweeps = GradeRelaxer.Relax(store.GetStrokes(), heights, policy);

        Assert.True(sweeps < policy.MaxSweeps, $"did not settle, used all {sweeps} sweeps");

        foreach (var s in store.GetStrokes())
        {
            Assert.True(_grade(s, heights) <= policy.MaxGradeFor(s) + 0.002f,
                $"stroke {s.Sid} is still at {_grade(s, heights):F3}");
        }
    }


    /**
     * A junction where many steep streets meet still settles.
     *
     * The case damping exists for. Every spoke of a star wants to pull the hub the same
     * way, so applying all of them in full moves it several times as far as any one of
     * them asked, past the target and back again. A chain cannot show this - its
     * junctions have two strokes and they usually pull against each other - which is why
     * removing the damping passed the rough-profile test.
     */
    [Fact]
    public void AStarJunctionWithManySteepSpokesStillSettles()
    {
        var cd = StreetHarness.MakeCluster("relax-star", ClusterSize);
        var store = new StrokeStore(ClusterSize);

        var hub = _pointAt(0f, 0f);
        var spokes = new List<StreetPoint>();

        for (int i = 0; i < 6; ++i)
        {
            var spoke = _pointAt(0f, 0f);
            store.AddStroke(Stroke.CreateByAngleFrom(cd, hub, spoke, i * 60f, 100f, true, 0.5f));
            spokes.Add(spoke);
        }

        /*
         * Keyed AFTER every AddStroke, because a point's Id changes when it joins a
         * store - the constructor hands out a provisional id and StrokeStore replaces it
         * with one local to the network. Reading hub.Id before the first AddStroke keys
         * this on the provisional value, the relaxer looks up the real one, finds
         * nothing and silently does no work. It fails only sometimes, because the
         * provisional counter is static across the whole test assembly, so whether the
         * two ids happen to coincide depends on what else ran first.
         */
        var heights = new Dictionary<int, float> { [hub.Id] = 90f };
        foreach (var spoke in spokes)
        {
            heights[spoke.Id] = 0f;
        }

        var policy = new GradePolicy();
        int sweeps = GradeRelaxer.Relax(store.GetStrokes(), heights, policy);

        Assert.True(sweeps < policy.MaxSweeps, $"did not settle, used all {sweeps} sweeps");

        foreach (var s in store.GetStrokes())
        {
            Assert.True(_grade(s, heights) <= policy.MaxGradeFor(s) + 0.002f,
                $"spoke {s.Sid} is still at {_grade(s, heights):F3}");
        }
    }


    /**
     * Relaxation flattens the profile; it must not slide the whole city up or down the
     * mountain while doing so.
     */
    [Fact]
    public void TheOverallLevelIsRoughlyPreserved()
    {
        var (store, points) = _chain(10, 80f, 0.5f);

        var heights = new Dictionary<int, float>();
        for (int i = 0; i < points.Count; ++i)
        {
            heights[points[i].Id] = (0 == (i & 1)) ? 0f : 50f;
        }

        float meanBefore = heights.Values.Average();

        GradeRelaxer.Relax(store.GetStrokes(), heights, new GradePolicy());

        float meanAfter = heights.Values.Average();

        Assert.True(Single.Abs(meanAfter - meanBefore) < 1f,
            $"the network drifted from {meanBefore:F2} to {meanAfter:F2}");
    }


    /**
     * A junction is one node, so relaxing it moves every street meeting there at once.
     * Stated explicitly because it is the property the whole non-planar story rests on,
     * and because a version of this keyed on stroke ends rather than junctions would
     * pass every other test in this file.
     */
    [Fact]
    public void RelaxingMovesAJunctionForEveryStreetThatMeetsIt()
    {
        var cd = StreetHarness.MakeCluster("relax-shared", ClusterSize);
        var store = new StrokeStore(ClusterSize);

        var shared = _pointAt(0f, 0f);
        var west = _pointAt(0f, 0f);
        var north = _pointAt(0f, 0f);

        var toWest = Stroke.CreateByAngleFrom(cd, shared, west, 0f, 100f, true, 0.5f);
        store.AddStroke(toWest);
        var toNorth = Stroke.CreateByAngleFrom(cd, shared, north, 90f, 100f, true, 0.5f);
        store.AddStroke(toNorth);

        var heights = new Dictionary<int, float>
        {
            [shared.Id] = 60f,
            [west.Id] = 0f,
            [north.Id] = 0f
        };

        GradeRelaxer.Relax(store.GetStrokes(), heights, new GradePolicy());

        /*
         * One entry for the junction, so both strokes necessarily read the same height -
         * and it did move, so this is not vacuous.
         */
        Assert.NotEqual(60f, heights[shared.Id]);
        Assert.Equal(heights[toWest.A.Id], heights[toNorth.A.Id]);
    }
}
