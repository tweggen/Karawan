using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace builtin.tools.kanshu;

public class ConstantReplacement
{
    /**
     * Provide a replacement function made from a list of edges and nodes created by
     * transformation from existing ones plus a list of newly specified edges and nodes.
     * The function is expected to construct a new graph created from the given one
     * and the replacements given.
     *
     * @param replaceByNodes
     *     Node i from the pattern shall be replaced by the given node.
     */
    public static Func<Graph, MatchResult, Graph?> Create(
        SortedDictionary<int, Graph.Node> replaceByNodes,
        List<NodeDescriptor> newNodes,
        List<EdgeDescriptor> newEdges)
    {
        return (graph, matchResult) =>
        {
            /*
             * Algorithm:
             * - first create all nodes.
             *   - Create the replacements for the previous nodes, creating
             *     a mapping table.
             *   - Create new nodes as specified by the replacements.
             * - then create all new edges origining from the new nodes.
             *
             * - Create all edges as requested from the replacement template.
             *
             * - Insert the edges into the respective nodes.
             *
             * Note:
             * - matchResult contains a map associating the index within the pattern
             *   with the original node found to be that one.
             */

            /*
             * This becomes the list of replaced nodes. Note they can be
             * referred to by index.
             */
            List<Graph.Node> listNodes = new();
            int nReplacedNodes = replaceByNodes.Count;
            Debug.Assert(nReplacedNodes == matchResult.Nodes.Count);

            Dictionary<Graph.Node, Graph.Node?> dictReplaceNodes = new();
            
            for (int i=0; i<matchResult.Rule.Pattern.Nodes.Count; i++)
            {
                var nodeReplacement = new Graph.Node()
                {
                    Label = Labels.FromMerge(
                        matchResult.Scope,
                        matchResult.Nodes[i].Label,
                        replaceByNodes[i].Label,
                        Labels.AlterationFlags.BindValues
                        |Labels.AlterationFlags.PriorizeNew
                        |Labels.AlterationFlags.ConsiderOld)
                };
                listNodes.Add(nodeReplacement);

                dictReplaceNodes.Add(matchResult.Nodes[i], nodeReplacement);
            }
            
            /*
             * Now create the new nodes that do not replace original nodes.
             */
            int nNewNodes = newNodes.Count;
            for (int i = 0; i < nNewNodes; i++)
            {
                var node = new Graph.Node()
                {
                    Label = newNodes[i].Labels,
                };
                listNodes.Add(node);
            }
            
            /*
             * Now add the edges. 
             */
            int nEdges = newEdges.Count;
            for (int i = 0; i < nEdges; ++i)
            {
                EdgeDescriptor desc = newEdges[i];
                var edge = new Graph.Edge() { Labels = desc.Labels };
                
                /*
                 * Add the edge into the node.
                 */ 
                Debug.Assert(desc.NodeFrom < listNodes.Count);
                Debug.Assert(desc.NodeTo < listNodes.Count);
                
                listNodes[desc.NodeFrom].Adjacency.Add(edge, listNodes[desc.NodeTo]);
            }
            return null;
        };
    }
}