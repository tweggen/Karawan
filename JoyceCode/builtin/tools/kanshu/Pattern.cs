using System;
using System.Collections.Generic;
using static engine.Logger;

namespace builtin.tools.kanshu;

public class Pattern
{
    public List<PatternNode> Nodes { get; set; } = new();

    public class PatternNode
    {
        public Func<Scope, Labels, Scope> Predicate;
        public List<PatternEdge> RequiredConnections { get; set; } = new();
        
        public int Id { get; set; }
    }

    public class PatternEdge
    {
        public Func<Scope, Labels, Scope> Predicate;
        public int TargetNodeIndex { get; set; } // Index in pattern's Nodes list
    }
    
    static public Pattern Create(
        List<NodePredicateDescriptor> nodes, 
        List<EdgePredicateDescriptor> edges)
    {
        var pattern = new Pattern();
        
        /*
         * First create the nodes, then add the edges from the desciptors.
         */
        int idx = 0;
        foreach (var nodeDesc in nodes)
        {
            pattern.Nodes.Add(new()
            {
                Id = nodeDesc.Id>=0?nodeDesc.Id:idx,
                Predicate = nodeDesc.Predicate
            });
            idx++;
        }

        foreach (var edgeDesc in edges)
        {
            if (edgeDesc.NodeFrom < 0 || edgeDesc.NodeFrom >= pattern.Nodes.Count)
            {
                ErrorThrow<ArgumentException>($"NodeFrom for edge node from is out of range.");
            }
            if (edgeDesc.NodeTo < 0 || edgeDesc.NodeTo >= pattern.Nodes.Count)
            {
                ErrorThrow<ArgumentException>($"NodeFrom for edge node to is out of range.");
            }

            PatternEdge edge = new ()
            {
                Predicate = edgeDesc.Predicate,
                TargetNodeIndex = edgeDesc.NodeTo
                
            };
            PatternNode nodeFrom = pattern.Nodes[edgeDesc.NodeFrom];
            nodeFrom.RequiredConnections.Add(edge);
        }

        return pattern;
    }

}
