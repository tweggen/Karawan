using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aihao.ViewModels;

namespace Aihao.Views;

public partial class CommandPalette : Window
{
    private ScrollViewer? _resultsScroller;
    
    public CommandPalette()
    {
        InitializeComponent();
        
        _resultsScroller = this.FindControl<ScrollViewer>("ResultsScroller");
    }
    
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        
        // Focus the search box
        var searchBox = this.FindControl<TextBox>("SearchBox");
        searchBox?.Focus();
    }
    
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        
        if (DataContext is CommandPaletteViewModel vm)
        {
            vm.RequestClose += OnRequestClose;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }
    
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommandPaletteViewModel.SelectedIndex))
        {
            ScrollSelectedIntoView();
        }
    }
    
    private void ScrollSelectedIntoView()
    {
        if (DataContext is not CommandPaletteViewModel vm) return;
        if (_resultsScroller == null) return;
        
        var index = vm.SelectedIndex;
        if (index < 0) return;
        
        // Estimate item height (approximately 52 pixels per item)
        const double itemHeight = 52;
        var targetOffset = index * itemHeight;
        
        // Get current scroll position
        var currentOffset = _resultsScroller.Offset.Y;
        var viewportHeight = _resultsScroller.Viewport.Height;
        
        // Scroll if needed
        if (targetOffset < currentOffset)
        {
            _resultsScroller.Offset = new Vector(0, targetOffset);
        }
        else if (targetOffset + itemHeight > currentOffset + viewportHeight)
        {
            _resultsScroller.Offset = new Vector(0, targetOffset + itemHeight - viewportHeight);
        }
    }
    
    private void OnRequestClose(object? sender, EventArgs e)
    {
        if (DataContext is CommandPaletteViewModel vm)
        {
            vm.RequestClose -= OnRequestClose;
            vm.PropertyChanged -= OnViewModelPropertyChanged;
        }
        
        Close();
    }
    
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (DataContext is not CommandPaletteViewModel vm)
        {
            base.OnKeyDown(e);
            return;
        }
        
        switch (e.Key)
        {
            case Key.Escape:
                vm.CancelCommand.Execute(null);
                e.Handled = true;
                break;
                
            case Key.Enter:
                vm.ExecuteSelectedCommand.Execute(null);
                e.Handled = true;
                break;
                
            case Key.Up:
                vm.SelectPreviousCommand.Execute(null);
                e.Handled = true;
                break;
                
            case Key.Down:
                vm.SelectNextCommand.Execute(null);
                e.Handled = true;
                break;
                
            case Key.Tab:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    vm.SelectPreviousCommand.Execute(null);
                else
                    vm.SelectNextCommand.Execute(null);
                e.Handled = true;
                break;
                
            default:
                base.OnKeyDown(e);
                break;
        }
    }
    
    private void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && 
            border.DataContext is CommandPaletteItemVM item &&
            DataContext is CommandPaletteViewModel vm)
        {
            vm.ExecuteItemCommand.Execute(item);
        }
    }
    
    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        
        // Close when focus is lost (unless focus moved to a child)
        if (DataContext is CommandPaletteViewModel vm)
        {
            // Give a small delay to check if focus moved within the window
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var focused = FocusManager?.GetFocusedElement();
                if (focused == null || !this.GetVisualDescendants().Contains(focused as Visual))
                {
                    // Focus moved outside - but don't close if we're already closing
                    // vm.CancelCommand.Execute(null);
                }
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
    }
}
