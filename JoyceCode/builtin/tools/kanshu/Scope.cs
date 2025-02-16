using System.Collections.Generic;

namespace builtin.tools.kanshu;


/**
 * Describes one rule match, including the bindings.
 */
public class Scope
{
    public Scope? Parent { get; set; } = null;
    public SortedDictionary<string, string>? Bindings { get; set; } = null;


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
}