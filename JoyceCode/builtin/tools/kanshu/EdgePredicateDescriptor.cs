using System;

namespace builtin.tools.kanshu;

public class EdgePredicateDescriptor<TEdgeLabel>
{
    public Func<TEdgeLabel, bool> Predicate { get; set; }
    public int NodeFrom { get; set; }
    public int NodeTo { get; set; }
}