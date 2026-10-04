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
     *
     * ⚠️ It was 32 until 2026-09-06, and every terrain-following city in the game -
     * which is the shipped city - spent all 32 without settling and said nothing about
     * it. Measured over the eight seeds StreetDeterminismTests pins, the sweeps
     * GradeRelaxer's successive projection needs to bring every stroke within a
     * centimetre of its own limit are 11, 14, 18, 28, 51, 57 and 80 - the last being
     * Yelukhdidru@3000, the largest city the game builds. 256 is three times that.
     *
     * It is not a licence to iterate: the sweep rule that shipped needed 82 to 1106
     * sweeps for the same result, so raising this number on its own would have been eight
     * times the budget for a quarter of the job. It buys headroom over a measured worst
     * case, and exhausting it is now reported - RelaxedStreetHeight is the caller that
     * complains.
     */
    public int MaxSweeps { get; set; } = 256;

    /**
     * Settled when no stroke the sweep is allowed to move is more than this many metres
     * of rise over its own limit. A centimetre is far below anything visible on a road
     * surface.
     *
     * ⚠️ This used to be compared against the largest CORRECTION a sweep applied, which
     * is a different question and a weaker one: a damped sweep's corrections shrink
     * geometrically whether or not the network has become buildable, so the old rule
     * called Yelukhdidru@3000 settled with 312 of its 1875 strokes still over their
     * limit, one of them by 0.58 m of rise. What this pass exists to guarantee is the
     * limit, so the limit is what it measures.
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


    /**
     * Steepest a bridge deck or a tunnel bore may be, and the number a corridor is
     * refused on.
     *
     * ⚠️ Nothing designs this. Both ramps climb MaxRampGrade from their own feet, so
     * whatever the two feet disagree by lands on the deck - measured over six generated
     * cities on the shipped terrain at +4.2, +11.4, +4.3, -4.7, +23.7 and +21.6 percent,
     * and 23.7 % is not a bridge, it is a ramp and a half (§10.5). The corridor-fit table
     * of §2 cannot see this at all: it asks whether two ramps FIT, not whether the two
     * ends are at similar enough heights for the deck between them to be a deck.
     *
     * TWO EXISTING QUANTITIES, AND DELIBERATELY NO NEW NUMBER:
     *
     * - **the road's own limit.** A deck is not a special kind of climb the way a ramp
     *   is; it is the road, carried in the air. So it is held to exactly what that road
     *   would be held to on the ground - MaxGradeFor, the same interpolation over weight
     *   every other stroke goes down, which is 5 % for the heaviest corridor and 14 % for
     *   the lightest. Anything else would be a second expression for "how steep may this
     *   be", which is what MaxGradeFor exists not to have.
     * - **and never steeper than its own ramps.** A deck that out-climbs the ramps
     *   leading to it is not a deck; it is a longer ramp with a different name. That
     *   binds only below weight 0.69, where the interpolation is above MaxRampGrade.
     *
     * So a heavy corridor gets 5 %, a light one 10 %, and every value in between comes
     * from a number that already existed and was already argued for.
     */
    public float MaxDeckGradeFor(Stroke stroke)
    {
        return Single.Min(MaxGradeFor(stroke), MaxRampGrade);
    }
}
