using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Aihao.ViewModels;

namespace Aihao.Views;

public partial class ConsoleWindow : UserControl
{
    private ScrollViewer? _scrollViewer;
    
    public ConsoleWindow()
    {
        InitializeComponent();
        
        // Set up auto-scroll
        Loaded += OnLoaded;
    }
    
    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _scrollViewer = this.FindControl<ScrollViewer>("LogScrollViewer");
        
        if (DataContext is ConsoleWindowViewModel vm)
        {
            vm.FilteredLines.CollectionChanged += OnLinesChanged;
        }
    }
    
    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is ConsoleWindowViewModel vm && vm.AutoScroll && _scrollViewer != null)
        {
            _scrollViewer.ScrollToEnd();
        }
    }
    
    private void OnLinePointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2A2A2A"));
        }
    }
    
    private void OnLinePointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = Avalonia.Media.Brushes.Transparent;
        }
    }
}
