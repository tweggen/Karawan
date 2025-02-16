using System;
using System.Collections.Generic;

namespace builtin.tools.kanshu;

public class ConstantReplacement
{
    /**
     * Provide a replacement made from a list of edges and nodes created by
     * transformation from existing ones plus a list of newly specified edges and nodes.
     *
     * @param replaceByNodes
     *     Node i from the pattern shall be replaced by the given node.
     */
    public static Func<Graph, MatchResult, Graph?> Create(
        SortedDictionary<int, NodeDescriptor> replaceByNodes,
        SortedDictionary<int, EdgeDescriptor> replaceByEdges,
        List<NodeDescriptor> newNodes,
        List<EdgeDescriptor> newEdges)
    {
        return (graph, matchResult) =>
        {
            return null;
        };
    }
}