using System.Collections.Generic;

namespace builtin.tools.kanshu;


/**
 * Describes one rule match, including the bindings.
 */
public class Match
{
    public Dictionary<int, Graph.Node> Nodes { get; set; }
    public SortedDictionary<string, string>? Bindings = null;

    public Rule Rule;
    
    /**
     * Try to add the given binding.
     * Fails, if there is a contradicting binding already stored.
     */
    public bool TryAddBinding(string key, string value)
    {
        if (Bindings != null)
        {
            if (Bindings.TryGetValue(key, out var oldValue))
            {
                if (value != oldValue)
                {
                    return false;
                }

                return true;
            }
            else
            {
                Bindings.Add(key, value);
                return true;
            }
        }
        else
        {
            Bindings = new();
            Bindings.Add(key, value);
            return true;
        }
    }
    
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