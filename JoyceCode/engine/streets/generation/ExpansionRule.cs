using System;
using System.Collections.Generic;

namespace engine.streets.generation;


internal enum StrokeDirection : byte
{
    Forward,
    Right,
    Left,
    Random
}


internal enum ProbKind : byte
{
    /**
     * A fixed 0..256 probability.
     */
    Constant,

    /**
     * a - normalisedWeight * b. Heavier streets branch off at random less often.
     */
    Affine,

    /**
     * a / (1 + b * (1 - normalisedWeight)). Heavier streets sprout side streets more.
     */
    Hyperbola
}


/**
 * How likely a rule is to fire, as a function of the parent stroke's weight.
 *
 * Deliberately a closed set of three shapes rather than an expression evaluator. Two
 * of them are the only shapes the generator has ever used; the third is a plain
 * constant. A ruleset naming anything else is rejected when it is parsed, not when it
 * is first drawn against.
 */
internal readonly struct ProbExpr
{
    internal readonly ProbKind Kind;
    internal readonly float A;
    internal readonly float B;


    internal ProbExpr(ProbKind kind, float a, float b)
    {
        Kind = kind;
        A = a;
        B = b;
    }


    internal static ProbExpr Constant(float a) => new(ProbKind.Constant, a, 0f);
    internal static ProbExpr Affine(float a, float b) => new(ProbKind.Affine, a, b);
    internal static ProbExpr Hyperbola(float a, float b) => new(ProbKind.Hyperbola, a, b);


    /**
     * Reproduces the original inline expressions exactly, including the truncation to
     * int. Do not "simplify" the arithmetic here.
     */
    internal int Evaluate(float normalisedWeight)
    {
        switch (Kind)
        {
            case ProbKind.Constant:
                return (int) A;

            case ProbKind.Affine:
                /* was: (int)(80 - normWeight(weight) * 60f) */
                return (int) (A - normalisedWeight * B);

            case ProbKind.Hyperbola:
                /* was: (int)(150f / (1 + 4f * (1f - normWeight(weight)))) */
                return (int) (A / (1 + B * (1f - normalisedWeight)));

            default:
                return 0;
        }
    }
}


/**
 * Rules sharing a weight group draw their weight once, together.
 *
 * This is not a tidiness choice: the original computed one weight for the straight
 * pair and one for the branch pair, so grouping is what keeps the number and order of
 * random draws unchanged.
 */
internal readonly struct WeightGroupSpec
{
    internal readonly string Name;
    internal readonly float DecreaseProbability;
    internal readonly float IncreaseProbability;


    internal WeightGroupSpec(string name, float decreaseProbability, float increaseProbability)
    {
        Name = name;
        DecreaseProbability = decreaseProbability;
        IncreaseProbability = increaseProbability;
    }
}


internal readonly struct ExpansionRule
{
    /**
     * Also used as the stroke's Creator tag, so it shows up in diagnostics.
     */
    internal readonly string Name;

    internal readonly StrokeDirection Direction;

    /**
     * Index into ExpansionRuleTable.Groups.
     */
    internal readonly int WeightGroup;

    internal readonly ProbExpr Probability;

    /**
     * Whether the successor keeps the parent's primary/secondary orientation or flips
     * it. Branches flip.
     */
    internal readonly bool KeepPrimary;

    /**
     * Fired when nothing else did, so that growth never stalls.
     */
    internal readonly bool IsFallback;


    internal ExpansionRule(string name, StrokeDirection direction, int weightGroup,
        ProbExpr probability, bool keepPrimary, bool isFallback = false)
    {
        Name = name;
        Direction = direction;
        WeightGroup = weightGroup;
        Probability = probability;
        KeepPrimary = keepPrimary;
        IsFallback = isFallback;
    }
}


/**
 * What a junction does next.
 *
 * TWO ORDERINGS LIVE IN HERE and they are not the same one:
 *
 *  - `Rules` is in PROBABILITY DRAW order. One Get8 is drawn per rule, in this order,
 *    for every accepted stroke. Reordering the array reorders the random sequence and
 *    changes every generated cluster.
 *  - Weight computation and emission run per weight group, and within a group in the
 *    order the rules appear in `Rules`. With the default table that yields
 *    straight(forward, random) then branch(right, left) — which is exactly what the
 *    original hard-coded, even though it does not match the draw order above.
 *
 * See docs/roadmap/proposed/STREETS-GENERATOR-REWORK-PLAN.md section 0.2.
 */
internal sealed class ExpansionRuleTable
{
    internal readonly ExpansionRule[] Rules;
    internal readonly WeightGroupSpec[] Groups;

    /**
     * Index into Rules of the rule to force when nothing fires.
     */
    internal readonly int FallbackRule;


    internal ExpansionRuleTable(IReadOnlyList<ExpansionRule> rules, IReadOnlyList<WeightGroupSpec> groups)
    {
        if (null == rules || rules.Count == 0)
        {
            throw new InvalidOperationException("Street generation ruleset has no rules.");
        }

        if (null == groups || groups.Count == 0)
        {
            throw new InvalidOperationException("Street generation ruleset has no weight groups.");
        }

        Rules = new ExpansionRule[rules.Count];
        FallbackRule = -1;

        for (int i = 0; i < rules.Count; ++i)
        {
            Rules[i] = rules[i];

            if (Rules[i].WeightGroup < 0 || Rules[i].WeightGroup >= groups.Count)
            {
                throw new InvalidOperationException(
                    $"Street generation rule '{Rules[i].Name}' names weight group " +
                    $"{Rules[i].WeightGroup}, but only {groups.Count} are defined.");
            }

            if (Rules[i].IsFallback)
            {
                if (FallbackRule >= 0)
                {
                    throw new InvalidOperationException(
                        $"Street generation ruleset marks more than one fallback rule " +
                        $"('{Rules[FallbackRule].Name}' and '{Rules[i].Name}').");
                }
                FallbackRule = i;
            }
        }

        if (FallbackRule < 0)
        {
            throw new InvalidOperationException(
                "Street generation ruleset marks no fallback rule. Without one, a junction " +
                "where nothing fires stops growing and the cluster comes out sparse.");
        }

        Groups = new WeightGroupSpec[groups.Count];
        for (int i = 0; i < groups.Count; ++i)
        {
            Groups[i] = groups[i];
        }
    }


    /**
     * The shipped ruleset, value-identical to the constants the generator used before
     * WP-3. If no configuration is present this is what runs, and the determinism
     * fingerprints are recorded against it.
     */
    internal static ExpansionRuleTable Defaults()
    {
        return new ExpansionRuleTable(
            new[]
            {
                new ExpansionRule("forward", StrokeDirection.Forward, 0,
                    ProbExpr.Constant(252f), keepPrimary: true),
                new ExpansionRule("right", StrokeDirection.Right, 1,
                    ProbExpr.Hyperbola(150f, 4f), keepPrimary: false),
                new ExpansionRule("left", StrokeDirection.Left, 1,
                    ProbExpr.Hyperbola(150f, 4f), keepPrimary: false),
                new ExpansionRule("randStroke", StrokeDirection.Random, 0,
                    ProbExpr.Affine(80f, 60f), keepPrimary: true, isFallback: true),
            },
            new[]
            {
                new WeightGroupSpec("straight", 5f, 10f),
                new WeightGroupSpec("branch", 190f, 3f),
            });
    }
}
