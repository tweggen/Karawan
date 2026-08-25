namespace engine.streets.generation;


internal enum VerdictKind
{
    /**
     * This constraint has nothing against the candidate. Continue with the next one.
     */
    Accept,

    /**
     * The candidate is discarded.
     */
    Reject,

    /**
     * The constraint modified the candidate in place (typically by moving an endpoint
     * onto an existing junction). Every constraint must run again from the start,
     * because an earlier one may now have something to say about it.
     */
    Restart,

    /**
     * The candidate crosses an existing stroke. That stroke has to be split at the
     * crossing, and the candidate re-queued in one or two pieces. Carries everything
     * the driver needs to do it.
     */
    Split
}


/**
 * The outcome of one constraint.
 *
 * Replaces the doAdd / continueCheck flag pair that used to steer a 400 line
 * while loop through about ten exit points.
 *
 * Instances are shared, never allocated per check: this runs several times per
 * candidate and tens of thousands of times per cluster, and the cost gate compares
 * allocated bytes against a recorded baseline.
 */
internal sealed class Verdict
{
    internal readonly VerdictKind Kind;

    /**
     * Why the candidate was rejected. Diagnostics only; aggregated by
     * GenerationReport once WP-2c introduces it.
     */
    internal readonly string Reason;

    /**
     * Split only: the stored stroke to cut in two.
     */
    internal readonly Stroke SplitTarget;

    /**
     * Split only: the new junction, already positioned. Created by the constraint
     * rather than by the driver because StreetPoint.SetPos quantises to 10 cm, and the
     * decision below is taken on the quantised position. Deriving it anywhere else
     * would mean duplicating that quantisation.
     */
    internal readonly StreetPoint SplitPoint;

    /**
     * Split only: whether the part of the candidate beyond the crossing is re-queued.
     * False when the crossing lands close to the far end of the stroke being split.
     */
    internal readonly bool GenerateTail;


    private Verdict(VerdictKind kind, string reason,
        Stroke splitTarget = null, StreetPoint splitPoint = null, bool generateTail = false)
    {
        Kind = kind;
        Reason = reason;
        SplitTarget = splitTarget;
        SplitPoint = splitPoint;
        GenerateTail = generateTail;
    }


    internal static readonly Verdict Accept = new(VerdictKind.Accept, null);
    internal static readonly Verdict Restart = new(VerdictKind.Restart, null);


    /**
     * Intended for `private static readonly` fields on the constraints, so that no
     * rejection allocates at generation time.
     */
    internal static Verdict Reject(string reason) => new(VerdictKind.Reject, reason);


    /**
     * Unavoidably allocates, but only once per actual crossing.
     */
    internal static Verdict Split(Stroke target, StreetPoint at, bool generateTail)
        => new(VerdictKind.Split, null, target, at, generateTail);
}
