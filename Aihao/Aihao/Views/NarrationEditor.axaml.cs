using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Aihao.ViewModels;

namespace Aihao.Views;

public partial class NarrationEditor : UserControl
{
    private NarrationScriptGraphViewModel _graphVm = new();

    public NarrationEditor()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is NarrationEditorViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(vm.SelectedNode) ||
                    args.PropertyName == nameof(vm.SelectedScriptName))
                {
                    RebuildGraph(vm);
                }
            };
            RebuildGraph(vm);
        }
    }

    private void RebuildGraph(NarrationEditorViewModel vm)
    {
        _graphVm.BuildGraph(vm.Nodes, vm.StartNodeId);
        if (vm.SelectedNode != null)
            _graphVm.SelectNode(vm.SelectedNode.NodeId);
        DrawGraph();
    }

    private void DrawGraph()
    {
        var canvas = this.FindControl<Canvas>("GraphCanvas");
        if (canvas == null) return;

        canvas.Children.Clear();

        // Draw edges first
        foreach (var edge in _graphVm.GraphEdges)
        {
            var line = new Line
            {
                StartPoint = new Point(edge.X1, edge.Y1),
                EndPoint = new Point(edge.X2, edge.Y2),
                Stroke = Brushes.Gray,
                StrokeThickness = 1.5
            };
            canvas.Children.Add(line);
        }

        // Draw nodes
        foreach (var node in _graphVm.GraphNodes)
        {
            var bg = node.IsSelected ? Brushes.CornflowerBlue
                   : node.IsStartNode ? Brushes.DarkOliveGreen
                   : Brushes.DimGray;

            var rect = new Border
            {
                Width = node.Width,
                Height = node.Height,
                Background = bg,
                CornerRadius = new CornerRadius(6),
                BorderBrush = node.IsSelected ? Brushes.White : Brushes.Transparent,
                BorderThickness = new Thickness(node.IsSelected ? 2 : 0),
                Child = new TextBlock
                {
                    Text = node.NodeId,
                    Foreground = Brushes.White,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    FontSize = 11
                }
            };

            Canvas.SetLeft(rect, node.X);
            Canvas.SetTop(rect, node.Y);

            // Click to select
            var nodeId = node.NodeId;
            rect.PointerPressed += (s, e) =>
            {
                if (DataContext is NarrationEditorViewModel editorVm)
                {
                    var target = editorVm.Nodes.FirstOrDefault(n => n.NodeId == nodeId);
                    if (target != null)
                    {
                        editorVm.SelectedNode = target;
                    }
                }
            };

            canvas.Children.Add(rect);
        }
    }
}
