using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Aihao.ViewModels;

namespace Aihao.Views;

public partial class ConsoleWindow : UserControl
{
    private ScrollViewer? _scrollViewer;
    private SelectableTextBlock? _logTextBlock;
    
    public ConsoleWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }
    
    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _scrollViewer = this.FindControl<ScrollViewer>("LogScrollViewer");
        _logTextBlock = this.FindControl<SelectableTextBlock>("LogTextBlock");
        
        if (DataContext is ConsoleWindowViewModel vm)
        {
            vm.FilteredLines.CollectionChanged += OnLinesChanged;
            vm.SelectAllRequested += OnSelectAllRequested;
        }
    }
    
    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is ConsoleWindowViewModel vm && vm.AutoScroll && _scrollViewer != null)
        {
            _scrollViewer.ScrollToEnd();
        }
    }
    
    private void OnSelectAllRequested(object? sender, System.EventArgs e)
    {
        _logTextBlock?.SelectAll();
        _logTextBlock?.Focus();
    }
}
