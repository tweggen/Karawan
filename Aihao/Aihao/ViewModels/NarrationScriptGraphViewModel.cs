using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aihao.ViewModels;

/// <summary>
/// Represents a node rectangle in the script graph view.
/// </summary>
public partial class GraphNodeViewModel : ObservableObject
{
    [ObservableProperty] private string _nodeId = "";
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private double _width = 140;
    [ObservableProperty] private double _height = 60;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isStartNode;

    /// <summary>
    /// Outgoing goto targets from this node.
    /// </summary>
    public List<string> GotoTargets { get; set; } = new();
}

/// <summary>
/// Represents a connection (edge) between two nodes in the graph.
/// </summary>
public partial class GraphEdgeViewModel : ObservableObject
{
    [ObservableProperty] private string _fromNodeId = "";
    [ObservableProperty] private string _toNodeId = "";
    [ObservableProperty] private string _label = "";

    // Computed endpoint coords for the view
    [ObservableProperty] private double _x1;
    [ObservableProperty] private double _y1;
    [ObservableProperty] private double _x2;
    [ObservableProperty] private double _y2;
}

/// <summary>
/// ViewModel for the script graph visualization.
/// Builds a simple node-graph from NarrationNodeViewModels.
/// </summary>
public partial class NarrationScriptGraphViewModel : ObservableObject
{
    public ObservableCollection<GraphNodeViewModel> GraphNodes { get; } = new();
    public ObservableCollection<GraphEdgeViewModel> GraphEdges { get; } = new();

    public void BuildGraph(IEnumerable<NarrationNodeViewModel> nodes, string startNodeId)
    {
        GraphNodes.Clear();
        GraphEdges.Clear();

        var nodeList = nodes.ToList();
        var nodeMap = new Dictionary<string, GraphNodeViewModel>();

        // Create graph nodes in a simple grid layout
        const double spacingX = 180;
        const double spacingY = 90;
        int col = 0;
        int row = 0;
        int maxCols = Math.Max(3, (int)Math.Ceiling(Math.Sqrt(nodeList.Count)));

        foreach (var node in nodeList)
        {
            var gn = new GraphNodeViewModel
            {
                NodeId = node.NodeId,
                X = 20 + col * spacingX,
                Y = 20 + row * spacingY,
                IsStartNode = node.NodeId == startNodeId
            };

            // Collect goto targets
            foreach (var stmt in node.Flow)
            {
                if (stmt.Kind == StatementKind.Goto && !string.IsNullOrEmpty(stmt.GotoTarget))
                {
                    gn.GotoTargets.Add(stmt.GotoTarget);
                }
                else if (stmt.Kind == StatementKind.Choices)
                {
                    foreach (var choice in stmt.Choices)
                    {
                        if (!string.IsNullOrEmpty(choice.GotoTarget))
                            gn.GotoTargets.Add(choice.GotoTarget);
                    }
                }
            }

            nodeMap[node.NodeId] = gn;
            GraphNodes.Add(gn);

            col++;
            if (col >= maxCols) { col = 0; row++; }
        }

        // Create edges
        foreach (var gn in GraphNodes)
        {
            foreach (var target in gn.GotoTargets)
            {
                if (nodeMap.TryGetValue(target, out var targetNode))
                {
                    GraphEdges.Add(new GraphEdgeViewModel
                    {
                        FromNodeId = gn.NodeId,
                        ToNodeId = target,
                        X1 = gn.X + gn.Width / 2,
                        Y1 = gn.Y + gn.Height,
                        X2 = targetNode.X + targetNode.Width / 2,
                        Y2 = targetNode.Y
                    });
                }
            }
        }
    }

    public void SelectNode(string nodeId)
    {
        foreach (var gn in GraphNodes)
        {
            gn.IsSelected = gn.NodeId == nodeId;
        }
    }
}
