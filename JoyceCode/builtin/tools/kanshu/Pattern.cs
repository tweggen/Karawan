using System.Collections.Generic;

namespace builtin.tools.kanshu;

public class Pattern<TNodeLabel, TEdgeLabel>
{
    public List<PatternNode> Nodes { get; set; }

    public class PatternNode
    {
        public TNodeLabel Label { get; set; }
        public List<PatternEdge> RequiredConnections { get; set; } = new();
    }

    public class PatternEdge
    {
        public TEdgeLabel Label { get; set; }
        public int TargetNodeIndex { get; set; } // Index in pattern's Nodes list
    }
}
