using System;

namespace builtin.tools.kanshu;

public class ConstantReplacement
{
    /**
     * Provide a replacement made from a list of edges and nodes created by
     * transformation from existing ones plus a list of newly specified edges and nodes.
     */
    public static Func<Graph, MatchResult, Graph?> Create()
    {
        return (graph, matchResult) =>
        {
            return null;
        };
    }
}