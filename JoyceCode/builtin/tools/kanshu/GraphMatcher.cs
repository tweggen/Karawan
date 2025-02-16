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
    }
    
    public MatchResult? GetResult()
    {
        return _matchResult;
    }

    public MatchResult? FindMatch()
    {
        Scope scopeRoot = new();
        Scope? scope = MatchRecursive(scopeRoot, 0);

        if (null != scope)
        {
            /*
             * if scope is null, there is no match.
             * If scope is non-null, we built the result node mapping in
             * mappedGraphNodes.
             */
            _matchResult = new MatchResult()
            {
                Rule = _rule,
                Nodes = _mapping
            };
            return _matchResult;
        }
        else
        {
            return null;
        }
    }

    
    private Scope MatchRecursive(Scope currentScope, int patternNodeIndex) 
    {
        // Base case: all pattern nodes matched
        if (patternNodeIndex >= _pattern.Nodes.Count)
        {
            return currentScope;
        }

        var patternNode = _pattern.Nodes[patternNodeIndex];

        // Find candidate nodes in graph
        foreach (var graphNode in _graph.Nodes) 
        {
            if (_mappedGraphNodes.Contains(graphNode)) continue;

            var matchTry = IsCompatible(currentScope, patternNode, graphNode); 
            if (null != matchTry)
            {
                // Try this mapping
                _mapping[patternNodeIndex] = graphNode;
                _mappedGraphNodes.Add(graphNode);


                var mightMatch = MatchRecursive(matchTry, patternNodeIndex + 1);
                if (null != mightMatch) 
                {
                    return mightMatch;
                }

                // Backtrack
                _mapping.Remove(patternNodeIndex);
                _mappedGraphNodes.Remove(graphNode);
            }
        }

        return null;
    }
    

    /**
     * Test if that real given node matches the one given in the pattern.
     */
    private Scope IsCompatible(
        Scope scopeCurrent,
        Pattern.PatternNode patternNode, 
        Graph.Node graphNode) 
    {
        /*
         * Does the node match the pattern?
         */
        scopeCurrent = patternNode.Predicate(scopeCurrent, graphNode.Label); 
        if (null == scopeCurrent)
        {
            return null;
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
                var matchGraphEdge = HasMatchingEdge(graphNode, targetGraphNode, scopeCurrent, reqEdge.Predicate); 
                if (null == matchGraphEdge) 
                {
                    return null;
                }

                scopeCurrent = matchGraphEdge;
            }
            else
            {
                /*
                 * If target not mapped yet, check if there exists at least one possible match
                 */
                bool hasCandidate = false;
                Scope? scopeCandidate = null;
                foreach (var adj in graphNode.Adjacency) 
                {
                    scopeCandidate = scopeCurrent;
                    if (!_mappedGraphNodes.Contains(adj.Value))
                    {
                        scopeCandidate = reqEdge.Predicate(scopeCandidate, adj.Key.Label);
                        if (null != scopeCandidate)
                        {
                            scopeCandidate = targetPatternNode.Predicate(scopeCandidate, adj.Value.Label);
                            // adj.Key.Label.Equals(reqEdge.Predicate) &&
                            // adj.Value.Label.Equals(targetPatternNode.Predicate))
                            //targetPatternNode.Predicate(matchTry, adj.Value.Label)
                            if (null != scopeCandidate)
                            {
                                hasCandidate = true;
                                break;
                            }
                        }
                    }
                }

                if (!hasCandidate)
                {
                    return null;
                }

                scopeCurrent = scopeCandidate;
            }
        }

        return scopeCurrent;
    }

    
    /*
     * We know from and to match nodes for a given requested connection,
     * look if a real connection exists between these nodes that matches
     * the given edge predicate
     */
    private Scope HasMatchingEdge(
        Graph.Node from,
        Graph.Node to,
        Scope scopeCurrent,
        Func<Scope, Labels, Scope> predicate
    )
    {
        #if false
        return from.Adjacency.Any(adj => 
            adj.Value == to 
            && 
            predicate(matchTry, adj.Key.Label)
        );
        #else
        foreach (var adj in from.Adjacency)
        {
            if (adj.Value == to)
            {
                /*
                 * Obviously, we return first match. What happens with the other matches?
                 */
                var matchAdj = predicate(scopeCurrent, adj.Key.Label);
                if (null != matchAdj)
                {
                    return matchAdj;
                }
            }
        }
        return null;
        #endif
    }
}
