using System;
using System.Collections.Generic;
using static engine.Logger;

namespace builtin.tools.kanshu;


/**
 * Represent a gfraph.
 *
 * How to replace a set of nodes together with its edges
 * in the graph:
 *
 * 1. We do want to replace a set of nodes. We will also replace the edges
 *    origining from those nodes. We will not replace edges origining from
 *    nodes that are not part of the substitute set.
 * 2. We decide that it is best to provide methods for altering the graph
 *    in this data structure to abstract as much functionality as possible.
 */
public class Graph {
    
    public class Node
    {
        public Labels Label { get; set; }
        public Dictionary<Edge, Node> Adjacency = new();
        // public int Id { get; set; } // Unique identifier helps with matching
    }
    

    public class Edge {
        public Labels Label { get; set; }
    }


    public List<Node> Nodes { get; set; } = new();

    
    /**
     * Replace the node in the current graph with a new node based on the template
     * given. Have the flags specify the way the labels are combined.
     *
     * While replacing, the values of the labels can be bound if desired. 
     */
    static public Node FromTemplate(Scope scope, Graph.Node old, Graph.Node template, Labels.AlterationFlags flags)
    {
        /*
         * Create a new node, containing the labels as requested by the caller.
         */
        Node node = new();
        node.Label = Labels.FromMerge(scope, old.Label, template.Label, flags);
        return node;
    }
    
    
    /**
     * Given two lists of descriptions, create a graph.
     */
    static public Graph Create(
        List<NodeDescriptor> nodes, 
        List<EdgeDescriptor> edges)
    {
        var graph = new Graph();
        
        /*
         * First create the nodes, then add the edges from the desciptors.
         */
        int idx = 0;
        foreach (var nodeDesc in nodes)
        {
            graph.Nodes.Add(new()
            {
                // Id = nodeDesc.Id>=0?nodeDesc.Id:idx,
                Label = nodeDesc.Labels
            });
            idx++;
        }

        foreach (var edgeDesc in edges)
        {
            Edge edge = new ()
            {
                Label = edgeDesc.Labels
            };
            if (edgeDesc.NodeFrom < 0 || edgeDesc.NodeFrom >= graph.Nodes.Count)
            {
                ErrorThrow<ArgumentException>($"NodeFrom for edge node from is out of range.");
            }
            if (edgeDesc.NodeTo < 0 || edgeDesc.NodeTo >= graph.Nodes.Count)
            {
                ErrorThrow<ArgumentException>($"NodeFrom for edge node to is out of range.");
            }
            Node nodeFrom = graph.Nodes[edgeDesc.NodeFrom];
            Node nodeTo = graph.Nodes[edgeDesc.NodeTo];
            nodeFrom.Adjacency.Add(edge, nodeTo);
        }

        return graph;
    }
}

