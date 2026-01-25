using System;
using Avalonia.Controls;
using Avalonia.Input;
using Aihao.ViewModels;

namespace Aihao.Views;

public partial class KeyBindingsEditor : Window
{
    public KeyBindingsEditor()
    {
        InitializeComponent();
    }
    
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        
        if (DataContext is KeyBindingsEditorViewModel vm)
        {
            vm.RequestClose += OnRequestClose;
        }
    }
    
    private void OnRequestClose(object? sender, bool result)
    {
        if (DataContext is KeyBindingsEditorViewModel vm)
        {
            vm.RequestClose -= OnRequestClose;
        }
        
        Close(result);
    }
    
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is KeyBindingsEditorViewModel vm && vm.RecordingItem != null)
        {
            // Convert Avalonia KeyModifiers to our modifiers
            vm.RecordKeyPress(e.Key, e.KeyModifiers);
            e.Handled = true;
        }
    }
}
