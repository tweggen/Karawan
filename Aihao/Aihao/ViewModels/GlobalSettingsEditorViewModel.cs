using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aihao.ViewModels;

public partial class GlobalSettingsEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Global Settings";
    
    [ObservableProperty]
    private bool _isDirty;
    
    [ObservableProperty]
    private JsonObject? _settingsNode;
    
    public ObservableCollection<SettingItemViewModel> Settings { get; } = new();
    
    public void LoadFromJson(JsonObject globalSettings)
    {
        SettingsNode = globalSettings;
        Settings.Clear();
        
        foreach (var property in globalSettings)
        {
            var item = new SettingItemViewModel
            {
                Key = property.Key,
                Value = property.Value?.ToString() ?? string.Empty,
                ValueType = GetValueType(property.Value)
            };
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SettingItemViewModel.Value))
                    IsDirty = true;
            };
            Settings.Add(item);
        }
    }
    
    private SettingValueType GetValueType(JsonNode? node)
    {
        return node switch
        {
            JsonValue v when v.TryGetValue<bool>(out _) => SettingValueType.Boolean,
            JsonValue v when v.TryGetValue<int>(out _) => SettingValueType.Integer,
            JsonValue v when v.TryGetValue<double>(out _) => SettingValueType.Number,
            JsonArray => SettingValueType.Array,
            JsonObject => SettingValueType.Object,
            _ => SettingValueType.String
        };
    }
    
    [RelayCommand]
    private void Save()
    {
        if (SettingsNode == null) return;
        
        foreach (var setting in Settings)
        {
            SettingsNode[setting.Key] = setting.ValueType switch
            {
                SettingValueType.Boolean => bool.Parse(setting.Value),
                SettingValueType.Integer => int.Parse(setting.Value),
                SettingValueType.Number => double.Parse(setting.Value),
                _ => setting.Value
            };
        }
        
        IsDirty = false;
    }
    
    [RelayCommand]
    private void AddSetting()
    {
        var item = new SettingItemViewModel
        {
            Key = "newSetting",
            Value = "",
            ValueType = SettingValueType.String,
            IsNew = true
        };
        Settings.Add(item);
        IsDirty = true;
    }
    
    [RelayCommand]
    private void RemoveSetting(SettingItemViewModel? setting)
    {
        if (setting != null)
        {
            Settings.Remove(setting);
            IsDirty = true;
        }
    }
}

public partial class SettingItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _key = string.Empty;
    
    [ObservableProperty]
    private string _value = string.Empty;
    
    [ObservableProperty]
    private SettingValueType _valueType;
    
    [ObservableProperty]
    private bool _isNew;
}

public enum SettingValueType
{
    String,
    Integer,
    Number,
    Boolean,
    Array,
    Object
}
