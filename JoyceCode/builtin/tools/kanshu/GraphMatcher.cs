using System;
using System.Collections.Generic;
using System.Linq;

namespace builtin.tools.kanshu;

public class GraphMatcher
{
    private Graph _graph;
    private Pattern _pattern;
    private Dictionary<int, Graph.Node> _mapping;
    private HashSet<Graph.Node> _mappedGraphNodes;

    public bool FindMatch(
        Graph graph,
        Pattern pattern,
        out Match match
    ) 
    {
        this._graph = graph;
        this._pattern = pattern;
        this._mapping = new Dictionary<int, Graph.Node>();
        this._mappedGraphNodes = new HashSet<Graph.Node>();

        bool success = MatchRecursive(0);
        match = new() { Nodes = this._mapping };
        return success;
    }

    private bool MatchRecursive(int patternNodeIndex) 
    {
        // Base case: all pattern nodes matched
        if (patternNodeIndex >= _pattern.Nodes.Count) 
        {
            return true;
        }

        var patternNode = _pattern.Nodes[patternNodeIndex];

        // Find candidate nodes in graph
        foreach (var graphNode in _graph.Nodes) 
        {
            if (_mappedGraphNodes.Contains(graphNode)) continue;
            
            if (IsCompatible(patternNode, graphNode))
            {
                // Try this mapping
                _mapping[patternNodeIndex] = graphNode;
                _mappedGraphNodes.Add(graphNode);

                if (MatchRecursive(patternNodeIndex + 1)) 
                {
                    return true;
                }

                // Backtrack
                _mapping.Remove(patternNodeIndex);
                _mappedGraphNodes.Remove(graphNode);
            }
        }

        return false;
    }

    private bool IsCompatible(
        Pattern.PatternNode patternNode, 
        Graph.Node graphNode) 
    {
        // Check label match
        if (!patternNode.Predicate(graphNode.Label))
        {
            return false;
        }

        // Check if required connections can be satisfied
        foreach (var reqEdge in patternNode.RequiredConnections) 
        {
            var targetPatternNode = _pattern.Nodes[reqEdge.TargetNodeIndex];
            
            // If target is already mapped, check if connection exists
            if (_mapping.ContainsKey(reqEdge.TargetNodeIndex)) {
                var targetGraphNode = _mapping[reqEdge.TargetNodeIndex];
                if (!HasMatchingEdge(graphNode, targetGraphNode, reqEdge.Predicate)) 
                {
                    return false;
                }
            }
            // If target not mapped yet, check if there exists at least one possible match
            else {
                bool hasCandidate = false;
                foreach (var adj in graphNode.Adjacency) 
                {
                    if (!_mappedGraphNodes.Contains(adj.Value) &&
                        reqEdge.Predicate(adj.Key.Label) && 
                        // adj.Key.Label.Equals(reqEdge.Predicate) &&
                        // adj.Value.Label.Equals(targetPatternNode.Predicate))
                        targetPatternNode.Predicate(adj.Value.Label)
                        )
                    {
                        hasCandidate = true;
                        break;
                    }
                }

                if (!hasCandidate)
                {
                    return false;
                }
            }
        }

        return true;
    }

    
    /*
     * We know from and to match nodes for a given requested connection,
     * look if a real connection exists between these nodes that matches
     * the given edge predicate
     */
    private bool HasMatchingEdge(
        Graph.Node from,
        Graph.Node to,
        Func<Labels, bool> predicate
    )
    {
        return from.Adjacency.Any(adj => 
            adj.Value == to 
            && 
            predicate(adj.Key.Label)
        );
    }
}
