using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aihao.ViewModels;

/// <summary>
/// Represents a node in the JSON settings tree.
/// Can be a branch (has children) or a leaf (has editable value).
/// </summary>
public partial class JsonTreeNode : ObservableObject
{
    private readonly GlobalSettingsEditorViewModel _owner;
    
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _fullPath = string.Empty;
    
    [ObservableProperty]
    private string _value = string.Empty;
    
    [ObservableProperty]
    private JsonValueKind _valueKind = JsonValueKind.String;
    
    [ObservableProperty]
    private bool _isExpanded = true;
    
    [ObservableProperty]
    private bool _isSelected;
    
    [ObservableProperty]
    private bool _isModified;
    
    [ObservableProperty]
    private string? _validationError;
    
    public ObservableCollection<JsonTreeNode> Children { get; } = new();
    
    /// <summary>
    /// True if this is a leaf node (has a value, no children).
    /// </summary>
    public bool IsLeaf => Children.Count == 0;
    
    /// <summary>
    /// True if this is a branch node (has children).
    /// </summary>
    public bool IsBranch => Children.Count > 0;
    
    /// <summary>
    /// Icon for the node based on type.
    /// </summary>
    public string Icon => IsLeaf ? GetValueIcon() : (IsExpanded ? "📂" : "📁");
    
    /// <summary>
    /// Special editor type based on key pattern.
    /// </summary>
    public SpecialEditorType EditorType => DetermineEditorType();
    
    /// <summary>
    /// For resolution editor - width component (decimal for NumericUpDown).
    /// </summary>
    public decimal ResolutionWidth
    {
        get => ParseResolutionWidth(Value);
        set => Value = $"{(int)value}x{(int)ResolutionHeight}";
    }
    
    /// <summary>
    /// For resolution editor - height component (decimal for NumericUpDown).
    /// </summary>
    public decimal ResolutionHeight
    {
        get => ParseResolutionHeight(Value);
        set => Value = $"{(int)ResolutionWidth}x{(int)value}";
    }
    
    public JsonTreeNode(GlobalSettingsEditorViewModel owner)
    {
        _owner = owner;
    }
    
    partial void OnValueChanged(string value)
    {
        Validate();
        IsModified = true;
        _owner.MarkDirty();
        OnPropertyChanged(nameof(ResolutionWidth));
        OnPropertyChanged(nameof(ResolutionHeight));
    }
    
    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(Icon));
    }
    
    private string GetValueIcon()
    {
        return ValueKind switch
        {
            JsonValueKind.True or JsonValueKind.False => "☑",
            JsonValueKind.Number => "#",
            JsonValueKind.String when EditorType == SpecialEditorType.Resolution => "📐",
            JsonValueKind.String => "𝐓",
            JsonValueKind.Null => "∅",
            _ => "•"
        };
    }
    
    private SpecialEditorType DetermineEditorType()
    {
        if (FullPath.EndsWith(".resolution", StringComparison.OrdinalIgnoreCase))
            return SpecialEditorType.Resolution;
        
        return SpecialEditorType.Default;
    }
    
    private void Validate()
    {
        ValidationError = null;
        
        switch (EditorType)
        {
            case SpecialEditorType.Resolution:
                if (!Regex.IsMatch(Value, @"^\d+x\d+$"))
                    ValidationError = "Must be in format WxH (e.g., 1920x1080)";
                break;
        }
        
        // Type-specific validation
        switch (ValueKind)
        {
            case JsonValueKind.Number:
                if (!double.TryParse(Value, out _))
                    ValidationError = "Must be a valid number";
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                if (!bool.TryParse(Value, out _) && Value != "true" && Value != "false")
                    ValidationError = "Must be true or false";
                break;
        }
    }
    
    private static decimal ParseResolutionWidth(string value)
    {
        var match = Regex.Match(value, @"^(\d+)x\d+$");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }
    
    private static decimal ParseResolutionHeight(string value)
    {
        var match = Regex.Match(value, @"^\d+x(\d+)$");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }
}

/// <summary>
/// Special editor types for certain key patterns.
/// </summary>
public enum SpecialEditorType
{
    Default,
    Resolution
}

/// <summary>
/// JSON value kinds for leaf nodes.
/// </summary>
public enum JsonValueKind
{
    String,
    Number,
    True,
    False,
    Null,
    Object,
    Array
}

/// <summary>
/// View model for the Global Settings tree editor.
/// </summary>
public partial class GlobalSettingsEditorViewModel : ObservableObject
{
    private JsonObject? _originalJson;
    
    [ObservableProperty]
    private string _title = "Global Settings";
    
    [ObservableProperty]
    private bool _isDirty;
    
    [ObservableProperty]
    private JsonTreeNode? _selectedNode;
    
    [ObservableProperty]
    private string _searchText = string.Empty;
    
    public ObservableCollection<JsonTreeNode> RootNodes { get; } = new();
    
    /// <summary>
    /// Flat list of all leaf nodes for easy iteration.
    /// </summary>
    private readonly List<JsonTreeNode> _allLeaves = new();
    
    // Keep old collection for compatibility during transition
    public ObservableCollection<SettingItemViewModel> Settings { get; } = new();
    
    public void LoadFromJson(JsonObject globalSettings)
    {
        _originalJson = globalSettings;
        RootNodes.Clear();
        _allLeaves.Clear();
        
        // Build tree from flat key-value pairs
        foreach (var property in globalSettings)
        {
            AddNodeFromPath(property.Key, property.Value);
        }
        
        // Sort children at each level
        SortChildren(RootNodes);
        
        IsDirty = false;
    }
    
    private void AddNodeFromPath(string path, JsonNode? value)
    {
        var parts = path.Split('.');
        var currentLevel = RootNodes;
        var currentPath = "";
        
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}.{part}";
            var isLast = i == parts.Length - 1;
            
            var existing = currentLevel.FirstOrDefault(n => n.Name == part);
            
            if (existing == null)
            {
                var node = new JsonTreeNode(this)
                {
                    Name = part,
                    FullPath = currentPath
                };
                
                if (isLast)
                {
                    // This is a leaf node with a value
                    node.Value = GetValueString(value);
                    node.ValueKind = GetValueKind(value);
                    _allLeaves.Add(node);
                }
                
                currentLevel.Add(node);
                existing = node;
            }
            else if (isLast)
            {
                // Update existing node to be a leaf
                existing.Value = GetValueString(value);
                existing.ValueKind = GetValueKind(value);
                if (!_allLeaves.Contains(existing))
                    _allLeaves.Add(existing);
            }
            
            currentLevel = existing.Children;
        }
    }
    
    private static string GetValueString(JsonNode? node)
    {
        if (node == null) return "null";
        
        if (node is JsonValue jv)
        {
            // Get the raw value without quotes for strings
            if (jv.TryGetValue<bool>(out var boolVal))
                return boolVal.ToString().ToLower();
            if (jv.TryGetValue<int>(out var intVal))
                return intVal.ToString();
            if (jv.TryGetValue<double>(out var doubleVal))
                return doubleVal.ToString();
            if (jv.TryGetValue<string>(out var strVal))
                return strVal;
        }
        
        return node.ToJsonString();
    }
    
    private static JsonValueKind GetValueKind(JsonNode? node)
    {
        if (node == null) return JsonValueKind.Null;
        
        if (node is JsonValue jv)
        {
            if (jv.TryGetValue<bool>(out var b))
                return b ? JsonValueKind.True : JsonValueKind.False;
            if (jv.TryGetValue<int>(out _) || jv.TryGetValue<double>(out _))
                return JsonValueKind.Number;
            return JsonValueKind.String;
        }
        
        if (node is JsonArray) return JsonValueKind.Array;
        if (node is JsonObject) return JsonValueKind.Object;
        
        return JsonValueKind.String;
    }
    
    private void SortChildren(ObservableCollection<JsonTreeNode> nodes)
    {
        // Sort: branches first, then alphabetically
        var sorted = nodes
            .OrderByDescending(n => n.IsBranch)
            .ThenBy(n => n.Name)
            .ToList();
        
        nodes.Clear();
        foreach (var node in sorted)
        {
            nodes.Add(node);
            if (node.Children.Count > 0)
                SortChildren(node.Children);
        }
    }
    
    public void MarkDirty()
    {
        IsDirty = true;
    }
    
    partial void OnSearchTextChanged(string value)
    {
        // Expand nodes that match search, collapse others
        if (string.IsNullOrWhiteSpace(value))
        {
            // Reset to default expansion
            SetExpansion(RootNodes, true);
        }
        else
        {
            ExpandMatching(RootNodes, value.ToLowerInvariant());
        }
    }
    
    private void SetExpansion(ObservableCollection<JsonTreeNode> nodes, bool expanded)
    {
        foreach (var node in nodes)
        {
            node.IsExpanded = expanded;
            SetExpansion(node.Children, expanded);
        }
    }
    
    private bool ExpandMatching(ObservableCollection<JsonTreeNode> nodes, string search)
    {
        bool anyMatch = false;
        
        foreach (var node in nodes)
        {
            bool selfMatches = node.Name.ToLowerInvariant().Contains(search) ||
                               node.Value.ToLowerInvariant().Contains(search);
            bool childMatches = ExpandMatching(node.Children, search);
            
            node.IsExpanded = selfMatches || childMatches;
            anyMatch = anyMatch || selfMatches || childMatches;
        }
        
        return anyMatch;
    }
    
    [RelayCommand]
    private void Save()
    {
        if (_originalJson == null) return;
        
        // Update the original JSON with modified values
        foreach (var leaf in _allLeaves)
        {
            JsonNode? newValue = leaf.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number when int.TryParse(leaf.Value, out var i) => i,
                JsonValueKind.Number when double.TryParse(leaf.Value, out var d) => d,
                JsonValueKind.Null => null,
                _ => leaf.Value
            };
            
            _originalJson[leaf.FullPath] = newValue;
        }
        
        // Reset modified flags
        foreach (var leaf in _allLeaves)
            leaf.IsModified = false;
        
        IsDirty = false;
    }
    
    [RelayCommand]
    private void AddSetting()
    {
        // Add a new setting with a placeholder path
        var newPath = "new.setting";
        var counter = 1;
        while (_allLeaves.Any(l => l.FullPath == newPath))
        {
            newPath = $"new.setting{counter++}";
        }
        
        AddNodeFromPath(newPath, "");
        SortChildren(RootNodes);
        
        // Find and select the new node
        var newNode = _allLeaves.FirstOrDefault(l => l.FullPath == newPath);
        if (newNode != null)
        {
            SelectedNode = newNode;
            newNode.IsModified = true;
        }
        
        IsDirty = true;
    }
    
    [RelayCommand]
    private void RemoveSetting(JsonTreeNode? node)
    {
        if (node == null || !node.IsLeaf) return;
        
        // Remove from _allLeaves
        _allLeaves.Remove(node);
        
        // Remove from tree
        RemoveNodeFromTree(RootNodes, node);
        
        // Remove from original JSON
        _originalJson?.Remove(node.FullPath);
        
        IsDirty = true;
    }
    
    private bool RemoveNodeFromTree(ObservableCollection<JsonTreeNode> nodes, JsonTreeNode target)
    {
        if (nodes.Remove(target))
            return true;
        
        foreach (var node in nodes)
        {
            if (RemoveNodeFromTree(node.Children, target))
            {
                // Clean up empty branches
                if (node.Children.Count == 0 && node.IsLeaf == false)
                {
                    nodes.Remove(node);
                }
                return true;
            }
        }
        
        return false;
    }
    
    [RelayCommand]
    private void ExpandAll()
    {
        SetExpansion(RootNodes, true);
    }
    
    [RelayCommand]
    private void CollapseAll()
    {
        SetExpansion(RootNodes, false);
    }
    
    [RelayCommand]
    private void ToggleBoolValue(JsonTreeNode? node)
    {
        if (node == null) return;
        
        if (node.ValueKind == JsonValueKind.True)
        {
            node.Value = "false";
            node.ValueKind = JsonValueKind.False;
        }
        else if (node.ValueKind == JsonValueKind.False)
        {
            node.Value = "true";
            node.ValueKind = JsonValueKind.True;
        }
    }
}

// Keep for backward compatibility
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
