using System.Collections.Specialized;
using Avalonia.Controls;
using Aihao.Models;
using Aihao.ViewModels;

namespace Aihao.Views;

public partial class MainWindow : Window
{
    private NativeMenuItem? _recentProjectsMenuItem;
    
    public MainWindow()
    {
        InitializeComponent();
        
        // Subscribe to DataContext changes to wire up collection changes
        DataContextChanged += OnDataContextChanged;
        
        // Find the recent projects menu item after the window is loaded
        Loaded += OnLoaded;
    }
    
    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Get the NativeMenu attached to this window
        var menu = NativeMenu.GetMenu(this);
        if (menu != null)
        {
            // Find File menu -> Open Recent submenu
            foreach (var item in menu.Items)
            {
                if (item is NativeMenuItem fileMenu && fileMenu.Header?.ToString() == "File")
                {
                    if (fileMenu.Menu != null)
                    {
                        foreach (var subItem in fileMenu.Menu.Items)
                        {
                            if (subItem is NativeMenuItem recentItem && recentItem.Header?.ToString() == "Open Recent")
                            {
                                _recentProjectsMenuItem = recentItem;
                                break;
                            }
                        }
                    }
                    break;
                }
            }
        }
        
        // Initial population if DataContext is already set
        if (DataContext is MainWindowViewModel vm)
        {
            RefreshRecentProjectsMenu(vm);
        }
    }
    
    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            // Subscribe to collection changes
            vm.RecentProjects.CollectionChanged += OnRecentProjectsChanged;
            
            // Initial population (if menu is already available)
            RefreshRecentProjectsMenu(vm);
        }
    }
    
    private void OnRecentProjectsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            RefreshRecentProjectsMenu(vm);
        }
    }
    
    private void RefreshRecentProjectsMenu(MainWindowViewModel vm)
    {
        if (_recentProjectsMenuItem?.Menu == null) return;
        
        var menu = _recentProjectsMenuItem.Menu;
        menu.Items.Clear();
        
        if (vm.RecentProjects.Count == 0)
        {
            var emptyItem = new NativeMenuItem("(No recent projects)")
            {
                IsEnabled = false
            };
            menu.Items.Add(emptyItem);
        }
        else
        {
            foreach (var project in vm.RecentProjects)
            {
                var item = new NativeMenuItem(project.Name)
                {
                    Command = vm.OpenRecentProjectCommand,
                    CommandParameter = project,
                    IsEnabled = project.Exists
                };
                menu.Items.Add(item);
            }
            
            // Add separator and clear option
            menu.Items.Add(new NativeMenuItemSeparator());
            
            var clearItem = new NativeMenuItem("Clear Recent Projects")
            {
                Command = vm.ClearRecentProjectsCommand
            };
            menu.Items.Add(clearItem);
        }
    }
}
