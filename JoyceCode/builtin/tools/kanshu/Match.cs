using System.Collections.Generic;

namespace builtin.tools.kanshu;


/**
 * Describes one rule match, including the bindings.
 */
public class Match
{
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
            
        }
        else
        {
            Bindings = new();
            Bindings.Add(key, value);
            return true;
        }
    }
}