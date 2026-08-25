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
    Restart
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


    private Verdict(VerdictKind kind, string reason)
    {
        Kind = kind;
        Reason = reason;
    }


    internal static readonly Verdict Accept = new(VerdictKind.Accept, null);
    internal static readonly Verdict Restart = new(VerdictKind.Restart, null);


    /**
     * Intended for `private static readonly` fields on the constraints, so that no
     * rejection allocates at generation time.
     */
    internal static Verdict Reject(string reason) => new(VerdictKind.Reject, reason);
}
