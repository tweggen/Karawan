using System;
using System.Collections.Generic;
using static engine.Logger;

namespace builtin.tools.kanshu;

public class Graph<TNodeLabel, TEdgeLabel> {
    public class Node {
        public TNodeLabel Label { get; set; }
        public Dictionary<Edge, Node> Adjacency { get; set; } = new();
        public int Id { get; set; }  // Unique identifier helps with matching

        public override string ToString()
        {
            string str = "{";
            str += $"\"id\": {Id},";
            str += $"\"label\": {Label},";
            str += "\"edges\": [";
            bool isFirst = true;
            foreach (var kvp in Adjacency)
            {
                if (!isFirst) str += ",";
                else isFirst = false;
                str += "{";
                str += $"\"destId\": {kvp.Value.Id},";
                str += $"\"label\": {kvp.Key}";
                str += "}";
            }
            str += "]";
            str += "}";
            return str;
        }
    }

    public class Edge {
        public TEdgeLabel Label { get; set; }

        public override string ToString()
        {
            string str = "{";
            str += $"\"label\": {Label}";
            str += "}";
            return str;
        }
    }


    public List<Node> Nodes = new();

    
    /**
     * Given two lists of descriptions, create a graph.
     */
    static public Graph<TNodeLabel, TEdgeLabel> Create(List<NodeDescriptor<TNodeLabel>> nodes, List<EdgeDescriptor<TEdgeLabel>> edges)
    {
        var graph = new Graph<TNodeLabel, TEdgeLabel>();
        
        /*
         * First create the nodes, then add the edges from the desciptors.
         */
        int idx = 0;
        foreach (var nodeDesc in nodes)
        {
            graph.Nodes.Add(new()
            {
                Id = nodeDesc.Id>=0?nodeDesc.Id:idx,
                Label = nodeDesc.Label
            });
            idx++;
        }

        foreach (var edgeDesc in edges)
        {
            Edge edge = new ()
            {
                Label = edgeDesc.Label
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

