using System.Collections.Generic;

namespace builtin.tools.kanshu;

public class MatchResult
{
    // TXWTODO: This should include bindings, shouldn't it`?
    public Dictionary<int, Graph.Node> Nodes { get; set; }

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
