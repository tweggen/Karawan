using System.Collections.Generic;

namespace builtin.tools.Lindenmayer;

public class System
{
    public State Seed = null;
    public List<Rule> Rules = null;
    public List<Rule> Macros = null;

    public System(
        State seed,
        List<Rule> rules,
        List<Rule> macros
    ) {
        Seed = seed;
        Rules = rules;
        Macros = macros;
    }
}