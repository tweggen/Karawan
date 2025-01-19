using System;
using System.Collections.Generic;

namespace builtin.tools.kanshu;


/**
 * Defines a rule predicate tbat consist of a set
 * of fixed strings that need to be matched.
 */
public class PropertiesPredicate
{
    public static Predicate<Properties> Create(SortedDictionary<string, string> props)
    {
        return (Properties label) =>
        {
            foreach (var kvp in props)
            {
                if (label.Value.TryGetValue(kvp.Key, out var value))
                {
                    if (kvp.Value != value)
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            return true;
        };
    }
}