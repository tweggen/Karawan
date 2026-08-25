using System;
using System.Numerics;
using builtin.tools;
using engine.world;

namespace engine.streets.generation;


/**
 * Tunables governing what a junction grows next, snapshotted once per run.
 */
internal sealed class EmitterSettings
{
    internal float WeightMin;
    internal float WeightMax;
    internal float WeightDecreaseFactor;
    internal float WeightIncreaseFactor;
    internal float NewStrokeMinimum;
    internal float NewStrokeSquaredWeight;
    internal float NewLengthMin;
    internal float ProbabilityAngleSlightTurn;
    internal int AngleSlightTurnMax;

    /**
     * Cluster area, used for the edge buffer below.
     */
    internal Vector2 BottomLeft;
    internal Vector2 TopRight;
}


/**
 * Grows the successors of an accepted stroke, following the rule table.
 *
 * One of the five responsibilities that used to be fused into Generator.Generate().
 * Everything about WHAT gets emitted lives here and in the ruleset; Generator only
 * owns the work queue it gets handed back.
 */
internal sealed class SuccessorEmitter
{
    private readonly ExpansionRuleTable _table;
    private readonly RandomSource _rnd;
    private readonly ClusterDesc _clusterDesc;
    private readonly EmitterSettings _s;
    private readonly Action<Stroke> _enqueue;
    private readonly bool[] _fired;


    internal SuccessorEmitter(
        ExpansionRuleTable table, RandomSource rnd, ClusterDesc clusterDesc,
        EmitterSettings settings, Action<Stroke> enqueue)
    {
        _table = table;
        _rnd = rnd;
        _clusterDesc = clusterDesc;
        _s = settings;
        _enqueue = enqueue;
        _fired = new bool[table.Rules.Length];
    }


    private float _normWeight(float weight)
    {
        return (weight - _s.WeightMin) / (_s.WeightMax - _s.WeightMin);
    }


    /**
     * Emit the successor candidates of an accepted stroke, following the rule table.
     *
     * DRAW ORDER IS BEHAVIOUR. In sequence:
     *   1. one Get8 per rule, in table order;
     *   2. one Get8 for the slight-turn test, plus a GetFloat if it fires;
     *   3. per weight group that has a rule firing, two Get8 for the weight;
     *   4. a GetFloat per firing Random rule, as it is emitted.
     * This is exactly what the hard-coded version drew. See section 0.2 of the plan.
     */
    internal void Emit(in Stroke curr)
    {
        var rules = _table.Rules;
        var groups = _table.Groups;

        /*
         * 1. Which rules fire? normWeight is evaluated once; the original
         * recomputed it per rule from the same input, which is the same value.
         */
        float normalisedWeight = _normWeight(curr.Weight);
        bool anyFired = false;

        for (int i = 0; i < rules.Length; ++i)
        {
            _fired[i] = _rnd.Get8() < rules[i].Probability.Evaluate(normalisedWeight);
            anyFired |= _fired[i];
        }

        /*
         * 2. The slight turn is drawn whether or not anything fires.
         */
        float newAngle = curr.Angle;
        if (_rnd.Get8() < _s.ProbabilityAngleSlightTurn)
        {
            newAngle = newAngle + _rnd.GetFloat() * 2f * _s.AngleSlightTurnMax - _s.AngleSlightTurnMax;
        }

        if (!anyFired)
        {
            _fired[_table.FallbackRule] = true;
        }

        /*
         * 3. and 4. Per group, so that the two weight draws happen once for the
         * group rather than once per rule.
         */
        for (int g = 0; g < groups.Length; ++g)
        {
            bool groupFires = false;
            for (int i = 0; i < rules.Length; ++i)
            {
                if (rules[i].WeightGroup == g && _fired[i])
                {
                    groupFires = true;
                    break;
                }
            }

            if (!groupFires)
            {
                continue;
            }

            float groupWeight = _computeWeight(
                curr.Weight, groups[g].DecreaseProbability, groups[g].IncreaseProbability);

            float newLength = (int)((_s.NewStrokeMinimum
                + _s.NewStrokeSquaredWeight * (groupWeight * groupWeight)) * 10f) / 10f;
            if (newLength < _s.NewLengthMin)
            {
                newLength = _s.NewLengthMin;
            }

            for (int i = 0; i < rules.Length; ++i)
            {
                if (rules[i].WeightGroup != g || !_fired[i])
                {
                    continue;
                }

                _emitOne(curr, rules[i], newAngle, newLength, groupWeight);
            }
        }
    }


    private void _emitOne(
        in Stroke curr, in ExpansionRule rule,
        float newAngle, float newLength, float weight)
    {
        float angle;
        switch (rule.Direction)
        {
            case StrokeDirection.Forward:
                angle = newAngle;
                break;
            case StrokeDirection.Right:
                angle = newAngle - (float)Math.PI / 2f;
                break;
            case StrokeDirection.Left:
                angle = newAngle + (float)Math.PI / 2f;
                break;
            default:
                angle = _rnd.GetFloat() * (float)Math.PI * 2f;
                break;
        }

        StreetPoint newB = new StreetPoint() { ClusterId = _clusterDesc.Id };
        var stroke = Stroke.CreateByAngleFrom(
            _clusterDesc,
            curr.B,
            newB,
            angle,
            newLength,
            rule.KeepPrimary ? curr.IsPrimary : !curr.IsPrimary,
            weight
        );

        if (_isSuccessorWorthQueueing(stroke))
        {
            stroke.PushCreator(rule.Name);
            newB.PushCreator(rule.Name);
            _enqueue(stroke);
        }
    }


    /**
     * Moved out of the lambda it used to live in, unchanged.
     */
    private float _computeWeight(float currentWeight, float probDescrease, float probIncrease)
    {
        float resultWeight = currentWeight;
        bool doDecreaseWeight = _rnd.Get8() < probDescrease;
        bool doIncreaseWeight = _rnd.Get8() < probIncrease;

        if( doDecreaseWeight ) {
            resultWeight = resultWeight * _s.WeightDecreaseFactor;
        }
        if( doIncreaseWeight ) {
            resultWeight = resultWeight * _s.WeightIncreaseFactor;
        }

        if( resultWeight < _s.WeightMin ) {
            resultWeight = _s.WeightMin;
        } else {
            if( resultWeight > _s.WeightMax) {
                resultWeight = _s.WeightMax;
            }
        }
        resultWeight = (int)((resultWeight)*1000f)/1000f;

        return resultWeight;
    }


    /**
     * Whether a freshly emitted successor is worth putting on the queue at all.
     *
     * This is BEHAVIOUR, not a diagnostic, despite having grown up among the
     * orphan-tracking helpers that WP-2c deleted. The 15 m edge buffer below is
     * strictly tighter than the bounds check, so removing this would let candidates
     * near the cluster edge through and change every generated cluster.
     */
    private bool _isSuccessorWorthQueueing(in Stroke candidateStroke)
    {
        // Check if B endpoint (the new point) is in bounds
        var b = candidateStroke.B.Pos;
        if (b.X <= _s.BottomLeft.X || b.X >= _s.TopRight.X || b.Y <= _s.BottomLeft.Y || b.Y >= _s.TopRight.Y)
        {
            return false;  // Out of bounds
        }

        // Check if B is too close to cluster edges (would fail edge distance checks)
        const float edgeBuffer = 15f;
        float distToBoundary = MathF.Min(
            MathF.Min(b.X - _s.BottomLeft.X, _s.TopRight.X - b.X),
            MathF.Min(b.Y - _s.BottomLeft.Y, _s.TopRight.Y - b.Y)
        );
        if (distToBoundary < edgeBuffer)
        {
            return false;  // Too close to boundary
        }

        // Check if B is very close to A (minimum length constraint)
        float lengthToB = Vector2.Distance(candidateStroke.A.Pos, b);
        if (lengthToB < _s.NewStrokeMinimum * 0.8f)  // Conservative check
        {
            return false;  // Stroke too short
        }

        return true;  // Endpoint appears valid
    }
}
