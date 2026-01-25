using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aihao.ViewModels;

/// <summary>
/// Represents a node in the properties tree.
/// Can be a branch (has children) or a leaf (has editable value).
/// </summary>
public partial class PropertyTreeNode : ObservableObject
{
    private readonly PropertiesEditorViewModel _owner;
    
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _fullPath = string.Empty;
    
    [ObservableProperty]
    private string _value = string.Empty;
    
    [ObservableProperty]
    private PropertyValueKind _valueKind = PropertyValueKind.String;
    
    [ObservableProperty]
    private bool _isExpanded = true;
    
    [ObservableProperty]
    private bool _isSelected;
    
    [ObservableProperty]
    private bool _isModified;
    
    [ObservableProperty]
    private string? _validationError;
    
    public ObservableCollection<PropertyTreeNode> Children { get; } = new();
    
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
    /// Special editor type based on key pattern or value format.
    /// </summary>
    public PropertyEditorType EditorType => DetermineEditorType();
    
    #region Special Editor Properties
    
    /// <summary>
    /// For resolution/vector2 editor - X component.
    /// </summary>
    public decimal VectorX
    {
        get => ParseVectorComponent(Value, 0);
        set => Value = ReplaceVectorComponent(Value, 0, value);
    }
    
    /// <summary>
    /// For resolution/vector2/vector3 editor - Y component.
    /// </summary>
    public decimal VectorY
    {
        get => ParseVectorComponent(Value, 1);
        set => Value = ReplaceVectorComponent(Value, 1, value);
    }
    
    /// <summary>
    /// For vector3 editor - Z component.
    /// </summary>
    public decimal VectorZ
    {
        get => ParseVectorComponent(Value, 2);
        set => Value = ReplaceVectorComponent(Value, 2, value);
    }
    
    /// <summary>
    /// For NumericUpDown (uses decimal).
    /// </summary>
    public decimal NumericValue
    {
        get => decimal.TryParse(Value, out var d) ? d : 0;
        set => Value = ((double)value).ToString("G");
    }
    
    /// <summary>
    /// For Slider (uses double).
    /// </summary>
    public double SliderValue
    {
        get => double.TryParse(Value, out var d) ? d : 0;
        set => Value = value.ToString("G");
    }
    
    #endregion
    
    public PropertyTreeNode(PropertiesEditorViewModel owner)
    {
        _owner = owner;
    }
    
    partial void OnValueChanged(string value)
    {
        Validate();
        IsModified = true;
        _owner.MarkDirty();
        OnPropertyChanged(nameof(VectorX));
        OnPropertyChanged(nameof(VectorY));
        OnPropertyChanged(nameof(VectorZ));
        OnPropertyChanged(nameof(NumericValue));
        OnPropertyChanged(nameof(SliderValue));
    }
    
    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(Icon));
    }
    
    private string GetValueIcon()
    {
        return ValueKind switch
        {
            PropertyValueKind.Boolean => "☑",
            PropertyValueKind.Integer or PropertyValueKind.Float => "#",
            PropertyValueKind.String when EditorType == PropertyEditorType.Resolution => "📐",
            PropertyValueKind.String when EditorType == PropertyEditorType.Vector2 => "↔",
            PropertyValueKind.String when EditorType == PropertyEditorType.Vector3 => "⊞",
            PropertyValueKind.String when EditorType == PropertyEditorType.Color => "🎨",
            PropertyValueKind.String => "𝐓",
            PropertyValueKind.Null => "∅",
            _ => "•"
        };
    }
    
    private PropertyEditorType DetermineEditorType()
    {
        // Check key patterns first
        var lowerPath = FullPath.ToLowerInvariant();
        
        if (lowerPath.EndsWith(".resolution"))
            return PropertyEditorType.Resolution;
        
        if (lowerPath.EndsWith(".color") || lowerPath.EndsWith("color"))
            return PropertyEditorType.Color;
        
        if (lowerPath.EndsWith(".position") || lowerPath.EndsWith(".scale") || 
            lowerPath.EndsWith(".rotation") || lowerPath.EndsWith(".vector3"))
            return PropertyEditorType.Vector3;
        
        if (lowerPath.EndsWith(".size") || lowerPath.EndsWith(".offset") || 
            lowerPath.EndsWith(".vector2"))
            return PropertyEditorType.Vector2;
        
        // Check value format
        if (Regex.IsMatch(Value, @"^\d+x\d+$"))
            return PropertyEditorType.Resolution;
        
        if (Regex.IsMatch(Value, @"^-?\d+\.?\d*,-?\d+\.?\d*,-?\d+\.?\d*$"))
            return PropertyEditorType.Vector3;
        
        if (Regex.IsMatch(Value, @"^-?\d+\.?\d*,-?\d+\.?\d*$"))
            return PropertyEditorType.Vector2;
        
        if (Regex.IsMatch(Value, @"^#[0-9A-Fa-f]{6,8}$"))
            return PropertyEditorType.Color;
        
        // Numeric types get slider for volume/distance-like values
        if (ValueKind == PropertyValueKind.Float && 
            (lowerPath.Contains("volume") || lowerPath.Contains("distance") || lowerPath.Contains("max")))
            return PropertyEditorType.Slider;
        
        return PropertyEditorType.Default;
    }
    
    private void Validate()
    {
        ValidationError = null;
        
        switch (EditorType)
        {
            case PropertyEditorType.Resolution:
                if (!Regex.IsMatch(Value, @"^\d+x\d+$"))
                    ValidationError = "Must be in format WxH (e.g., 1920x1080)";
                break;
            case PropertyEditorType.Vector2:
                if (!Regex.IsMatch(Value, @"^-?\d+\.?\d*,-?\d+\.?\d*$"))
                    ValidationError = "Must be in format X,Y";
                break;
            case PropertyEditorType.Vector3:
                if (!Regex.IsMatch(Value, @"^-?\d+\.?\d*,-?\d+\.?\d*,-?\d+\.?\d*$"))
                    ValidationError = "Must be in format X,Y,Z";
                break;
            case PropertyEditorType.Color:
                if (!Regex.IsMatch(Value, @"^#[0-9A-Fa-f]{6,8}$"))
                    ValidationError = "Must be hex color (#RRGGBB or #RRGGBBAA)";
                break;
        }
        
        // Type-specific validation
        switch (ValueKind)
        {
            case PropertyValueKind.Integer:
                if (!int.TryParse(Value, out _))
                    ValidationError = "Must be a valid integer";
                break;
            case PropertyValueKind.Float:
                if (!double.TryParse(Value, out _))
                    ValidationError = "Must be a valid number";
                break;
            case PropertyValueKind.Boolean:
                if (Value != "true" && Value != "false")
                    ValidationError = "Must be true or false";
                break;
        }
    }
    
    private static decimal ParseVectorComponent(string value, int index)
    {
        // Handle "WxH" format (resolution)
        if (value.Contains('x'))
        {
            var parts = value.Split('x');
            if (index < parts.Length && decimal.TryParse(parts[index], out var v))
                return v;
            return 0;
        }
        
        // Handle "X,Y" or "X,Y,Z" format
        if (value.Contains(','))
        {
            var parts = value.Split(',');
            if (index < parts.Length && decimal.TryParse(parts[index], out var v))
                return v;
            return 0;
        }
        
        return 0;
    }
    
    private static string ReplaceVectorComponent(string value, int index, decimal newValue)
    {
        // Handle "WxH" format (resolution)
        if (value.Contains('x'))
        {
            var parts = value.Split('x');
            if (index < parts.Length)
            {
                parts[index] = ((int)newValue).ToString();
                return string.Join("x", parts);
            }
            return value;
        }
        
        // Handle "X,Y" or "X,Y,Z" format
        if (value.Contains(','))
        {
            var parts = value.Split(',');
            if (index < parts.Length)
            {
                parts[index] = newValue.ToString();
                return string.Join(",", parts);
            }
            return value;
        }
        
        return value;
    }
}

/// <summary>
/// Special editor types for properties.
/// </summary>
public enum PropertyEditorType
{
    Default,
    Resolution,
    Vector2,
    Vector3,
    Color,
    Slider
}

/// <summary>
/// Value kinds for property nodes.
/// </summary>
public enum PropertyValueKind
{
    String,
    Integer,
    Float,
    Boolean,
    Null,
    Object,
    Array
}

/// <summary>
/// View model for the Properties tree editor.
/// </summary>
public partial class PropertiesEditorViewModel : ObservableObject
{
    private JsonObject? _originalJson;
    
    [ObservableProperty]
    private string _title = "Properties";
    
    [ObservableProperty]
    private bool _isDirty;
    
    [ObservableProperty]
    private PropertyTreeNode? _selectedNode;
    
    [ObservableProperty]
    private string _searchText = string.Empty;
    
    [ObservableProperty]
    private object? _selectedObject;
    
    [ObservableProperty]
    private string _selectedObjectName = string.Empty;
    
    public ObservableCollection<PropertyTreeNode> RootNodes { get; } = new();
    
    /// <summary>
    /// Flat list of all leaf nodes for easy iteration.
    /// </summary>
    private readonly List<PropertyTreeNode> _allLeaves = new();
    
    // Keep old collection for compatibility
    public ObservableCollection<PropertyItemViewModel> Properties { get; } = new();
    
    public void LoadFromJson(JsonObject propertiesNode, string name)
    {
        _originalJson = propertiesNode;
        SelectedObjectName = name;
        RootNodes.Clear();
        _allLeaves.Clear();
        
        // Build tree from flat key-value pairs
        foreach (var property in propertiesNode)
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
                var node = new PropertyTreeNode(this)
                {
                    Name = part,
                    FullPath = currentPath
                };
                
                if (isLast)
                {
                    node.Value = GetValueString(value);
                    node.ValueKind = GetValueKind(value);
                    _allLeaves.Add(node);
                }
                
                currentLevel.Add(node);
                existing = node;
            }
            else if (isLast)
            {
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
    
    private static PropertyValueKind GetValueKind(JsonNode? node)
    {
        if (node == null) return PropertyValueKind.Null;
        
        if (node is JsonValue jv)
        {
            if (jv.TryGetValue<bool>(out _))
                return PropertyValueKind.Boolean;
            if (jv.TryGetValue<int>(out _))
                return PropertyValueKind.Integer;
            if (jv.TryGetValue<double>(out _))
                return PropertyValueKind.Float;
            return PropertyValueKind.String;
        }
        
        if (node is JsonArray) return PropertyValueKind.Array;
        if (node is JsonObject) return PropertyValueKind.Object;
        
        return PropertyValueKind.String;
    }
    
    private void SortChildren(ObservableCollection<PropertyTreeNode> nodes)
    {
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
        if (string.IsNullOrWhiteSpace(value))
        {
            SetExpansion(RootNodes, true);
        }
        else
        {
            ExpandMatching(RootNodes, value.ToLowerInvariant());
        }
    }
    
    partial void OnSelectedObjectChanged(object? value)
    {
        if (value is JsonObject jsonObj)
        {
            LoadFromJson(jsonObj, "Selected Object");
        }
    }
    
    private void SetExpansion(ObservableCollection<PropertyTreeNode> nodes, bool expanded)
    {
        foreach (var node in nodes)
        {
            node.IsExpanded = expanded;
            SetExpansion(node.Children, expanded);
        }
    }
    
    private bool ExpandMatching(ObservableCollection<PropertyTreeNode> nodes, string search)
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
        
        foreach (var leaf in _allLeaves)
        {
            JsonNode? newValue = leaf.ValueKind switch
            {
                PropertyValueKind.Boolean => leaf.Value == "true",
                PropertyValueKind.Integer when int.TryParse(leaf.Value, out var i) => i,
                PropertyValueKind.Float when double.TryParse(leaf.Value, out var d) => d,
                PropertyValueKind.Null => null,
                _ => leaf.Value
            };
            
            _originalJson[leaf.FullPath] = newValue;
        }
        
        foreach (var leaf in _allLeaves)
            leaf.IsModified = false;
        
        IsDirty = false;
    }
    
    [RelayCommand]
    private void AddProperty()
    {
        var newPath = "new.property";
        var counter = 1;
        while (_allLeaves.Any(l => l.FullPath == newPath))
        {
            newPath = $"new.property{counter++}";
        }
        
        AddNodeFromPath(newPath, "");
        SortChildren(RootNodes);
        
        var newNode = _allLeaves.FirstOrDefault(l => l.FullPath == newPath);
        if (newNode != null)
        {
            SelectedNode = newNode;
            newNode.IsModified = true;
        }
        
        IsDirty = true;
    }
    
    [RelayCommand]
    private void RemoveProperty(PropertyTreeNode? node)
    {
        if (node == null || !node.IsLeaf) return;
        
        _allLeaves.Remove(node);
        RemoveNodeFromTree(RootNodes, node);
        _originalJson?.Remove(node.FullPath);
        
        IsDirty = true;
    }
    
    private bool RemoveNodeFromTree(ObservableCollection<PropertyTreeNode> nodes, PropertyTreeNode target)
    {
        if (nodes.Remove(target))
            return true;
        
        foreach (var node in nodes.ToList())
        {
            if (RemoveNodeFromTree(node.Children, target))
            {
                if (node.Children.Count == 0 && !node.IsLeaf)
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
}

// Keep for backward compatibility
public partial class PropertyItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _path = string.Empty;
    
    [ObservableProperty]
    private string _value = string.Empty;
    
    [ObservableProperty]
    private PropertyType _valueType;
    
    [ObservableProperty]
    private bool _isCategory;
    
    [ObservableProperty]
    private bool _isExpanded = true;
    
    [ObservableProperty]
    private int _depth;
}

public enum PropertyType
{
    String,
    Integer,
    Float,
    Boolean,
    Vector2,
    Vector3,
    Color,
    List,
    Reference
}
