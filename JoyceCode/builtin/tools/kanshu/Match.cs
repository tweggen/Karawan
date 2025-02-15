using System.Collections.Generic;

namespace builtin.tools.kanshu;


/**
 * Describes one rule match, including the bindings.
 */
public class Match
{
    public Match? Parent = null;
    public SortedDictionary<string, string>? Bindings = null;


    public bool HasBinding(string key, out string value)
    {
        if (Parent != null)
        {
            if (Parent.HasBinding(key, out value))
            {
                return true;
            }
        }

        if (Bindings != null)
        {

            if (Bindings.TryGetValue(key, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }
    
    /**
     * Try to add the given binding.
     * Fails, if there is a contradicting binding already stored.
     */
    public bool TryAddBinding(string key, string value)
    {
        string parentValue = default;
        if (Parent != null && Parent.HasBinding(key, out parentValue))
        {
            if (parentValue == value)
            {
                return true;
            }
            else
            {
                /*
                 * Parent has different value, does not match.
                 */
                return false;
            }
        }
        // TXWTODO: We have to include checks if the parent match contains a result.
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