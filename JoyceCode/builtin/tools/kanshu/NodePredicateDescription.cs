using System;

namespace builtin.tools.kanshu;

public class NodePredicateDescriptor
{
    public Func<Labels, bool> Predicate { get; set; }
    public int NodeFrom { get; set; }
    public int NodeTo { get; set; }
    public int Id { get; set; } = -1;
}