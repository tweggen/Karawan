using System;

namespace builtin.tools.kanshu;

public class ConstantReplacement
{
    public static Func<Graph, MatchResult, Graph?> Create()
    {
        return (graph, matchResult) =>
        {
            return null;
        };
    }
}