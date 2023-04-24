using System;
using System.Collections.Generic;

namespace builtin.tools.Lindenmayer;

public class Rule
{
    public string Name;
    public float Probability;
    public Func<Params, bool> Condition;
    public Func<Params, IList<Part>> TransformParts;

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
}