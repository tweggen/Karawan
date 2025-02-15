using System;

namespace builtin.tools.kanshu;

public class EdgePredicateDescriptor
{
    public Func<Match, Labels, Match> Predicate { get; set; }
    public int NodeFrom { get; set; }
    public int NodeTo { get; set; }
}