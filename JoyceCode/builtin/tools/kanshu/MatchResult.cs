using System.Collections.Generic;

namespace builtin.tools.kanshu;

public class MatchResult
{
    /**
     * Node template n from the pattern matches the given node in real life.
     */
    public Dictionary<int, Graph.Node> Nodes { get; set; }

    public Rule Rule { get; set; }
    public Scope Scope { get; set; }
}
