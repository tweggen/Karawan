using System;
using Avalonia.Controls;
using Aihao.ViewModels;

namespace Aihao.Views;

public partial class SettingsDialog : Window
{
    public SettingsDialog()
    {
        InitializeComponent();
    }
    
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        
        if (DataContext is SettingsDialogViewModel vm)
        {
            vm.RequestClose += OnRequestClose;
        }
    }
    
    private void OnRequestClose(object? sender, bool result)
    {
        if (DataContext is SettingsDialogViewModel vm)
        {
            vm.RequestClose -= OnRequestClose;
        }
        
        Close(result);
    }
}
