using System.Collections.Generic;

namespace builtin.tools.kanshu;


/**
 * Describes one rule match, including the bindings.
 */
public class Scope
{
    public Scope? Parent = null;
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
    
    
    public override string ToString()
    {
        string strParent;
        if (Parent != null)
        {
            strParent = Parent.ToString();
        }
        else
        {
            strParent = "null";
        }

        string str = $"{{\"parent\": {strParent}, \"bindings\": {{";
        bool isFirst = true; 
        foreach (var kvp in Bindings)
        {
            if (!isFirst) str += ",";
            else isFirst = false;
            str += $"\"{kvp.Key}\": \"{kvp.Value}\"";
        }
        str += "}";
        return str;
    }
}