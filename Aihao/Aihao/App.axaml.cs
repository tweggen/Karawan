using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Aihao.ViewModels;
using Aihao.Views;
using System.Threading.Tasks;

namespace Aihao;

public partial class App : Application
{
    private MainWindowViewModel? _mainViewModel;
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _mainViewModel = new MainWindowViewModel();
            
            var mainWindow = new MainWindow
            {
                DataContext = _mainViewModel,
            };
            
            desktop.MainWindow = mainWindow;
            
            // Initialize async after window is created
            InitializeAsync(mainWindow);
            
            // Save window state on close
            desktop.ShutdownRequested += OnShutdownRequested;
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    private async void InitializeAsync(MainWindow mainWindow)
    {
        if (_mainViewModel == null) return;
        
        // Load user settings
        await _mainViewModel.InitializeAsync();
        
        // Restore window state
        var (x, y, width, height, maximized) = _mainViewModel.GetSavedWindowState();
        
        if (width.HasValue && height.HasValue && width.Value > 0 && height.Value > 0)
        {
            mainWindow.Width = width.Value;
            mainWindow.Height = height.Value;
        }
        
        if (x.HasValue && y.HasValue)
        {
            mainWindow.Position = new PixelPoint(x.Value, y.Value);
        }
        
        if (maximized)
        {
            mainWindow.WindowState = WindowState.Maximized;
        }
    }
    
    private async void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (_mainViewModel == null) return;
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && 
            desktop.MainWindow is MainWindow mainWindow)
        {
            // Save window state
            var isMaximized = mainWindow.WindowState == WindowState.Maximized;
            
            // Get position and size (use restored bounds if maximized)
            int? x = null, y = null, width = null, height = null;
            
            if (!isMaximized)
            {
                x = mainWindow.Position.X;
                y = mainWindow.Position.Y;
                width = (int)mainWindow.Width;
                height = (int)mainWindow.Height;
            }
            
            await _mainViewModel.SaveWindowStateAsync(x, y, width, height, isMaximized);
        }
    }
}
