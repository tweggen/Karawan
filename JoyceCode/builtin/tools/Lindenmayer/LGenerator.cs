using System.Collections.Generic;
using static engine.Logger;

namespace builtin.tools.Lindenmayer;

public class LGenerator
{
    private System _system;

    private bool _traceEmit = false;

    private Instance _applyRules( Instance lInstance, IList<Rule> rules )
    {

        if( null==rules ) {
            return lInstance.Clone();
        }

        var isChanged = false;

        var os = lInstance.State;
        var op = os.Parts;
        if( null==op ) {
            return new Instance( _system, new State( op ) );
        }
        var np = new List<Part>();
        foreach (Part part in op ) {
            /*
             * First collect all the rules that match the part.
             */
            var matchingRules = new List<Rule>();
            foreach (Rule rule in rules) 
            {
                if( rule.Name != part.Name ) 
                {
                    continue;
                }
                if (null != rule.Condition) 
                {
                    if (false == rule.Condition( part.Parameters )) 
                    {
                        continue;
                    }
                }
                if( _traceEmit ) Trace($"Matched rule {rule.Name}.");
                matchingRules.Add( rule );
            }

            if( 0 != matchingRules.Count ) {
                /*
                * If we have matching rules, select the first match.
                * TXWTODO: Collect the probabilities and select accordingly.
                */
                var winningRule = matchingRules[0];
                var newParts = winningRule.TransformParts( part.Parameters );
                foreach (Part singleNewPart in newParts ) {
                    if( _traceEmit ) Trace( $"Pushing new \"{singleNewPart}\".");
                    np.Add( singleNewPart );
                }
                isChanged = true;
            } else {
                /*
                 * No rule match, leave part untouched.
                 */
                if( _traceEmit ) Trace( $"Pushing old \"{part}\".");
                np.Add( part.Clone() );
            }
        }
        if( !isChanged ) {
            return null;
        }
        var ns = new State( np );
        return new Instance( _system, ns );

    }

    /**
     * Return a new instance, iterated using LSystem rules.
     */
    public Instance Iterate( Instance instance  )
    {
        return _applyRules( instance, _system.Rules );
    }


    public Instance Finalize( Instance instance )
    {
        return _applyRules(instance, _system.Macros );
    }


    public Instance Instantiate()
    {
        return new Instance( _system, _system.Seed.Clone() );
    }


    public Instance Generate(int maxGenerations)
    {
        var lInstance = Instantiate();
        var prevInstance = lInstance;
        int iMax = maxGenerations;
        for (int i = 0; i < iMax; ++i)
        {
            var nextInstance = Iterate(prevInstance);
            if (null == nextInstance)
            {
                break;
            }

            prevInstance = nextInstance;
        }

        return Finalize(prevInstance);
    }


    public LGenerator( System system )
    {
        _system = system;
    }
}
