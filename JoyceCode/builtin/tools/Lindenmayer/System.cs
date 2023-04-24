namespace builtin.tools.Lindenmayer;

public class System
{
    class LSystem {

        public var seed: LState = null;
        public var rules: Array<LRule> = null;
        public var macros: Array<LRule> = null;

        public function new (
        seed0: LState,
        rules0: Array<LRule>,
        macros0: Array<LRule>
        ) {
            seed = seed0;
            rules = rules0;
            macros = macros0;
        }
    }

}