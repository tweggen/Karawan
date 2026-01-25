using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Aihao.Models;
using Aihao.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aihao.ViewModels;

/// <summary>
/// View model for a setting item in the dialog.
/// Implements INotifyPropertyChanged directly to avoid source generator conflicts.
/// </summary>
public class SettingsItemVM : INotifyPropertyChanged
{
    private readonly SettingsItem _definition;
    private object? _originalValue;
    private object? _currentValue;
    private bool _isModified;
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    public SettingsItem Definition => _definition;
    public string JsonPath => _definition.JsonPath;
    public string DisplayName => _definition.DisplayName;
    public string Description => _definition.Description;
    public SettingsItemType Type => _definition.Type;
    public List<ChoiceOption>? Choices => _definition.Choices;
    public int? MinValue => _definition.MinValue;
    public int? MaxValue => _definition.MaxValue;
    public bool RequiresRestart => _definition.RequiresRestart;
    public string? Category => _definition.Category;
    
    // Typed accessors for Min/Max as decimal for NumericUpDown
    public decimal? MinValueDecimal => MinValue.HasValue ? (decimal)MinValue.Value : null;
    public decimal? MaxValueDecimal => MaxValue.HasValue ? (decimal)MaxValue.Value : null;
    
    /// <summary>
    /// Generic value accessor.
    /// </summary>
    public object? Value
    {
        get => _currentValue;
        set => SetValue(value);
    }
    
    /// <summary>
    /// String value accessor for TextBox binding.
    /// </summary>
    public string StringValue
    {
        get => _currentValue?.ToString() ?? string.Empty;
        set => SetValue(value);
    }
    
    /// <summary>
    /// Boolean value accessor for ToggleSwitch binding.
    /// </summary>
    public bool BoolValue
    {
        get
        {
            if (_currentValue is bool b) return b;
            if (_currentValue is string s) return s.Equals("true", StringComparison.OrdinalIgnoreCase);
            return false;
        }
        set => SetValue(value);
    }
    
    /// <summary>
    /// Decimal value accessor for NumericUpDown binding.
    /// </summary>
    public decimal? DecimalValue
    {
        get
        {
            if (_currentValue == null) return null;
            if (_currentValue is int i) return i;
            if (_currentValue is long l) return l;
            if (_currentValue is double d) return (decimal)d;
            if (_currentValue is float f) return (decimal)f;
            if (_currentValue is decimal dec) return dec;
            if (_currentValue is string s && int.TryParse(s, out var parsed)) return parsed;
            return null;
        }
        set
        {
            // Store as int since that's what we want for settings
            if (value.HasValue)
                SetValue((int)value.Value);
            else
                SetValue(null);
        }
    }
    
    public bool IsModified
    {
        get => _isModified;
        private set
        {
            if (_isModified != value)
            {
                _isModified = value;
                OnPropertyChanged(nameof(IsModified));
            }
        }
    }
    
    public SettingsItemVM(SettingsItem definition)
    {
        _definition = definition;
        _currentValue = definition.DefaultValue;
        _originalValue = definition.DefaultValue;
    }
    
    private void SetValue(object? value)
    {
        if (!Equals(_currentValue, value))
        {
            _currentValue = value;
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(StringValue));
            OnPropertyChanged(nameof(BoolValue));
            OnPropertyChanged(nameof(DecimalValue));
            IsModified = !Equals(value, _originalValue);
        }
    }
    
    public void SetValueWithoutModified(object? value)
    {
        _currentValue = value;
        _originalValue = value;
        _isModified = false;
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(StringValue));
        OnPropertyChanged(nameof(BoolValue));
        OnPropertyChanged(nameof(DecimalValue));
        OnPropertyChanged(nameof(IsModified));
    }
    
    public void ResetToDefault()
    {
        SetValue(_definition.DefaultValue);
    }
    
    public void MarkAsSaved()
    {
        _originalValue = _currentValue;
        IsModified = false;
    }
    
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// View model for a section in the settings tree.
/// </summary>
public partial class SettingsSectionViewModel : ObservableObject
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Icon { get; }
    
    public ObservableCollection<SettingsSectionViewModel> Children { get; } = new();
    public ObservableCollection<SettingsItemVM> Items { get; } = new();
    
    [ObservableProperty]
    private bool _isExpanded = true;
    
    [ObservableProperty]
    private bool _isSelected;
    
    public SettingsSectionViewModel(SettingsSection section)
    {
        Id = section.Id;
        DisplayName = section.DisplayName;
        Icon = section.Icon;
        
        foreach (var item in section.Items)
        {
            Items.Add(new SettingsItemVM(item));
        }
        
        foreach (var child in section.Children)
        {
            Children.Add(new SettingsSectionViewModel(child));
        }
    }
    
    /// <summary>
    /// Get all items from this section and all children recursively.
    /// </summary>
    public IEnumerable<SettingsItemVM> GetAllItems()
    {
        foreach (var item in Items)
        {
            yield return item;
        }
        
        foreach (var child in Children)
        {
            foreach (var item in child.GetAllItems())
            {
                yield return item;
            }
        }
    }
}

/// <summary>
/// View model for the settings dialog.
/// </summary>
public partial class SettingsDialogViewModel : ObservableObject
{
    private readonly UserSettingsService _settingsService;
    private Dictionary<string, SettingsItemVM> _itemsByPath = new();
    
    public ObservableCollection<SettingsSectionViewModel> Sections { get; } = new();
    
    [ObservableProperty]
    private SettingsSectionViewModel? _selectedSection;
    
    [ObservableProperty]
    private string _searchText = string.Empty;
    
    [ObservableProperty]
    private bool _hasChanges;
    
    [ObservableProperty]
    private bool _hasRestartRequired;
    
    /// <summary>
    /// Event raised when dialog should close with result.
    /// </summary>
    public event EventHandler<bool>? RequestClose;
    
    public SettingsDialogViewModel(UserSettingsService settingsService)
    {
        _settingsService = settingsService;
        
        // Build sections from definitions
        foreach (var sectionDef in SettingsDefinitions.GetAllSections())
        {
            var section = new SettingsSectionViewModel(sectionDef);
            Sections.Add(section);
            
            // Index all items by path
            foreach (var item in section.GetAllItems())
            {
                _itemsByPath[item.JsonPath] = item;
                item.PropertyChanged += OnItemPropertyChanged;
            }
        }
        
        // Select first section
        if (Sections.Count > 0)
        {
            SelectedSection = Sections[0];
            SelectedSection.IsSelected = true;
        }
        
        // Load current values
        LoadCurrentValues();
    }
    
    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsItemVM.IsModified))
        {
            UpdateHasChanges();
        }
    }
    
    private void LoadCurrentValues()
    {
        var settings = _settingsService.Settings;
        
        foreach (var kvp in _itemsByPath)
        {
            var path = kvp.Key;
            var item = kvp.Value;
            
            object? value = null;
            
            // Check if it's a module setting
            if (path.StartsWith("modules."))
            {
                var parts = path.Split('.', 3);
                if (parts.Length >= 3)
                {
                    var moduleId = parts[1];
                    var key = parts[2];
                    var moduleSettings = settings.GetModuleSettings(moduleId);
                    if (moduleSettings.TryGetPropertyValue(key, out var node))
                    {
                        value = GetValueFromJson(node, item.Type);
                    }
                }
            }
            else
            {
                // Direct property on UserSettings
                value = GetSettingsProperty(settings, path);
            }
            
            if (value != null)
            {
                item.SetValueWithoutModified(value);
            }
        }
    }
    
    private object? GetSettingsProperty(UserSettings settings, string path)
    {
        return path switch
        {
            "theme" => settings.Theme,
            "editorFontSize" => settings.EditorFontSize,
            "restoreLastProject" => settings.RestoreLastProject,
            "autoSaveSettings" => settings.AutoSaveSettings,
            "maxRecentProjects" => settings.MaxRecentProjects,
            "lastDirectory" => settings.LastDirectory,
            "consoleLogLevel" => settings.ConsoleLogLevel,
            "defaultOverlayPriority" => settings.DefaultOverlayPriority,
            "defaultOverlayPrefix" => settings.DefaultOverlayPrefix,
            _ => null
        };
    }
    
    private void SetSettingsProperty(UserSettings settings, string path, object? value)
    {
        switch (path)
        {
            case "theme":
                settings.Theme = value as string ?? "Dark";
                break;
            case "editorFontSize":
                settings.EditorFontSize = value is int i ? i : 13;
                break;
            case "restoreLastProject":
                settings.RestoreLastProject = value is bool b && b;
                break;
            case "autoSaveSettings":
                settings.AutoSaveSettings = value is not bool ab || ab;
                break;
            case "maxRecentProjects":
                settings.MaxRecentProjects = value is int mp ? mp : 10;
                break;
            case "lastDirectory":
                settings.LastDirectory = value as string;
                break;
            case "consoleLogLevel":
                settings.ConsoleLogLevel = value as string ?? "Info";
                break;
            case "defaultOverlayPriority":
                settings.DefaultOverlayPriority = value is int op ? op : 100;
                break;
            case "defaultOverlayPrefix":
                settings.DefaultOverlayPrefix = value as string ?? "local";
                break;
        }
    }
    
    private object? GetValueFromJson(JsonNode? node, SettingsItemType type)
    {
        if (node == null) return null;
        
        return type switch
        {
            SettingsItemType.Bool => node.GetValue<bool>(),
            SettingsItemType.Int => node.GetValue<int>(),
            SettingsItemType.String or SettingsItemType.Choice or SettingsItemType.Path => node.GetValue<string>(),
            _ => node.ToString()
        };
    }
    
    private void UpdateHasChanges()
    {
        HasChanges = _itemsByPath.Values.Any(i => i.IsModified);
        HasRestartRequired = _itemsByPath.Values.Any(i => i.IsModified && i.RequiresRestart);
    }
    
    partial void OnSelectedSectionChanged(SettingsSectionViewModel? oldValue, SettingsSectionViewModel? newValue)
    {
        if (oldValue != null)
        {
            oldValue.IsSelected = false;
        }
        if (newValue != null)
        {
            newValue.IsSelected = true;
        }
    }
    
    [RelayCommand]
    private void SelectSection(SettingsSectionViewModel? section)
    {
        if (section != null)
        {
            SelectedSection = section;
        }
    }
    
    [RelayCommand]
    private async Task Save()
    {
        var settings = _settingsService.Settings;
        
        foreach (var kvp in _itemsByPath)
        {
            var path = kvp.Key;
            var item = kvp.Value;
            
            if (!item.IsModified) continue;
            
            // Check if it's a module setting
            if (path.StartsWith("modules."))
            {
                var parts = path.Split('.', 3);
                if (parts.Length >= 3)
                {
                    var moduleId = parts[1];
                    var key = parts[2];
                    var jsonValue = ConvertToJsonNode(item.Value, item.Type);
                    settings.SetModuleSetting(moduleId, key, jsonValue);
                }
            }
            else
            {
                // Direct property on UserSettings
                SetSettingsProperty(settings, path, item.Value);
            }
            
            item.MarkAsSaved();
        }
        
        await _settingsService.SaveAsync();
        
        HasChanges = false;
        RequestClose?.Invoke(this, true);
    }
    
    private JsonNode? ConvertToJsonNode(object? value, SettingsItemType type)
    {
        if (value == null) return null;
        
        return type switch
        {
            SettingsItemType.Bool => JsonValue.Create((bool)value),
            SettingsItemType.Int => JsonValue.Create((int)value),
            SettingsItemType.String or SettingsItemType.Choice or SettingsItemType.Path => JsonValue.Create((string)value),
            _ => JsonValue.Create(value.ToString())
        };
    }
    
    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(this, false);
    }
    
    [RelayCommand]
    private void ResetToDefaults()
    {
        foreach (var item in _itemsByPath.Values)
        {
            item.ResetToDefault();
        }
    }
    
    [RelayCommand]
    private void ResetSectionToDefaults()
    {
        if (SelectedSection == null) return;
        
        foreach (var item in SelectedSection.GetAllItems())
        {
            item.ResetToDefault();
        }
    }
}
