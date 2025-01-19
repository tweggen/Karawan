using System;
using System.Collections.Generic;
using System.Linq;

namespace builtin.tools.kanshu;

public class GraphMatcher<
    TNodeLabel, TEdgeLabel> {
    private Graph<TNodeLabel, TEdgeLabel> graph;
    private Pattern<TNodeLabel, TEdgeLabel> pattern;
    private Dictionary<int, Graph<TNodeLabel, TEdgeLabel>.Node> mapping;
    private HashSet<Graph<TNodeLabel, TEdgeLabel>.Node> mappedGraphNodes;

    public bool FindMatch(
        Graph<TNodeLabel, TEdgeLabel> graph,
        Pattern<TNodeLabel, TEdgeLabel> pattern,
        out Dictionary<int, Graph<TNodeLabel, TEdgeLabel>.Node> mapping
    ) {
        this.graph = graph;
        this.pattern = pattern;
        this.mapping = new Dictionary<int, Graph<TNodeLabel, TEdgeLabel>.Node>();
        this.mappedGraphNodes = new HashSet<Graph<TNodeLabel, TEdgeLabel>.Node>();

        bool success = MatchRecursive(0);
        mapping = this.mapping;
        return success;
    }

    private bool MatchRecursive(int patternNodeIndex) {
        // Base case: all pattern nodes matched
        if (patternNodeIndex >= pattern.Nodes.Count) {
            return true;
        }

        var patternNode = pattern.Nodes[patternNodeIndex];

        // Find candidate nodes in graph
        foreach (var graphNode in graph.Nodes) {
            if (mappedGraphNodes.Contains(graphNode)) continue;
            
            if (IsCompatible(patternNode, graphNode)) {
                // Try this mapping
                mapping[patternNodeIndex] = graphNode;
                mappedGraphNodes.Add(graphNode);

                if (MatchRecursive(patternNodeIndex + 1)) {
                    return true;
                }

                // Backtrack
                mapping.Remove(patternNodeIndex);
                mappedGraphNodes.Remove(graphNode);
            }
        }

        return false;
    }

    private bool IsCompatible(Pattern<TNodeLabel, TEdgeLabel>.PatternNode patternNode, 
                            Graph<TNodeLabel, TEdgeLabel>.Node graphNode) {
        // Check label match
        if (!patternNode.Predicate(graphNode.Label)) return false;

        // Check if required connections can be satisfied
        foreach (var reqEdge in patternNode.RequiredConnections) {
            var targetPatternNode = pattern.Nodes[reqEdge.TargetNodeIndex];
            
            // If target is already mapped, check if connection exists
            if (mapping.ContainsKey(reqEdge.TargetNodeIndex)) {
                var targetGraphNode = mapping[reqEdge.TargetNodeIndex];
                if (!HasMatchingEdge(graphNode, targetGraphNode, reqEdge.Predicate)) {
                    return false;
                }
            }
            // If target not mapped yet, check if there exists at least one possible match
            else {
                bool hasCandidate = false;
                foreach (var adj in graphNode.Adjacency) {
                    if (!mappedGraphNodes.Contains(adj.Value) && 
                        adj.Key.Label.Equals(reqEdge.Predicate) &&
                        adj.Value.Label.Equals(targetPatternNode.Predicate)) {
                        hasCandidate = true;
                        break;
                    }
                }
                if (!hasCandidate) return false;
            }
        }

        return true;
    }

    private bool HasMatchingEdge(
        Graph<TNodeLabel, TEdgeLabel>.Node from,
        Graph<TNodeLabel, TEdgeLabel>.Node to,
        Predicate<TEdgeLabel> predicate
    ) {
        return from.Adjacency.Any(adj => 
            adj.Value == to && predicate(adj.Key.Label));
    }
}
