using Avalonia.Controls;
using Avalonia.Input;
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
        if (DataContext is ProjectTreeViewModel vm)
        {
            // Get the actual item from the sender's DataContext, not from SelectedItem
            // because selection may not have updated yet
            FileTreeItemViewModel? item = null;
            
            if (sender is Control control && control.DataContext is FileTreeItemViewModel treeItem)
            {
                item = treeItem;
            }
            else if (vm.SelectedItem != null)
            {
                item = vm.SelectedItem;
            }
            
            if (item != null)
            {
                vm.ItemDoubleClickedCommand.Execute(item);
            }
        }
    }
}
