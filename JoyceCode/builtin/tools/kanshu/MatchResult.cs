using System.Collections.Generic;

namespace builtin.tools.kanshu;

public class MatchResult<TNodeLabel, TEdgeLabel>
{
    // TXWTODO: This should include bindings, shouldn't it`?
    public Dictionary<int, Graph<TNodeLabel, TEdgeLabel>.Node> Nodes { get; set; }

    public override string ToString()
    {
        string str = "[";
        bool isFirst = true; 
        foreach (var kvp in Nodes)
        {
            if (!isFirst) str += ",";
            else isFirst = false;
            str += $"{kvp.Value}";
        }

        str += "]";
        return str;
    }
}
