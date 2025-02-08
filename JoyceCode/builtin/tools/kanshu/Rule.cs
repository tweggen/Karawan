using System;

namespace builtin.tools.kanshu;

public class Rule<TNodeLabel,TEdgeLabel>
{
    /**
     * Lindenmayer rules are matched directly by name.
     */
    public string Name { get; set; }
    
    /**
     * The probability of this rule to match.
     */
    public float Probability { get; set; }
    
    /**
     * Only if this condition matches, the rule matches.
     */
    public Func<Labels, bool> Condition { get; set; }


    // TXWTODO: Define call context as done for lsystems.
    // TXWTODO: Unify the context for lua calls, the context for Lsystems and this
    // TXWTODO: Decide when to stop all these genericsd.
    // public Func<bool> Condition;
    public Pattern<TNodeLabel, TEdgeLabel> Pattern { get; set; }
    
    /**
     * A function replacing the input parts with a given replacement
     * graph.
     *
     * @param properties
     *     the bound properties of the matched pattern
     * @returns
     *     a graph to replace the original patter with.
     */
    public Func<Labels, Graph<TNodeLabel, TEdgeLabel>> Replacement { get; set; }
}