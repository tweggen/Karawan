using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Aihao.ViewModels;

namespace Aihao.Services;

/// <summary>
/// Manages dockable windows and layout persistence
/// </summary>
public class DockingService
{
    private readonly Dictionary<string, DockableWindowInfo> _registeredWindows = new();
    
    public ObservableCollection<DockableWindowInfo> OpenWindows { get; } = new();
    
    public event EventHandler<DockableWindowInfo>? WindowOpened;
    public event EventHandler<DockableWindowInfo>? WindowClosed;
    
    public void RegisterWindow(string id, string title, Type viewModelType, DockPosition defaultPosition)
    {
        _registeredWindows[id] = new DockableWindowInfo
        {
            Id = id,
            Title = title,
            ViewModelType = viewModelType,
            DefaultPosition = defaultPosition
        };
    }
    
    public DockableWindowInfo? OpenWindow(string id, object? context = null)
    {
        if (!_registeredWindows.TryGetValue(id, out var windowInfo))
            return null;
            
        var instance = new DockableWindowInfo
        {
            Id = id,
            Title = windowInfo.Title,
            ViewModelType = windowInfo.ViewModelType,
            DefaultPosition = windowInfo.DefaultPosition,
            Context = context,
            IsOpen = true
        };
        
        OpenWindows.Add(instance);
        WindowOpened?.Invoke(this, instance);
        
        return instance;
    }
    
    public void CloseWindow(DockableWindowInfo window)
    {
        window.IsOpen = false;
        OpenWindows.Remove(window);
        WindowClosed?.Invoke(this, window);
    }
    
    public IEnumerable<string> GetRegisteredWindowIds() => _registeredWindows.Keys;
}

public class DockableWindowInfo
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Type ViewModelType { get; set; } = typeof(object);
    public DockPosition DefaultPosition { get; set; }
    public object? Context { get; set; }
    public bool IsOpen { get; set; }
    public object? ViewModel { get; set; }
}

public enum DockPosition
{
    Left,
    Right,
    Bottom,
    Center,
    Float
}
