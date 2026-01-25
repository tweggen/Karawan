using Avalonia.Controls;
using Avalonia.Input;
using Aihao.ViewModels;

namespace Aihao.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
    
    private void OnTabPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is DocumentTabViewModel tab)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.SelectedDocument = tab;
            }
        }
    }
}
