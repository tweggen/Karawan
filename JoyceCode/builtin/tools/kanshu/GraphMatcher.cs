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
    private MatchResult _matchResult;
    private Rule _rule;


    public GraphMatcher(
        Graph graph,
        Rule rule)
    {
        _graph = graph;
        _rule = rule;
        _pattern = rule.Pattern;
        _mapping = new Dictionary<int, Graph.Node>();
        _mappedGraphNodes = new HashSet<Graph.Node>();
        _matchResult = new()
        {
            Rule = rule,
            Nodes = _mapping
        };

    }
    
    public MatchResult GetResult()
    {
        return _matchResult;
    }

    public bool FindMatch()
    {
        Match matchRoot = new();
        bool success = MatchRecursive(matchRoot, 0, out var matchLeaf);
        return success;
    }

    
    private bool MatchRecursive(Match currentMatch, int patternNodeIndex, out Match matchLeaf) 
    {
        // Base case: all pattern nodes matched
        if (patternNodeIndex >= _pattern.Nodes.Count)
        {
            matchLeaf = currentMatch;
            return true;
        }

        var patternNode = _pattern.Nodes[patternNodeIndex];

        // Find candidate nodes in graph
        foreach (var graphNode in _graph.Nodes) 
        {
            if (_mappedGraphNodes.Contains(graphNode)) continue;

            Match matchTry = new Match() { Parent = currentMatch };
            if (IsCompatible(matchTry, patternNode, graphNode))
            {
                // Try this mapping
                _mapping[patternNodeIndex] = graphNode;
                _mappedGraphNodes.Add(graphNode);

                
                if (MatchRecursive(matchTry, patternNodeIndex + 1, out var newMatch)) 
                {
                    matchLeaf = newMatch;
                    return true;
                }

                // Backtrack
                _mapping.Remove(patternNodeIndex);
                _mappedGraphNodes.Remove(graphNode);
            }
        }

        matchLeaf = currentMatch;
        return false;
    }
    

    private bool IsCompatible(
        Match matchTry,
        Pattern.PatternNode patternNode, 
        Graph.Node graphNode) 
    {
        // Check label match
        if (!patternNode.Predicate(matchTry, graphNode.Label))
        {
            return false;
        }

        // Check if required connections can be satisfied
        foreach (var reqEdge in patternNode.RequiredConnections) 
        {
            var targetPatternNode = _pattern.Nodes[reqEdge.TargetNodeIndex];
            
            if (_mapping.ContainsKey(reqEdge.TargetNodeIndex)) {
                /*
                 * If target is already mapped, check if connection exists
                 */

                var targetGraphNode = _mapping[reqEdge.TargetNodeIndex];
                if (!HasMatchingEdge(graphNode, targetGraphNode, matchTry, reqEdge.Predicate)) 
                {
                    return false;
                }
            }
            else
            {
                /*
                 * If target not mapped yet, check if there exists at least one possible match
                 */
                bool hasCandidate = false;
                foreach (var adj in graphNode.Adjacency) 
                {
                    if (!_mappedGraphNodes.Contains(adj.Value) &&
                        reqEdge.Predicate(matchTry, adj.Key.Label) && 
                        // adj.Key.Label.Equals(reqEdge.Predicate) &&
                        // adj.Value.Label.Equals(targetPatternNode.Predicate))
                        targetPatternNode.Predicate(matchTry, adj.Value.Label)
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
        Match matchTry,
        Func<Match, Labels, bool> predicate
    )
    {
        return from.Adjacency.Any(adj => 
            adj.Value == to 
            && 
            predicate(matchTry, adj.Key.Label)
        );
    }
}
