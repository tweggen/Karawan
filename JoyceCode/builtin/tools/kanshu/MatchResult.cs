using System.Collections.Generic;

namespace builtin.tools.kanshu;

public class MatchResult
{
    public Dictionary<int, Graph.Node> Nodes { get; set; }

    public Rule Rule { get; set; }
    public Scope Scope { get; set; }
}
