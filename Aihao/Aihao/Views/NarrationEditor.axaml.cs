using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using AvaloniaEdit.Rendering;
using Aihao.ViewModels;

namespace Aihao.Views;

public partial class NarrationEditor : UserControl
{
    private NarrationScriptGraphViewModel _graphVm = new();
    private NarrationNodeViewModel? _currentNode;
    private DispatcherTimer? _debounceTimer;
    private bool _isUpdatingEditor;
    private NarrationTokenPopupViewModel _popupVm = new();

    /// <summary>
    /// Character IDs available for speaker autocomplete.
    /// Set from the parent document view model.
    /// </summary>
    public static readonly StyledProperty<IEnumerable<string>> CharacterIdsProperty =
        AvaloniaProperty.Register<NarrationEditor, IEnumerable<string>>(
            nameof(CharacterIds),
            defaultValue: Enumerable.Empty<string>());

    public IEnumerable<string> CharacterIds
    {
        get => GetValue(CharacterIdsProperty);
        set => SetValue(CharacterIdsProperty, value);
    }

    public NarrationEditor()
    {
        InitializeComponent();
        LoadSyntaxHighlighting();
        SetupEditorColors();
        TokenPopupControl.DataContext = _popupVm;
        DataContextChanged += OnDataContextChanged;
    }

    private void SetupEditorColors()
    {
        var fg = new SolidColorBrush(Color.Parse("#D4D4D4"));
        var bg = new SolidColorBrush(Color.Parse("#1E1E2E"));

        MarkupEditor.Foreground = fg;
        MarkupEditor.Background = bg;
        MarkupEditor.TextArea.Foreground = fg;
        MarkupEditor.TextArea.Background = bg;
        MarkupEditor.TextArea.TextView.LinkTextForegroundBrush = Brushes.CornflowerBlue;
        MarkupEditor.LineNumbersForeground = new SolidColorBrush(Color.FromRgb(0x85, 0x85, 0x85));

        // Force default text color via a line transformer — this ensures
        // AvaloniaEdit's own rendering pipeline uses our foreground color.
        MarkupEditor.TextArea.TextView.LineTransformers.Insert(0, new DefaultForegroundColorizer(fg));
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
        System.Console.Error.WriteLine($"[NarrationEditor] OnDataContextChanged: DataContext is {DataContext?.GetType().Name ?? "null"}");
        if (DataContext is NarrationEditorViewModel vm)
        {
            System.Console.Error.WriteLine($"[NarrationEditor] VM.SelectedNode={vm.SelectedNode?.NodeId ?? "null"}, VM.SelectedScriptName={vm.SelectedScriptName ?? "null"}, Nodes.Count={vm.Nodes.Count}");
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(vm.SelectedNode))
                {
                    System.Console.Error.WriteLine($"[NarrationEditor] PropertyChanged: SelectedNode -> {vm.SelectedNode?.NodeId ?? "null"}");
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
        System.Console.Error.WriteLine($"[NarrationEditor] OnSelectedNodeChanged: node={node?.NodeId ?? "null"}, MarkupText length={node?.MarkupText?.Length ?? -1}");
        if (node != null)
        {
            System.Console.Error.WriteLine($"[NarrationEditor] MarkupText first 200 chars: [{node.MarkupText?[..Math.Min(node.MarkupText?.Length ?? 0, 200)] ?? "NULL"}]");
            System.Console.Error.WriteLine($"[NarrationEditor] Flow.Count={node.Flow.Count}");
        }

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
            System.Console.Error.WriteLine($"[NarrationEditor] OnNodePropertyChanged: MarkupText changed, length={_currentNode?.MarkupText?.Length ?? -1}");
            if (_currentNode != null)
                SetEditorText(_currentNode.MarkupText);
        }
    }

    private void SetEditorText(string text)
    {
        System.Console.Error.WriteLine($"[NarrationEditor] SetEditorText: length={text?.Length ?? -1}, editor IsVisible={MarkupEditor.IsVisible}, editor Bounds={MarkupEditor.Bounds}");
        _isUpdatingEditor = true;
        try
        {
            MarkupEditor.Document.Text = text ?? "";
            System.Console.Error.WriteLine($"[NarrationEditor] SetEditorText: Document.Text length after set={MarkupEditor.Document.TextLength}");
        }
        finally
        {
            _isUpdatingEditor = false;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        System.Console.Error.WriteLine($"[NarrationEditor] OnAttachedToVisualTree: _currentNode={_currentNode?.NodeId ?? "null"}");
        base.OnAttachedToVisualTree(e);
        MarkupEditor.TextChanged += OnEditorTextChanged;
        MarkupEditor.TextArea.PointerPressed += OnEditorPointerPressed;

        // Defer text sync until after layout
        Dispatcher.UIThread.Post(() =>
        {
            System.Console.Error.WriteLine($"[NarrationEditor] Deferred sync: _currentNode={_currentNode?.NodeId ?? "null"}, MarkupText length={_currentNode?.MarkupText?.Length ?? -1}, editor Bounds={MarkupEditor.Bounds}");
            if (_currentNode != null)
                SetEditorText(_currentNode.MarkupText);
        }, DispatcherPriority.Loaded);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        MarkupEditor.TextChanged -= OnEditorTextChanged;
        MarkupEditor.TextArea.PointerPressed -= OnEditorPointerPressed;
        _debounceTimer?.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    private Point? _lastRightClickPosition;

    private void OnEditorPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(MarkupEditor.TextArea.TextView).Properties;
        if (props.IsRightButtonPressed)
        {
            // Store click position relative to the text view for popup placement
            _lastRightClickPosition = e.GetPosition(MarkupEditor.TextArea.TextView);

            // Compute the text offset at the click position instead of using the caret
            var clickPos = e.GetPosition(MarkupEditor.TextArea.TextView);
            var textPos = MarkupEditor.TextArea.TextView.GetPosition(clickPos);
            int clickOffset;
            if (textPos.HasValue)
            {
                clickOffset = MarkupEditor.Document.GetOffset(textPos.Value.Location);
            }
            else
            {
                clickOffset = MarkupEditor.CaretOffset;
            }

            Dispatcher.UIThread.Post(() => TryShowTokenPopup(clickOffset), DispatcherPriority.Input);
        }
    }

    private void TryShowTokenPopup(int offset)
    {
        if (_currentNode == null) return;
        var doc = MarkupEditor.Document;
        if (offset < 0 || offset >= doc.TextLength) return;

        var line = doc.GetLineByOffset(offset);
        var rawLine = doc.GetText(line.Offset, line.Length);
        var lineText = rawLine.TrimStart();

        string? tokenType = null;
        string currentToken = "";
        int tokenStart = 0, tokenEnd = 0;

        // Check for @ speaker at start of line
        if (lineText.StartsWith('@'))
        {
            tokenType = "speaker";
            var content = lineText[1..].Split(' ', 2)[0];
            currentToken = content;
            tokenStart = line.Offset + rawLine.IndexOf('@') + 1;
            tokenEnd = tokenStart + content.Length;
        }
        else
        {
            // Check for -> anywhere on the line (standalone goto or inline choice goto)
            var arrowIdx = rawLine.IndexOf("->");
            if (arrowIdx >= 0)
            {
                // Only activate if the click is on or after the -> token
                var arrowAbsOffset = line.Offset + arrowIdx;
                if (offset >= arrowAbsOffset)
                {
                    tokenType = "goto";
                    tokenStart = arrowAbsOffset + 2;
                    while (tokenStart < line.EndOffset && doc.GetCharAt(tokenStart) == ' ') tokenStart++;
                    tokenEnd = line.EndOffset;
                    currentToken = doc.GetText(tokenStart, tokenEnd - tokenStart).Trim();
                }
            }
        }

        if (tokenType == null) return;

        _popupVm.AllItems.Clear();
        if (tokenType == "speaker")
        {
            var speakers = new HashSet<string>();

            // First, add character IDs from the characters section (primary source)
            foreach (var charId in CharacterIds)
            {
                speakers.Add(charId);
            }

            // Fallback: add default speakers if no characters defined
            if (speakers.Count == 0)
            {
                speakers.Add("narrator");
                speakers.Add("player");
            }

            // Also include dynamically-discovered speakers from current script (for backwards compatibility)
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

        // Position popup at the right-click location
        if (_lastRightClickPosition.HasValue)
        {
            var p = _lastRightClickPosition.Value;
            TokenPopup.PlacementTarget = MarkupEditor.TextArea.TextView;
            TokenPopup.PlacementRect = new Rect(p.X, p.Y, 1, 1);
        }

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
                    _currentNode.UpdateFlowFromMarkup(MarkupEditor.Document.Text);
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

/// <summary>
/// Applies a default foreground color to all text in the editor.
/// This ensures text is visible regardless of AvaloniaEdit's theme handling.
/// </summary>
internal class DefaultForegroundColorizer : DocumentColorizingTransformer
{
    private readonly IBrush _foreground;

    public DefaultForegroundColorizer(IBrush foreground)
    {
        _foreground = foreground;
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (line.Length == 0) return;
        ChangeLinePart(line.Offset, line.EndOffset, element =>
        {
            element.TextRunProperties.SetForegroundBrush(_foreground);
        });
    }
}
