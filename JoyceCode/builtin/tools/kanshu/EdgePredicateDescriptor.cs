using System;

namespace builtin.tools.kanshu;

public class EdgePredicateDescriptor<TEdgeLabel>
{
    public Predicate<TEdgeLabel> Predicate { get; set; }
    public int NodeFrom { get; set; }
    public int NodeTo { get; set; }

}