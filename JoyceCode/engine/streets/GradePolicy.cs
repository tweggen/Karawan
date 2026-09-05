using System;

namespace engine.streets;


/**
 * How steep a street is allowed to be, by what kind of street it is.
 *
 * Not a stylistic knob: this is how roads are actually designed. An arterial is held to
 * a shallower maximum grade than a service alley, which is why, where the two meet a
 * hill, it is the alley that does the climbing. Stroke.Weight already carries that
 * hierarchy, so the policy is a straight interpolation over it.
 *
 * The numbers are deliberate but not sacred - real limits are in this range (motorways
 * around 4-6 %, distributor roads 8 %, residential streets 10-15 %, and San Francisco
 * has public streets over 30 %). They live here rather than in models/nogame.streets.json
 * only because that file's parser refuses unknown fields by design; moving them out is a
 * follow-up, not a redesign.
 */
public sealed class GradePolicy
{
    /**
     * Grade permitted to the lightest street in the network, as rise over run.
     */
    public float MaxGradeAtMinWeight { get; set; } = 0.14f;

    /**
     * Grade permitted to the heaviest. Lower, because a heavy road bends the terrain
     * rather than the other way round.
     */
    public float MaxGradeAtMaxWeight { get; set; } = 0.05f;

    /**
     * The weight range Generator works in. Kept as fields rather than read from the
     * generator so that this stays a plain value with no dependencies.
     */
    public float WeightMin { get; set; } = 0.2f;
    public float WeightMax { get; set; } = 1.3f;

    /**
     * Grade a ramp between two decks is built to, as rise over run.
     *
     * A ramp is the one stroke whose grade is a DESIGN rather than a consequence, so it
     * does not interpolate over weight: whatever a structure's corridor weighs, its
     * ramps climb at this and its profile follows.
     *
     * Ten percent, and it is a decision rather than a tuning constant - see §2a/D1 of
     * STREETS-3D-PHASE-B-CROSSING-POLICY. At five percent, the grade a heavy road is
     * held to, a ramp needs 160 m and NOTHING is buildable in any generated city. At
     * fourteen, the grade this policy allows the lightest alley, the largest city gets
     * 294 structures and grade separation stops being a feature and becomes the norm.
     * Ten gives a handful per city, and is already steeper than any road the deck it
     * leads to carries - which is how real interchanges are built.
     */
    public float MaxRampGrade { get; set; } = 0.10f;

    /**
     * Give up after this many sweeps even if the network is still settling. A cap
     * matters more than the exact value: a pathological terrain must not be able to
     * stall cluster generation.
     */
    public int MaxSweeps { get; set; } = 32;

    /**
     * Stop once the largest correction in a sweep is smaller than this, in metres.
     * A centimetre is far below anything visible on a road surface.
     */
    public float ConvergenceEpsilon { get; set; } = 0.01f;


    /**
     * Steepest grade this stroke may have, interpolated over its weight and clamped to
     * the ends of the range so an out-of-range weight cannot produce a negative or
     * absurd limit.
     */
    public float MaxGradeFor(Stroke stroke)
    {
        /*
         * A ramp only. Not "anything that is not a Street": ConnectorBridge strokes
         * exist in every shipped city and are ordinary ground roads, and a Bridge or
         * Tunnel deck spans between two junctions its ramps have already placed, so its
         * grade is not something anybody chooses. Naming the one kind keeps the shipped
         * city's every stroke on the interpolation below, which WP-B3a.2 pins bit for
         * bit over eight generated cities.
         */
        if (StrokeKind.Ramp == stroke.Kind)
        {
            return MaxRampGrade;
        }

        float span = WeightMax - WeightMin;
        float t = span > 1e-6f
            ? Single.Clamp((stroke.Weight - WeightMin) / span, 0f, 1f)
            : 0f;

        return MaxGradeAtMinWeight + t * (MaxGradeAtMaxWeight - MaxGradeAtMinWeight);
    }
}
