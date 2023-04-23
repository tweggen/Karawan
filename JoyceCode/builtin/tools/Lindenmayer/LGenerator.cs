#if false

namespace builtin.tools;

public class LGenerator
{
    private var _lSystem: LSystem;

    private var _traceEmit = false;

    private function applyRules( lInstance: LInstance, rules: Array<LRule> ): LInstance {

        if( null==rules ) {
            return lInstance.clone();
        }

        var isChanged = false;

        var os = lInstance.state;
        var op = os.parts;
        if( null==op ) {
            return new LInstance( _lSystem, new LState( op ) );
        }
        var np = new Array<LPart>();
        for( part in op ) {
            /*
             * First collect all the rules that match the part.
             */
            var matchingRules = new Array<LRule>();
            for( rule in rules ) {
                if( rule.name != part.name ) {
                    continue;
                }
                if( null != rule.condition ) {
                    if( false == rule.condition( part.parameters ) ) {
                        continue;
                    }
                }
                if( _traceEmit ) trace('LGenerator.iterate(): Matched rule "${rule.name}".');
                matchingRules.push( rule );
            }

            if( 0 != matchingRules.length ) {
                /*
                * If we have matching rules, select the first match.
                * TXWTODO: Collect the probabilities and select accordingly.
                */
                var winningRule = matchingRules[0];
                var newParts = winningRule.transformParts( part.parameters );
                for( singleNewPart in newParts ) {
                    if( _traceEmit ) trace( 'LGenerator.iterate(): Pushing new "${singleNewPart}".');
                    np.push( singleNewPart );
                }
                isChanged = true;
            } else {
                /*
                 * No rule match, leave part untouched.
                 */
                if( _traceEmit ) trace( 'LGenerator.iterate(): Pushing old "${part}".');
                np.push( part.clone() );
            }
        }
        if( !isChanged ) {
            return null;
        }
        var ns = new LState( np );
        return new LInstance( _lSystem, ns );

    }

    /**
     * Return a new instance, iterated using LSystem rules.
     */
    public function iterate( lInstance: LInstance ): LInstance {
        return applyRules( lInstance, _lSystem.rules );
    }


    public function finalize( lInstance: LInstance ): LInstance {
        return applyRules( lInstance, _lSystem.macros );
    }


    public function instantiate(): LInstance {
        return new LInstance( _lSystem, _lSystem.seed.clone() );
    }


    public function new ( lSystem: LSystem ) {
        _lSystem = lSystem;
    }
}
}
#endif