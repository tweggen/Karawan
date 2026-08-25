using System;
using System.Numerics;

namespace engine.streets.generation;


/**
 * The candidate must not be shorter than the minimum distance between two junctions.
 *
 * Extracted from Generator.Generate()'s validation loop. The original additionally
 * had an empty `if (curr.A.InStore && curr.B.InStore) { }` block whose entire body was
 * a commented out throw and a question about whether the situation is really invalid;
 * it did nothing and is not carried over. The question survives in the git history.
 */
internal sealed class MinLengthConstraint : ICandidateConstraint
{
    private static readonly Verdict _tooShort = Verdict.Reject("too short");

    public string Name => "min-length";

    public Verdict Check(Stroke cand, StrokeStore store, GenerationContext ctx)
    {
        if (Vector2.Distance(cand.A.Pos, cand.B.Pos) < ctx.MinPointToCandPointDistance)
        {
            return _tooShort;
        }

        return Verdict.Accept;
    }
}


/**
 * If the candidate's far end is new and lands close to an existing junction, use that
 * junction instead of creating another one.
 *
 * This is the reason the pipeline needs a restart verdict at all: the candidate is
 * rewritten and everything has to be judged again.
 *
 * Note that only B is considered. A is the junction we are growing away from, so it is
 * already in the store and already known to be far enough from its neighbours.
 */
internal sealed class SnapToNearbyPointConstraint : ICandidateConstraint
{
    public string Name => "snap-to-nearby-point";

    public Verdict Check(Stroke cand, StrokeStore store, GenerationContext ctx)
    {
        if (cand.B.InStore)
        {
            return Verdict.Accept;
        }

        StreetPoint tooClose = store.FindClosestBelowButNot(
            cand.B, ctx.MinPointToCandPointDistance, cand.A);

        if (null == tooClose)
        {
            return Verdict.Accept;
        }

        cand.B = tooClose;
        return Verdict.Restart;
    }
}


/**
 * Two junctions may only be joined by one stroke.
 */
internal sealed class AlreadyConnectedConstraint : ICandidateConstraint
{
    private static readonly Verdict _alreadyConnected = Verdict.Reject("already connected");

    public string Name => "already-connected";

    public Verdict Check(Stroke cand, StrokeStore store, GenerationContext ctx)
    {
        if (cand.A.InStore && cand.B.InStore)
        {
            if (store.AreConnected(cand.A, cand.B))
            {
                return _alreadyConnected;
            }
        }

        return Verdict.Accept;
    }
}


/**
 * A new stroke must not leave a junction at too shallow an angle to a stroke that
 * already leaves it, or the two would render as one thick street.
 *
 * Instantiated twice, once per endpoint, and run A first then B — the original loop
 * checked them in that order and an A-side rejection means the B-side scan never runs.
 *
 * The original wrote the two scans out separately and, in doing so, declared the
 * running minimum as `double` on the A side and `float` on the B side. Every value
 * stored in it is exactly representable as a float, so the two are numerically
 * identical; they are unified here as float, and the fingerprint gate is what
 * demonstrates that this is true rather than merely plausible.
 */
internal sealed class AngleSeparationConstraint : ICandidateConstraint
{
    private static readonly Verdict _tooCloseAtA = Verdict.Reject("angle too close at A");
    private static readonly Verdict _tooCloseAtB = Verdict.Reject("angle too close at B");

    private readonly bool _atB;


    internal AngleSeparationConstraint(bool atB)
    {
        _atB = atB;
    }


    public string Name => _atB ? "angle-separation-b" : "angle-separation-a";


    public Verdict Check(Stroke cand, StrokeStore store, GenerationContext ctx)
    {
        StreetPoint sp = _atB ? cand.B : cand.A;

        /*
         * Incoming angle with respect to the endpoint under test.
         */
        float myAngle = _atB ? cand.Angle + (float) Math.PI : cand.Angle;

        var angles = sp.GetAngleArray();
        float closestAngle = 9.0f;

        foreach (var stroke in angles)
        {
            float candAngle = stroke.GetAngleSP(sp);
            float thisAngle = Single.Abs(geom.Angles.Snorm(candAngle - myAngle));
            if (thisAngle < closestAngle)
            {
                closestAngle = thisAngle;
            }
        }

        if (closestAngle < ctx.AngleMinStrokesRad)
        {
            return _atB ? _tooCloseAtB : _tooCloseAtA;
        }

        return Verdict.Accept;
    }
}


/**
 * The candidate must lie inside the cluster area.
 *
 * Deliberately NOT part of the restart pipeline. In the original this ran exactly once,
 * before the validation loop, and the loop never re-checked it after an endpoint was
 * snapped onto an existing junction. Running it per restart would be a behaviour
 * change, so Generator calls it once, in the same place as before.
 */
internal sealed class BoundsConstraint : ICandidateConstraint
{
    private static readonly Verdict _outOfBounds = Verdict.Reject("out of bounds");

    private readonly Vector2 _bl;
    private readonly Vector2 _tr;


    internal BoundsConstraint(Vector2 bl, Vector2 tr)
    {
        _bl = bl;
        _tr = tr;
    }


    public string Name => "bounds";


    public Verdict Check(Stroke cand, StrokeStore store, GenerationContext ctx)
    {
        bool inBounds = true
            && cand.A.Pos.X > _bl.X
            && cand.A.Pos.Y > _bl.Y
            && cand.A.Pos.X < _tr.X
            && cand.A.Pos.Y < _tr.Y
            && cand.B.Pos.X > _bl.X
            && cand.B.Pos.Y > _bl.Y
            && cand.B.Pos.X < _tr.X
            && cand.B.Pos.Y < _tr.Y;

        return inBounds ? Verdict.Accept : _outOfBounds;
    }
}
