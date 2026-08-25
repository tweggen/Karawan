using System;
using System.Numerics;
using engine.streets;
using engine.streets.generation;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * Shared scaffolding for the constraint tests.
 *
 * Each of these checks used to be a block inside a 400 line loop, reachable only by
 * running the whole generator over a whole cluster. Individually they need three or
 * four strokes.
 */
internal static class ConstraintFixture
{
    internal const float ClusterSize = 1000f;


    internal static GenerationContext Context() => new GenerationContext
    {
        MinPointToCandPointDistance = 30f,
        MinPointToCandStrokeDistance = 30f,
        MinPointToCandIntersectionDistance = 30f,
        AngleMinStrokesRad = 40f * (float) Math.PI / 180f,
        IsTracing = false
    };


    internal static StreetPoint PointAt(float x, float y)
    {
        var sp = new StreetPoint() { ClusterId = 0 };
        sp.SetPos(x, y);
        return sp;
    }


    internal static StrokeStore Store() => new StrokeStore(ClusterSize);


    /**
     * A candidate stroke leaving `from` at `angleDeg`, not in any store.
     */
    internal static Stroke Candidate(StreetPoint from, float angleDeg, float length)
    {
        var clusterDesc = StreetHarness.MakeCluster("constraints", ClusterSize);
        return Stroke.CreateByAngleFrom(
            clusterDesc, from, PointAt(0f, 0f),
            angleDeg * (float) Math.PI / 180f, length, true, 1.0f);
    }


    /**
     * Put one stroke into the store, leaving `from` at `angleDeg`.
     */
    internal static Stroke AddStroke(StrokeStore store, StreetPoint from, float angleDeg, float length)
    {
        var stroke = Candidate(from, angleDeg, length);
        store.AddStroke(stroke);
        return stroke;
    }
}


public class MinLengthConstraintTests
{
    private readonly MinLengthConstraint _c = new();

    [Fact]
    public void ALongEnoughStrokeIsAccepted()
    {
        var cand = ConstraintFixture.Candidate(ConstraintFixture.PointAt(0f, 0f), 0f, 100f);
        Assert.Equal(VerdictKind.Accept,
            _c.Check(cand, ConstraintFixture.Store(), ConstraintFixture.Context()).Kind);
    }

    [Fact]
    public void ATooShortStrokeIsRejected()
    {
        var cand = ConstraintFixture.Candidate(ConstraintFixture.PointAt(0f, 0f), 0f, 10f);
        var verdict = _c.Check(cand, ConstraintFixture.Store(), ConstraintFixture.Context());
        Assert.Equal(VerdictKind.Reject, verdict.Kind);
        Assert.Equal("too short", verdict.Reason);
    }

    /**
     * The comparison is strictly less than, so a stroke exactly at the limit stays.
     */
    [Fact]
    public void AStrokeExactlyAtTheLimitIsAccepted()
    {
        var a = ConstraintFixture.PointAt(0f, 0f);
        var cand = ConstraintFixture.Candidate(a, 0f, 30f);
        Assert.Equal(30f, Vector2.Distance(cand.A.Pos, cand.B.Pos), 3);
        Assert.Equal(VerdictKind.Accept,
            _c.Check(cand, ConstraintFixture.Store(), ConstraintFixture.Context()).Kind);
    }
}


public class SnapToNearbyPointConstraintTests
{
    private readonly SnapToNearbyPointConstraint _c = new();

    [Fact]
    public void AnEndpointAlreadyInTheStoreIsLeftAlone()
    {
        var store = ConstraintFixture.Store();
        var a = ConstraintFixture.PointAt(0f, 0f);
        var stored = ConstraintFixture.AddStroke(store, a, 0f, 100f);

        /*
         * Reuse the stored stroke's far end as the candidate's far end.
         */
        var cand = ConstraintFixture.Candidate(ConstraintFixture.PointAt(0f, 200f), 0f, 100f);
        cand.B = stored.B;

        Assert.Equal(VerdictKind.Accept,
            _c.Check(cand, store, ConstraintFixture.Context()).Kind);
    }

    [Fact]
    public void ANewEndpointWithNothingNearbyIsLeftAlone()
    {
        var store = ConstraintFixture.Store();
        ConstraintFixture.AddStroke(store, ConstraintFixture.PointAt(0f, 0f), 0f, 100f);

        var cand = ConstraintFixture.Candidate(ConstraintFixture.PointAt(0f, 300f), 0f, 100f);

        Assert.Equal(VerdictKind.Accept,
            _c.Check(cand, store, ConstraintFixture.Context()).Kind);
    }

    /**
     * The reason the pipeline needs a restart verdict at all.
     */
    [Fact]
    public void ANewEndpointNearAJunctionSnapsOntoItAndRestarts()
    {
        var store = ConstraintFixture.Store();
        var a = ConstraintFixture.PointAt(0f, 0f);
        var stored = ConstraintFixture.AddStroke(store, a, 0f, 100f);

        /*
         * Candidate ending at (95,0), 5 m from the stored junction at (100,0).
         */
        var cand = ConstraintFixture.Candidate(ConstraintFixture.PointAt(0f, 0f), 0f, 95f);
        Assert.False(cand.B.InStore);

        var verdict = _c.Check(cand, store, ConstraintFixture.Context());

        Assert.Equal(VerdictKind.Restart, verdict.Kind);
        Assert.Same(stored.B, cand.B);
    }
}


public class AlreadyConnectedConstraintTests
{
    private readonly AlreadyConnectedConstraint _c = new();

    [Fact]
    public void AlreadyConnectedJunctionsAreRejected()
    {
        var store = ConstraintFixture.Store();
        var a = ConstraintFixture.PointAt(0f, 0f);
        var stored = ConstraintFixture.AddStroke(store, a, 0f, 100f);

        var cand = ConstraintFixture.Candidate(ConstraintFixture.PointAt(0f, 0f), 90f, 100f);
        cand.A = a;
        cand.B = stored.B;

        var verdict = _c.Check(cand, store, ConstraintFixture.Context());
        Assert.Equal(VerdictKind.Reject, verdict.Kind);
        Assert.Equal("already connected", verdict.Reason);
    }

    [Fact]
    public void UnconnectedStoredJunctionsAreAccepted()
    {
        var store = ConstraintFixture.Store();
        var a = ConstraintFixture.PointAt(0f, 0f);
        var far = ConstraintFixture.PointAt(0f, 500f);
        ConstraintFixture.AddStroke(store, a, 0f, 100f);
        var other = ConstraintFixture.AddStroke(store, far, 0f, 100f);

        var cand = ConstraintFixture.Candidate(ConstraintFixture.PointAt(0f, 0f), 0f, 10f);
        cand.A = a;
        cand.B = other.B;

        Assert.Equal(VerdictKind.Accept, _c.Check(cand, store, ConstraintFixture.Context()).Kind);
    }

    [Fact]
    public void ANewEndpointCannotBeConnectedYet()
    {
        var store = ConstraintFixture.Store();
        var a = ConstraintFixture.PointAt(0f, 0f);
        ConstraintFixture.AddStroke(store, a, 0f, 100f);

        var cand = ConstraintFixture.Candidate(a, 90f, 100f);

        Assert.False(cand.B.InStore);
        Assert.Equal(VerdictKind.Accept, _c.Check(cand, store, ConstraintFixture.Context()).Kind);
    }
}


public class AngleSeparationConstraintTests
{
    private readonly AngleSeparationConstraint _atA = new(atB: false);
    private readonly AngleSeparationConstraint _atB = new(atB: true);

    [Fact]
    public void AJunctionWithNoOtherStrokesAcceptsAnything()
    {
        var store = ConstraintFixture.Store();
        var a = ConstraintFixture.PointAt(0f, 0f);
        var cand = ConstraintFixture.Candidate(a, 0f, 100f);

        Assert.Equal(VerdictKind.Accept, _atA.Check(cand, store, ConstraintFixture.Context()).Kind);
    }

    [Fact]
    public void AWideAngleIsAccepted()
    {
        var store = ConstraintFixture.Store();
        var a = ConstraintFixture.PointAt(0f, 0f);
        ConstraintFixture.AddStroke(store, a, 0f, 100f);

        var cand = ConstraintFixture.Candidate(a, 90f, 100f);

        Assert.Equal(VerdictKind.Accept, _atA.Check(cand, store, ConstraintFixture.Context()).Kind);
    }

    [Fact]
    public void AShallowAngleIsRejected()
    {
        var store = ConstraintFixture.Store();
        var a = ConstraintFixture.PointAt(0f, 0f);
        ConstraintFixture.AddStroke(store, a, 0f, 100f);

        var cand = ConstraintFixture.Candidate(a, 10f, 100f);

        var verdict = _atA.Check(cand, store, ConstraintFixture.Context());
        Assert.Equal(VerdictKind.Reject, verdict.Kind);
        Assert.Equal("angle too close at A", verdict.Reason);
    }

    /**
     * The B side measures the candidate's angle reversed, because the candidate
     * arrives at that junction rather than leaving it.
     *
     * A stroke arriving at (100,0) from the east meets one arriving from the west head
     * on: 180 degrees apart at the junction, which is just a straight road running
     * through it.
     */
    [Fact]
    public void AStraightContinuationIsAcceptedAtB()
    {
        var store = ConstraintFixture.Store();
        var a = ConstraintFixture.PointAt(0f, 0f);
        var stored = ConstraintFixture.AddStroke(store, a, 0f, 100f);

        var cand = ConstraintFixture.Candidate(ConstraintFixture.PointAt(200f, 0f), 180f, 100f);
        cand.B = stored.B;

        Assert.Equal(VerdictKind.Accept, _atB.Check(cand, store, ConstraintFixture.Context()).Kind);
    }


    /**
     * Arriving from almost the same direction as an existing stroke is what the
     * constraint is actually there to stop: the two would render as one thick street.
     */
    [Fact]
    public void AShallowArrivalIsRejectedAtB()
    {
        var store = ConstraintFixture.Store();
        var a = ConstraintFixture.PointAt(0f, 0f);
        var stored = ConstraintFixture.AddStroke(store, a, 0f, 100f);

        /*
         * From (0,10) to (100,0): both strokes leave the junction westward, about 5.7
         * degrees apart.
         */
        var cand = ConstraintFixture.Candidate(ConstraintFixture.PointAt(0f, 10f), 0f, 100f);
        cand.B = stored.B;

        var verdict = _atB.Check(cand, store, ConstraintFixture.Context());
        Assert.Equal(VerdictKind.Reject, verdict.Kind);
        Assert.Equal("angle too close at B", verdict.Reason);
    }
}


public class BoundsConstraintTests
{
    private static readonly BoundsConstraint _c =
        new(new Vector2(-100f, -100f), new Vector2(100f, 100f));

    [Fact]
    public void AStrokeFullyInsideIsAccepted()
    {
        var cand = ConstraintFixture.Candidate(ConstraintFixture.PointAt(-50f, 0f), 0f, 50f);
        Assert.Equal(VerdictKind.Accept,
            _c.Check(cand, ConstraintFixture.Store(), ConstraintFixture.Context()).Kind);
    }

    [Fact]
    public void AStrokeLeavingTheAreaIsRejected()
    {
        var cand = ConstraintFixture.Candidate(ConstraintFixture.PointAt(50f, 0f), 0f, 200f);
        var verdict = _c.Check(cand, ConstraintFixture.Store(), ConstraintFixture.Context());
        Assert.Equal(VerdictKind.Reject, verdict.Kind);
        Assert.Equal("out of bounds", verdict.Reason);
    }

    [Fact]
    public void AStrokeStartingOutsideIsRejected()
    {
        var cand = ConstraintFixture.Candidate(ConstraintFixture.PointAt(-200f, 0f), 0f, 150f);
        Assert.Equal(VerdictKind.Reject,
            _c.Check(cand, ConstraintFixture.Store(), ConstraintFixture.Context()).Kind);
    }

    /**
     * The comparison is strict, so a point exactly on the edge counts as outside.
     */
    [Fact]
    public void APointExactlyOnTheEdgeIsOutside()
    {
        var cand = ConstraintFixture.Candidate(ConstraintFixture.PointAt(0f, 0f), 0f, 100f);
        Assert.Equal(100f, cand.B.Pos.X, 3);
        Assert.Equal(VerdictKind.Reject,
            _c.Check(cand, ConstraintFixture.Store(), ConstraintFixture.Context()).Kind);
    }
}
