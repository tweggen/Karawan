using System;
using System.Collections.Generic;
using System.Linq;
using static engine.Logger;

namespace engine.streets;


/**
 * Takes raw junction heights and settles them into a network no road would be ashamed
 * of.
 *
 * Sampling terrain under each junction gives a three-dimensional city immediately, but
 * with whatever gradients the noise happened to produce - including ones no road would
 * ever be built on. This is the pass that fixes that, and it is deliberately a pure
 * function of (stroke graph, starting heights, policy): no terrain, no fragments, no
 * engine, so it can be tested exhaustively and cheaply.
 *
 * **Heights are per junction, not per stroke end.** That is the invariant the whole
 * non-planar story rests on - a junction is one node in the graph, so relaxing it moves
 * every street meeting there at once and the network cannot come apart. It also makes
 * the relaxation converge on something consistent rather than fighting itself.
 *
 * Jacobi rather than Gauss-Seidel: corrections are accumulated over a whole sweep and
 * applied together, so the result does not depend on the order strokes are visited.
 * Strokes are still walked in a fixed order (by Sid) so that floating point addition
 * order is fixed too - the arithmetic-identity rule this project generates cities under.
 *
 * **This is a boundary value problem, and it has boundaries.** A grade separated
 * structure is built to a profile rather than draped over the ground, so the junctions a
 * Ramp, Bridge or Tunnel touches are fixed: StructureProfile gives them their designed
 * heights, and the sweep then settles everything else AROUND them. A stroke with one
 * fixed end therefore hands its whole correction to the other end - the resistance split
 * has nothing to split - and a stroke with two fixed ends is not corrected at all, which
 * is the point: the relaxer must not iron a 10 % ramp down to the 5 % its corridor's
 * weight would otherwise permit.
 *
 * A boundary value has to come from somewhere, and where it comes from is the ANCHOR
 * PASS: the network is first relaxed AS IF the structures were not there, and the
 * structure is designed from the height its feet have in that city. See the comment on
 * that call - designing from the raw terrain sample instead moves a foot by up to 27 m.
 *
 * Nothing in a city the shipped ruleset builds is a structure, so the pinned set is
 * empty there, every stroke takes the same branch it always took, and the arithmetic is
 * unchanged float for float. WP-B3a.2 pins that over eight generated cities rather than
 * arguing it from this comment.
 */
public static class GradeRelaxer
{
    private static readonly engine.Dc _dc = engine.Dc.StreetGen;


    /**
     * @param strokes
     *     Every stroke in the cluster. Order does not matter; it is sorted internally.
     * @param heights
     *     Starting height per junction id, modified in place.
     * @param policy
     *     How steep each class of street may be.
     * @returns
     *     How many sweeps were used. Reaching policy.MaxSweeps means the network had
     *     not settled - useful to a caller that wants to complain about it, and the
     *     reason this is not void.
     */
    public static int Relax(
        IEnumerable<Stroke> strokes, Dictionary<int, float> heights, GradePolicy policy)
    {
        var ordered = strokes
            .Where(s => null != s.A && null != s.B)
            .OrderBy(s => s.Sid)
            .ToList();

        if (0 == ordered.Count)
        {
            return 0;
        }

        /*
         * The boundary. Empty for every city the shipped ruleset builds, in which case
         * everything below this is the loop it has always been.
         */
        HashSet<int> pinned = StructureProfile.PinnedJunctionsOf(ordered);

        int anchorSweeps = 0;
        if (pinned.Count > 0)
        {
            /*
             * ⚠️ THE ANCHOR PASS, and it is not decoration.
             *
             * A structure is designed from the height of its FEET, and the feet are
             * ordinary junctions of the ordinary city - so the height to design from is
             * the one the city already settled on, not the raw terrain sample under
             * them. Measured over six generated cities on the shipped terrain, the two
             * differ by up to 27.1 m at a single foot. Designing from the raw sample and
             * then pinning it would drag the existing road, its approaches and
             * everything cornering on them down into the noise the relaxation exists to
             * take out - the structure moving the city rather than standing on it.
             *
             * So: relax the network AS IF the structure were not there, which is exactly
             * this same function over the strokes that are not part of one, and take the
             * feet from that. It is not a special case of the sweep; it is the sweep, on
             * a smaller graph, and it can recurse no further because the filtered list
             * contains no structure.
             *
             * The property this buys, measured over six generated cities rather than
             * argued: adding a structure leaves every ordinary junction at EXACTLY the
             * height it had without one - 0 of 274, 0 of 785, 0 of 1379 moved, worst
             * 0.0000 m. A structure hangs off the city; the city does not move to
             * accommodate it.
             */
            anchorSweeps = Relax(
                ordered.Where(s => !StrokeKinds.IsStructure(s.Kind)).ToList(), heights, policy);

            StructureProfile.Design(ordered, heights, policy);
        }

        /*
         * The sweep budget is spent ONCE over the whole relaxation, not once per pass.
         * WP-B2.6 made the same call about the generation budget and for the same
         * reason: a second allowance handed out by an internal stage is a city that
         * settles further because of something that should not have changed it. With
         * the split, adding a structure leaves every ordinary junction of a generated
         * city at exactly the height it had without one - measured over six cities.
         */
        int remaining = Int32.Max(0, policy.MaxSweeps - anchorSweeps);

        return anchorSweeps + RelaxAround(ordered, heights, policy, pinned, remaining);
    }


    /**
     * The sweep itself, with the boundary handed to it.
     *
     * Separate from Relax so that what a fixed junction does to its neighbours can be
     * driven directly, on the production method rather than on a lookalike of it.
     *
     * ⚠️ In the flag-off game the boundary is always empty. And with a boundary, what
     * measurement says is this: GradeRelaxer exhausts its whole 32 sweep budget on every
     * real city (pre-existing, and RelaxedStreetHeight does not look at the return value
     * that says so), so the anchor pass leaves nothing of the allowance and this call
     * does no sweep at all; on a network small enough for the anchor to converge, there
     * is nothing over its limit left for it to correct. The boundary rules below are
     * therefore a GUARANTEE that no sweep can bend a designed structure rather than a
     * step some city depends on - which is why they are driven here directly, and why
     * saying so is better than implying that a generated city exercises them.
     *
     * @param ordered
     *     Strokes with both endpoints, already ordered by Sid.
     * @param pinned
     *     Junctions the sweep may not move. Empty for every city the shipped ruleset
     *     builds, in which case this is the loop it has always been, float for float.
     * @param maxSweeps
     *     What is left of policy.MaxSweeps. Passed rather than read off the policy so
     *     that the anchor pass and this one share one allowance instead of each getting
     *     their own.
     */
    internal static int RelaxAround(
        List<Stroke> ordered, Dictionary<int, float> heights, GradePolicy policy,
        HashSet<int> pinned, int maxSweeps)
    {
        /*
         * How hard a junction is to move: the heaviest street meeting it. A crossroads
         * where an arterial meets an alley moves as the arterial dictates, which is the
         * whole point of weighting - without it the two would split the difference and
         * the arterial would end up following the terrain after all.
         */
        var resistance = new Dictionary<int, float>();
        var degree = new Dictionary<int, int>();
        foreach (var s in ordered)
        {
            _raiseTo(resistance, s.A.Id, s.Weight);
            _raiseTo(resistance, s.B.Id, s.Weight);
            _bump(degree, s.A.Id);
            _bump(degree, s.B.Id);
        }

        /*
         * One damping factor for the whole graph, not one per junction.
         *
         * Damping at all is what stops the sweep oscillating: a junction on a ridge is
         * pushed down by every street running off it, and applying all of those in full
         * overshoots past the valley. Dividing by each junction's OWN degree would damp
         * it just as well - but then the two ends of a stroke get divided by different
         * numbers, the equal and opposite pair no longer cancels, and the network as a
         * whole creeps uphill or down. A single divisor keeps every pair balanced, so
         * the only thing that can move the overall level is the weighting, which is
         * supposed to.
         */
        int busiest = 1;
        foreach (var d in degree.Values)
        {
            if (d > busiest) busiest = d;
        }

        var delta = new Dictionary<int, float>();

        int nUnheighted = 0;

        int sweep = 0;
        for (; sweep < maxSweeps; ++sweep)
        {
            delta.Clear();

            foreach (var s in ordered)
            {
                float length = s.Length;
                if (length < 0.001f)
                {
                    continue;
                }

                /*
                 * A stroke whose endpoint has no starting height cannot be relaxed, and
                 * quietly skipping it would leave exactly the unbuildable grade this
                 * pass exists to remove - with nothing in the log to say so. It means
                 * the caller built the height table from a different set of junctions
                 * than the strokes it passed, so it is reported rather than absorbed.
                 */
                if (!heights.TryGetValue(s.A.Id, out float hA)
                    || !heights.TryGetValue(s.B.Id, out float hB))
                {
                    ++nUnheighted;
                    continue;
                }

                float rise = hB - hA;
                float limit = policy.MaxGradeFor(s) * length;

                if (Single.Abs(rise) <= limit)
                {
                    continue;
                }

                /*
                 * Only the excess is taken out. Correcting to the limit rather than to
                 * flat is what lets the city keep the shape of the ground it stands on.
                 */
                float excess = rise > 0f ? rise - limit : rise + limit;

                float rA = resistance[s.A.Id];
                float rB = resistance[s.B.Id];
                float total = rA + rB;

                /*
                 * Split inversely to resistance, so the lighter end does more of the
                 * moving. Equal weights fall back to half and half.
                 */
                float wA = total > 1e-6f ? rB / total : 0.5f;
                float wB = 1f - wA;

                bool pinA = pinned.Contains(s.A.Id);
                bool pinB = pinned.Contains(s.B.Id);

                if (pinA && pinB)
                {
                    /*
                     * A structure's own stroke. Both ends are designed, so there is
                     * nothing here to correct and nowhere to put a correction.
                     */
                    continue;
                }

                if (pinA)
                {
                    /*
                     * The whole excess, not this end's share of it. Splitting it would
                     * leave the free end taking a fraction of the correction each sweep
                     * and the stroke over its limit until the geometric series had run -
                     * which is not the same thing as "the neighbours absorb it".
                     */
                    _add(delta, s.B.Id, -excess);
                }
                else if (pinB)
                {
                    _add(delta, s.A.Id, excess);
                }
                else
                {
                    _add(delta, s.A.Id, wA * excess);
                    _add(delta, s.B.Id, -wB * excess);
                }
            }

            float largest = 0f;
            foreach (var entry in delta.OrderBy(e => e.Key))
            {
                float move = entry.Value / busiest;
                heights[entry.Key] += move;

                float magnitude = Single.Abs(move);
                if (magnitude > largest) largest = magnitude;
            }

            if (largest < policy.ConvergenceEpsilon)
            {
                ++sweep;
                break;
            }
        }

        if (nUnheighted > 0)
        {
            Warning(_dc,
                $"{nUnheighted / Math.Max(1, sweep)} of {ordered.Count} strokes had no "
                + "starting height for an endpoint and were left at whatever grade the "
                + "terrain gave them.");
        }

        return sweep;
    }


    private static void _raiseTo(Dictionary<int, float> d, int key, float value)
    {
        if (d.TryGetValue(key, out float existing))
        {
            if (value > existing) d[key] = value;
        }
        else
        {
            d[key] = value;
        }
    }


    private static void _add(Dictionary<int, float> d, int key, float value)
    {
        d[key] = d.TryGetValue(key, out float existing) ? existing + value : value;
    }


    private static void _bump(Dictionary<int, int> d, int key)
    {
        d[key] = d.TryGetValue(key, out int existing) ? existing + 1 : 1;
    }
}
