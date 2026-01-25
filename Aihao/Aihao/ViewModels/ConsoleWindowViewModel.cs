using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aihao.ViewModels;

public partial class ConsoleWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Console";
    
    [ObservableProperty]
    private string _searchText = string.Empty;
    
    [ObservableProperty]
    private bool _showInfo = true;
    
    [ObservableProperty]
    private bool _showWarning = true;
    
    [ObservableProperty]
    private bool _showError = true;
    
    [ObservableProperty]
    private bool _showDebug = true;
    
    [ObservableProperty]
    private bool _isVisible = true;
    
    [ObservableProperty]
    private bool _autoScroll = true;
    
    public ObservableCollection<ConsoleLineViewModel> Lines { get; } = new();
    public ObservableCollection<ConsoleLineViewModel> FilteredLines { get; } = new();
    
    private int _infoCount;
    private int _warningCount;
    private int _errorCount;
    
    [ObservableProperty]
    private string _statusText = "0 messages";
    
    public void AddLine(string text, LogLevel level)
    {
        var line = new ConsoleLineViewModel
        {
            Text = text,
            Level = level,
            Timestamp = DateTime.Now
        };
        
        Lines.Add(line);
        
        // Update counts
        switch (level)
        {
            case LogLevel.Info: _infoCount++; break;
            case LogLevel.Warning: _warningCount++; break;
            case LogLevel.Error: _errorCount++; break;
        }
        
        UpdateStatusText();
        ApplyFilter();
    }
    
    private void UpdateStatusText()
    {
        StatusText = $"{Lines.Count} messages | {_infoCount} info | {_warningCount} warnings | {_errorCount} errors";
    }
    
    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnShowInfoChanged(bool value) => ApplyFilter();
    partial void OnShowWarningChanged(bool value) => ApplyFilter();
    partial void OnShowErrorChanged(bool value) => ApplyFilter();
    partial void OnShowDebugChanged(bool value) => ApplyFilter();
    
    private void ApplyFilter()
    {
        FilteredLines.Clear();
        
        foreach (var line in Lines)
        {
            var levelMatch = line.Level switch
            {
                LogLevel.Info => ShowInfo,
                LogLevel.Warning => ShowWarning,
                LogLevel.Error => ShowError,
                LogLevel.Debug => ShowDebug,
                _ => true
            };
            
            if (!levelMatch) continue;
            
            var textMatch = string.IsNullOrEmpty(SearchText) ||
                            line.Text.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            
            if (textMatch)
            {
                FilteredLines.Add(line);
            }
        }
    }
    
    [RelayCommand]
    public void Clear()
    {
        Lines.Clear();
        FilteredLines.Clear();
        _infoCount = 0;
        _warningCount = 0;
        _errorCount = 0;
        UpdateStatusText();
    }
    
    [RelayCommand]
    private void CopyToClipboard()
    {
        // TODO: Copy all filtered lines to clipboard
    }
    
    [RelayCommand]
    private void SaveToFile()
    {
        // TODO: Save log to file
    }
}

public partial class ConsoleLineViewModel : ObservableObject
{
    [ObservableProperty]
    private string _text = string.Empty;
    
    [ObservableProperty]
    private LogLevel _level;
    
    [ObservableProperty]
    private DateTime _timestamp;
    
    public string TimestampText => Timestamp.ToString("HH:mm:ss.fff");
    
    public string LevelIcon => Level switch
    {
        LogLevel.Info => "ℹ️",
        LogLevel.Warning => "⚠️",
        LogLevel.Error => "❌",
        LogLevel.Debug => "🔍",
        _ => "📝"
    };
    
    public string LevelColor => Level switch
    {
        LogLevel.Info => "#3794FF",
        LogLevel.Warning => "#FF8C00",
        LogLevel.Error => "#F44747",
        LogLevel.Debug => "#6A9955",
        _ => "#CCCCCC"
    };
}

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}
