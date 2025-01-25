using System;

namespace builtin.tools.kanshu;

public class Rule<TNodeLabel,TEdgeLabel>
{
    public float Probability;

    // TXWTODO: Define call context as done for lsystems.
    // TXWTODO: Unify the context for lua calls, the context for Lsystems and this
    // TXWTODO: Decide when to stop all these genericsd.
    // public Func<bool> Condition;
    public Pattern<TNodeLabel, TEdgeLabel> Pattern { get; init; }
    public Graph<TNodeLabel, TEdgeLabel> Replacement { get; init; }
}