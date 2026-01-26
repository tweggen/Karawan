using System;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aihao.ViewModels;

/// <summary>
/// View model for the Global Settings editor.
/// Uses the unified JsonPropertyEditorViewModel for editing.
/// </summary>
public partial class GlobalSettingsEditorViewModel : ObservableObject
{
    private JsonObject? _originalJson;
    
    [ObservableProperty]
    private string _title = "Global Settings";
    
    /// <summary>
    /// The unified property editor for this section.
    /// </summary>
    public JsonPropertyEditorViewModel PropertyEditor { get; }
    
    /// <summary>
    /// Proxy for IsDirty to maintain compatibility.
    /// </summary>
    public bool IsDirty => PropertyEditor.IsDirty;
    
    public GlobalSettingsEditorViewModel()
    {
        PropertyEditor = new JsonPropertyEditorViewModel
        {
            Title = "Global Settings",
            AllowTypeChange = true,
            AllowAddRemove = true,
            ShowToolbar = true
        };
        
        PropertyEditor.Modified += (_, _) => OnPropertyChanged(nameof(IsDirty));
    }
    
    public void LoadFromJson(JsonObject globalSettings)
    {
        _originalJson = globalSettings;
        PropertyEditor.LoadFromJson(globalSettings);
    }
    
    [RelayCommand]
    private void Save()
    {
        if (_originalJson == null) return;
        
        // Update original JSON from the property editor
        var newData = PropertyEditor.ToJsonObject();
        
        _originalJson.Clear();
        foreach (var prop in newData)
        {
            _originalJson[prop.Key] = prop.Value?.DeepClone();
        }
        
        PropertyEditor.ClearModifiedFlags();
        OnPropertyChanged(nameof(IsDirty));
    }
    
    /// <summary>
    /// Build a new JsonObject from current state (for external saving).
    /// </summary>
    public JsonObject ToJson()
    {
        return PropertyEditor.ToJsonObject();
    }
}
