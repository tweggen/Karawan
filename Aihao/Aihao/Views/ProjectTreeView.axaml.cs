using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aihao.ViewModels;

namespace Aihao.Views;

public partial class ProjectTreeView : UserControl
{
    public ProjectTreeView()
    {
        InitializeComponent();
    }
    
    private void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is ProjectTreeViewModel vm && vm.SelectedItem != null)
        {
            vm.ItemDoubleClickedCommand.Execute(vm.SelectedItem);
        }
    }
}
