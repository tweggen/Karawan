using System;
using System.Collections.Generic;
using static engine.Logger;

namespace builtin.tools.kanshu;

public class Graph {
    public class Node
    {
        public Labels Label { get; set; }
        public Dictionary<Edge, Node> Adjacency = new();
        public int Id { get; set; } // Unique identifier helps with matching
    }
    

    public class Edge {
        public Labels Label { get; set; }
    }


    public List<Node> Nodes { get; set; } = new();
        
    
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
                Id = nodeDesc.Id>=0?nodeDesc.Id:idx,
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

