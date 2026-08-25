using System;

namespace engine.streets.generation;


/**
 * Tunables a constraint needs, snapshotted once per Generate() run.
 *
 * The Generator exposes these as settable properties, so they are read once at the
 * start of a run rather than per candidate: a constraint must not observe a tunable
 * changing halfway through a cluster.
 */
internal sealed class GenerationContext
{
    internal float MinPointToCandPointDistance;
    internal float MinPointToCandStrokeDistance;
    internal float MinPointToCandIntersectionDistance;

    /**
     * Precomputed exactly as the original inline expression
     * `AngleMinStrokes * (float)Math.PI / 180f`.
     */
    internal float AngleMinStrokesRad;

    internal int ClusterId;

    internal bool IsTracing;
}


/**
 * One rule about whether a candidate stroke may join the network.
 *
 * Each of these was a block inside Generator.Generate()'s validation loop, reachable
 * only by running the whole generator. As separate classes they can be exercised
 * against a hand-built store of three or four strokes.
 *
 * WARNING: the ORDER in which constraints run is part of the generated output — an
 * earlier rejection means a later constraint never gets to modify the candidate. The
 * pipeline order in Generator is exactly the order these checks appeared in the
 * original loop and must not be rearranged for tidiness. See
 * docs/roadmap/proposed/STREETS-GENERATOR-REWORK-PLAN.md section 0.2.
 */
internal interface ICandidateConstraint
{
    /**
     * Short stable name, used in diagnostics.
     */
    string Name { get; }


    /**
     * Judge the candidate. May modify it in place, but only together with
     * VerdictKind.Restart, and must never touch the store.
     */
    Verdict Check(Stroke cand, StrokeStore store, GenerationContext ctx);
}
