using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace builtin.tools.kanshu;


/**
 * Defines a rule predicate tbat consist of a set
 * of fixed strings that need to be matched.
 */
public class LabelsPredicate
{
    
    /**
     * Create a new binding predicate.
     * @param constantProps
     *     The label (key) needs to be bound to a constant (value).
     * @param boundProps
     *     The label (key) needs to be bound to a binding key in the frame (value).
     *     The requested value from the caller needs to match the value stored
     *     in the binding.
     */
    public static Func<Scope, Labels, Scope> Create(
        SortedDictionary<string, string>? constantProps = null,
        SortedDictionary<string, string>? boundProps = null)
    {
        return (Scope match, Labels label) =>
        {
            /*
             * First match the plain constants
             */
            if (constantProps != null)
            {
                foreach (var kvp in constantProps)
                {
                    string labelName = kvp.Key;
                    string labelValue = kvp.Value;
                    if (label.Value.TryGetValue(labelName, out var value))
                    {
                        if (labelValue != value)
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
             * Then match the bindings.
             * For every requested binding look, if we there is a similar or contradicting
             * binding in our context. If there is not, create a new frame containing all
             * required bindings.
             */
            if (boundProps != null)
            {
                Scope? newFrame = null;
                
                foreach (var kvp in boundProps)
                {
                    /*
                     * kvp.Key is the label.
                     * kvp.Value contains the name of the binding.
                     */
                    string labelName = kvp.Key;
                    if (label.Value.TryGetValue(labelName, out var subjectLabelValue))
                    {
                        /*
                         * subjectLabelValue is the value as stored in the graph that we should scan
                         * for patterns.
                         */
                        
                        /*
                         * This is the name of the binding we should use for that particular label name.
                         */
                        string bindingName = kvp.Value;
                        
                        /*
                         * Look for existing bindings for that binding name.
                         * - If there is none, create one an succeed.
                         * - If there is a contradicting one, fail.
                         * - If there is a matching one, also succeed.
                         */
                        if (match.HasBinding(bindingName, out string existingValue))
                        {
                            if (existingValue == subjectLabelValue)
                            {
                                /*
                                 * We already have bound that one, continue.
                                 */
                            }
                            else
                            {
                                /*
                                 * This is a different, contradicting binding, fail.
                                 */
                                return null;
                            }
                        }
                        else
                        {
                            /*
                             * There is no binding for the requested value. Create one.
                             */

                            /*
                             * If there is no current frame, create it.
                             */
                            if (null == newFrame)
                            {
                                newFrame = new() { Parent = match, Bindings = new() };
                            }

                            newFrame.Bindings.Add(labelName, subjectLabelValue);
                        }
                    }
                }

                if (newFrame != null)
                {
                    match = newFrame;
                }
            }

            return match;
        };
    }
}