using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using Aihao.ViewModels;

namespace Aihao.Views;

public partial class NarrationEditor : UserControl
{
    private NarrationScriptGraphViewModel _graphVm = new();
    private NarrationNodeViewModel? _currentNode;
    private DispatcherTimer? _debounceTimer;
    private bool _isUpdatingEditor;
    private NarrationTokenPopupViewModel _popupVm = new();

    public NarrationEditor()
    {
        InitializeComponent();
        LoadSyntaxHighlighting();
        TokenPopupControl.DataContext = _popupVm;
        DataContextChanged += OnDataContextChanged;
    }

    private void LoadSyntaxHighlighting()
    {
        try
        {
            var assembly = typeof(NarrationEditor).Assembly;
            using var stream = assembly.GetManifestResourceStream("Aihao.Resources.NarrationMarkup.xshd");
            if (stream != null)
            {
                using var reader = new XmlTextReader(stream);
                var highlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                MarkupEditor.SyntaxHighlighting = highlighting;
            }
        }
        catch
        {
            // Syntax highlighting is optional
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is NarrationEditorViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(vm.SelectedNode))
                {
                    OnSelectedNodeChanged(vm.SelectedNode);
                    RebuildGraph(vm);
                }
                else if (args.PropertyName == nameof(vm.SelectedScriptName))
                {
                    RebuildGraph(vm);
                }
            };
            OnSelectedNodeChanged(vm.SelectedNode);
            RebuildGraph(vm);
        }
    }

    private void OnSelectedNodeChanged(NarrationNodeViewModel? node)
    {
        // Unsubscribe from old node
        if (_currentNode != null)
        {
            _currentNode.PropertyChanged -= OnNodePropertyChanged;
        }

        _currentNode = node;

        if (node != null)
        {
            node.PropertyChanged += OnNodePropertyChanged;
            SetEditorText(node.MarkupText);
        }
        else
        {
            SetEditorText("");
        }
    }

    private void OnNodePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NarrationNodeViewModel.MarkupText) && !_isUpdatingEditor)
        {
            if (_currentNode != null)
                SetEditorText(_currentNode.MarkupText);
        }
    }

    private void SetEditorText(string text)
    {
        _isUpdatingEditor = true;
        try
        {
            MarkupEditor.Text = text;
        }
        finally
        {
            _isUpdatingEditor = false;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        MarkupEditor.TextChanged += OnEditorTextChanged;
        MarkupEditor.TextArea.PointerPressed += OnEditorPointerPressed;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        MarkupEditor.TextChanged -= OnEditorTextChanged;
        MarkupEditor.TextArea.PointerPressed -= OnEditorPointerPressed;
        _debounceTimer?.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnEditorPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // Delay slightly to let caret update
        Dispatcher.UIThread.Post(() => TryShowTokenPopup(), DispatcherPriority.Input);
    }

    private void TryShowTokenPopup()
    {
        if (_currentNode == null) return;
        var doc = MarkupEditor.Document;
        var offset = MarkupEditor.CaretOffset;
        if (offset < 0 || offset >= doc.TextLength) return;

        var line = doc.GetLineByOffset(offset);
        var lineText = doc.GetText(line.Offset, line.Length).TrimStart();

        string? tokenType = null;
        string currentToken = "";
        int tokenStart = 0, tokenEnd = 0;

        if (lineText.StartsWith('@'))
        {
            tokenType = "speaker";
            // Token is the word after @
            var content = lineText[1..].Split(' ', 2)[0];
            currentToken = content;
            tokenStart = line.Offset + (doc.GetText(line.Offset, line.Length).IndexOf('@')) + 1;
            tokenEnd = tokenStart + content.Length;
        }
        else if (lineText.StartsWith("->"))
        {
            tokenType = "goto";
            var content = lineText[2..].Trim();
            currentToken = content;
            var rawLine = doc.GetText(line.Offset, line.Length);
            var arrowIdx = rawLine.IndexOf("->");
            tokenStart = line.Offset + arrowIdx + 2;
            // Skip whitespace
            while (tokenStart < line.EndOffset && doc.GetCharAt(tokenStart) == ' ') tokenStart++;
            tokenEnd = line.EndOffset;
        }

        if (tokenType == null) return;

        _popupVm.AllItems.Clear();
        if (tokenType == "speaker")
        {
            var speakers = new HashSet<string> { "narrator", "player" };
            if (DataContext is NarrationEditorViewModel editorVm)
            {
                foreach (var node in editorVm.Nodes)
                    foreach (var stmt in node.Flow)
                        if (stmt.Kind == StatementKind.Speaker && !string.IsNullOrEmpty(stmt.Speaker))
                            speakers.Add(stmt.Speaker);
            }
            foreach (var s in speakers.OrderBy(x => x)) _popupVm.AllItems.Add(s);
        }
        else
        {
            if (DataContext is NarrationEditorViewModel editorVm)
                foreach (var node in editorVm.Nodes)
                    _popupVm.AllItems.Add(node.NodeId);
        }

        _popupVm.SearchText = currentToken;
        _popupVm.UpdateFilter();
        _popupVm.OnItemSelected = selected =>
        {
            doc.Replace(tokenStart, tokenEnd - tokenStart, selected);
            TokenPopup.IsOpen = false;
        };

        TokenPopup.IsOpen = true;
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingEditor || _currentNode == null) return;

        // Debounce: restart timer on each keystroke
        _debounceTimer?.Stop();
        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _debounceTimer.Tick += (s, args) =>
        {
            _debounceTimer?.Stop();
            if (_currentNode != null)
            {
                _isUpdatingEditor = true;
                try
                {
                    _currentNode.UpdateFlowFromMarkup(MarkupEditor.Text);
                }
                finally
                {
                    _isUpdatingEditor = false;
                }

                // Rebuild graph after flow changes
                if (DataContext is NarrationEditorViewModel vm)
                    RebuildGraph(vm);
            }
        };
        _debounceTimer.Start();
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
