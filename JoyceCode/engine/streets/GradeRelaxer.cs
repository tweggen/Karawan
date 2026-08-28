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
        for (; sweep < policy.MaxSweeps; ++sweep)
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

                _add(delta, s.A.Id, wA * excess);
                _add(delta, s.B.Id, -wB * excess);
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
