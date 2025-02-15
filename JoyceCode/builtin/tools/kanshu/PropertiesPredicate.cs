using System;
using System.Collections.Generic;

namespace builtin.tools.kanshu;


/**
 * Defines a rule predicate tbat consist of a set
 * of fixed strings that need to be matched.
 */
public class PropertiesPredicate
{
    public static Func<Match, Labels, Match> Create(
        SortedDictionary<string, string>? constantProps = null,
        SortedDictionary<string, string>? boundProps = null)
    {
        return (Match match, Labels label) =>
        {
            /*
             * First match the plain constants
             */
            if (constantProps != null)
            {
                foreach (var kvp in constantProps)
                {
                    if (label.Value.TryGetValue(kvp.Key, out var value))
                    {
                        if (kvp.Value != value)
                        {
                            return null;
                        }
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            
            /*
             * Then match the bindings 
             */
            if (boundProps != null)
            {
                foreach (var kvp in boundProps)
                {
                    if (label.Value.TryGetValue(kvp.Key, out var value))
                    {
                        bool hasMatch = match.HasBinding(kvp.Key, out string oldValue);
                        #error foo we need to continue here
                        if (!match.TryAddBinding(kvp.Key, value))
                        {
                            return null;
                        }
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            return match;
        };
    }
}