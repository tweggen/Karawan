using System;
using System.Collections.Generic;

namespace builtin.tools.Lindenmayer;


/**
 * A lindenmayer system rule.
 * Rules are matched by name.
 * The parameters of the original part are passed to the transforming
 * function.
 */
public class Rule
{
    /**
     * Lindenmayer rules are matched directly by name.
     */
    public string Name;
    
    /**
     * The probability of this rule to match.
     */
    public float Probability;
    
    /**
     * Only if this condition matches, the rule matches.
     */
    public Func<Params, bool> Condition;
    
    /**
     * Returns the parts that shall replace the part that
     * matched.
     */
    public Func<Params, IList<Part>> TransformParts;
    
    /**
     * Built-in condition to always match. 
     */
    public static bool Always(Params _) => true;

    public Rule(
        string name,
        float probability,
        Func<Params, bool> condition,
        Func<Params, IList<Part>> transformParts
    ) {
        Name = name;
        Probability = probability;
        Condition = condition;
        TransformParts = transformParts;
    }

    public Rule(string name, Func<Params, IList<Part>> transformParts)
    {
        Name = name;
        Probability = 1f;
        Condition = Always;
        TransformParts = transformParts;
    }
}