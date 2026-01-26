using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aihao.ViewModels;

/// <summary>
/// The kind of JSON value a property node holds.
/// </summary>
public enum JsonNodeValueKind
{
    String,
    Number,
    Boolean,
    Null,
    Object,
    Array
}

/// <summary>
/// Special editor types for property nodes.
/// Determined by key pattern or value format.
/// </summary>
public enum NodeEditorType
{
    Default,
    Resolution,   // WxH format (e.g., 1920x1080)
    Vector2,      // X,Y format
    Vector3,      // X,Y,Z format
    Color,        // #RRGGBB or #RRGGBBAA
    Slider        // Numeric with slider UI
}

/// <summary>
/// Represents a node in a JSON property tree.
/// Can be a leaf (string, number, bool, null) or a branch (object, array).
/// Supports special editors based on key patterns or value formats.
/// </summary>
public partial class PropertyNodeViewModel : ObservableObject
{
    private readonly Action? _onModified;
    
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _value = string.Empty;
    
    [ObservableProperty]
    private JsonNodeValueKind _valueKind = JsonNodeValueKind.String;
    
    [ObservableProperty]
    private bool _isExpanded = true;
    
    [ObservableProperty]
    private bool _isSelected;
    
    [ObservableProperty]
    private bool _isModified;
    
    [ObservableProperty]
    private bool _isReadOnly;
    
    [ObservableProperty]
    private int _arrayIndex = -1;
    
    [ObservableProperty]
    private PropertyNodeViewModel? _parent;
    
    [ObservableProperty]
    private string? _validationError;
    
    /// <summary>
    /// Full path from root (e.g., "graphics.resolution").
    /// Used for determining editor type by key pattern.
    /// </summary>
    [ObservableProperty]
    private string _fullPath = string.Empty;
    
    public ObservableCollection<PropertyNodeViewModel> Children { get; } = new();
    
    #region Computed Properties
    
    /// <summary>
    /// True if this is a leaf node (has an editable value).
    /// </summary>
    public bool IsLeaf => ValueKind != JsonNodeValueKind.Object && ValueKind != JsonNodeValueKind.Array;
    
    /// <summary>
    /// True if this is an object or array (has children).
    /// </summary>
    public bool IsBranch => !IsLeaf;
    
    /// <summary>
    /// True if this is an array element (has an index).
    /// </summary>
    public bool IsArrayElement => ArrayIndex >= 0;
    
    /// <summary>
    /// Display name (uses index for array elements).
    /// </summary>
    public string DisplayName => IsArrayElement ? $"[{ArrayIndex}]" : Name;
    
    /// <summary>
    /// Determines the special editor type based on key pattern or value format.
    /// </summary>
    public NodeEditorType EditorType => DetermineEditorType();
    
    /// <summary>
    /// Icon for the node based on type.
    /// </summary>
    public string Icon => GetIcon();
    
    /// <summary>
    /// Summary text for collapsed branches.
    /// </summary>
    public string Summary
    {
        get
        {
            if (IsLeaf) return Value;
            if (ValueKind == JsonNodeValueKind.Array)
                return $"[{Children.Count} items]";
            if (ValueKind == JsonNodeValueKind.Object)
                return $"{{ {Children.Count} properties }}";
            return "";
        }
    }
    
    #endregion
    
    #region Value Kind Visibility Properties
    
    public int ValueKindIndex
    {
        get => (int)ValueKind;
        set
        {
            if (value >= 0 && value <= 5)
            {
                ValueKind = (JsonNodeValueKind)value;
            }
        }
    }
    
    public bool BoolValue
    {
        get => Value.Equals("true", StringComparison.OrdinalIgnoreCase);
        set
        {
            Value = value ? "true" : "false";
            OnPropertyChanged();
        }
    }
    
    public bool IsStringType => ValueKind == JsonNodeValueKind.String;
    public bool IsNumberType => ValueKind == JsonNodeValueKind.Number;
    public bool IsBooleanType => ValueKind == JsonNodeValueKind.Boolean;
    public bool IsNullType => ValueKind == JsonNodeValueKind.Null;
    
    #endregion
    
    #region Editor Type Visibility Properties
    
    public bool IsDefaultEditor => EditorType == NodeEditorType.Default && IsLeaf;
    public bool IsResolutionEditor => EditorType == NodeEditorType.Resolution;
    public bool IsVector2Editor => EditorType == NodeEditorType.Vector2;
    public bool IsVector3Editor => EditorType == NodeEditorType.Vector3;
    public bool IsColorEditor => EditorType == NodeEditorType.Color;
    public bool IsSliderEditor => EditorType == NodeEditorType.Slider;
    
    /// <summary>
    /// Show default text editor only when no special editor applies.
    /// </summary>
    public bool ShowDefaultTextEditor => IsLeaf && EditorType == NodeEditorType.Default && 
                                          !IsBooleanType && !IsNullType;
    
    #endregion
    
    #region Special Editor Value Properties
    
    /// <summary>
    /// For resolution editor - width component.
    /// </summary>
    public decimal ResolutionWidth
    {
        get => ParseVectorComponent(Value, 0, 'x');
        set => Value = $"{(int)value}x{(int)ResolutionHeight}";
    }
    
    /// <summary>
    /// For resolution editor - height component.
    /// </summary>
    public decimal ResolutionHeight
    {
        get => ParseVectorComponent(Value, 1, 'x');
        set => Value = $"{(int)ResolutionWidth}x{(int)value}";
    }
    
    /// <summary>
    /// For vector2/vector3 editor - X component.
    /// </summary>
    public decimal VectorX
    {
        get => ParseVectorComponent(Value, 0, ',');
        set => Value = ReplaceVectorComponent(Value, 0, value);
    }
    
    /// <summary>
    /// For vector2/vector3 editor - Y component.
    /// </summary>
    public decimal VectorY
    {
        get => ParseVectorComponent(Value, 1, ',');
        set => Value = ReplaceVectorComponent(Value, 1, value);
    }
    
    /// <summary>
    /// For vector3 editor - Z component.
    /// </summary>
    public decimal VectorZ
    {
        get => ParseVectorComponent(Value, 2, ',');
        set => Value = ReplaceVectorComponent(Value, 2, value);
    }
    
    /// <summary>
    /// For slider editor - numeric value as double.
    /// </summary>
    public double SliderValue
    {
        get => double.TryParse(Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;
        set => Value = value.ToString("G", CultureInfo.InvariantCulture);
    }
    
    /// <summary>
    /// For numeric editor - value as decimal.
    /// </summary>
    public decimal NumericValue
    {
        get => decimal.TryParse(Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;
        set => Value = value.ToString("G", CultureInfo.InvariantCulture);
    }
    
    #endregion
    
    public PropertyNodeViewModel(Action? onModified = null)
    {
        _onModified = onModified;
        Children.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Summary));
            OnPropertyChanged(nameof(IsLeaf));
            OnPropertyChanged(nameof(IsBranch));
        };
    }
    
    #region Property Change Handlers
    
    partial void OnValueChanged(string value)
    {
        Validate();
        MarkModified();
        NotifyValueDependentProperties();
    }
    
    partial void OnNameChanged(string value)
    {
        UpdateFullPath();
        MarkModified();
        OnPropertyChanged(nameof(DisplayName));
        NotifyEditorTypeChanged();
    }
    
    partial void OnFullPathChanged(string value)
    {
        NotifyEditorTypeChanged();
    }
    
    partial void OnValueKindChanged(JsonNodeValueKind value)
    {
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(IsLeaf));
        OnPropertyChanged(nameof(IsBranch));
        OnPropertyChanged(nameof(ValueKindIndex));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(IsStringType));
        OnPropertyChanged(nameof(IsNumberType));
        OnPropertyChanged(nameof(IsBooleanType));
        OnPropertyChanged(nameof(IsNullType));
        NotifyEditorTypeChanged();
        
        if (IsLeaf)
        {
            Children.Clear();
        }
        
        MarkModified();
    }
    
    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(Icon));
    }
    
    private void NotifyValueDependentProperties()
    {
        OnPropertyChanged(nameof(BoolValue));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(ResolutionWidth));
        OnPropertyChanged(nameof(ResolutionHeight));
        OnPropertyChanged(nameof(VectorX));
        OnPropertyChanged(nameof(VectorY));
        OnPropertyChanged(nameof(VectorZ));
        OnPropertyChanged(nameof(SliderValue));
        OnPropertyChanged(nameof(NumericValue));
        NotifyEditorTypeChanged();
    }
    
    private void NotifyEditorTypeChanged()
    {
        OnPropertyChanged(nameof(EditorType));
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(IsDefaultEditor));
        OnPropertyChanged(nameof(IsResolutionEditor));
        OnPropertyChanged(nameof(IsVector2Editor));
        OnPropertyChanged(nameof(IsVector3Editor));
        OnPropertyChanged(nameof(IsColorEditor));
        OnPropertyChanged(nameof(IsSliderEditor));
        OnPropertyChanged(nameof(ShowDefaultTextEditor));
    }
    
    #endregion
    
    #region Helper Methods
    
    private string GetIcon()
    {
        if (IsBranch)
        {
            return ValueKind switch
            {
                JsonNodeValueKind.Object => IsExpanded ? "📂" : "📁",
                JsonNodeValueKind.Array => IsExpanded ? "📋" : "📑",
                _ => "📁"
            };
        }
        
        return EditorType switch
        {
            NodeEditorType.Resolution => "📐",
            NodeEditorType.Vector2 => "↔",
            NodeEditorType.Vector3 => "⊞",
            NodeEditorType.Color => "🎨",
            NodeEditorType.Slider => "🎚",
            _ => ValueKind switch
            {
                JsonNodeValueKind.Boolean => "☑",
                JsonNodeValueKind.Number => "#",
                JsonNodeValueKind.Null => "∅",
                _ => "𝐓"
            }
        };
    }
    
    private NodeEditorType DetermineEditorType()
    {
        if (!IsLeaf) return NodeEditorType.Default;
        
        var lowerPath = FullPath.ToLowerInvariant();
        var lowerName = Name.ToLowerInvariant();
        
        // Check key patterns first
        if (lowerPath.EndsWith(".resolution") || lowerName == "resolution")
            return NodeEditorType.Resolution;
        
        if (lowerPath.EndsWith(".color") || lowerName == "color" || lowerName.EndsWith("color"))
            return NodeEditorType.Color;
        
        if (lowerPath.EndsWith(".position") || lowerPath.EndsWith(".scale") || 
            lowerPath.EndsWith(".rotation") || lowerPath.EndsWith(".vector3") ||
            lowerName == "position" || lowerName == "scale" || lowerName == "rotation")
            return NodeEditorType.Vector3;
        
        if (lowerPath.EndsWith(".size") || lowerPath.EndsWith(".offset") || 
            lowerPath.EndsWith(".vector2") || lowerName == "size" || lowerName == "offset")
            return NodeEditorType.Vector2;
        
        // Check value format
        if (Regex.IsMatch(Value, @"^\d+x\d+$"))
            return NodeEditorType.Resolution;
        
        if (Regex.IsMatch(Value, @"^-?\d+\.?\d*,-?\d+\.?\d*,-?\d+\.?\d*$"))
            return NodeEditorType.Vector3;
        
        if (Regex.IsMatch(Value, @"^-?\d+\.?\d*,-?\d+\.?\d*$"))
            return NodeEditorType.Vector2;
        
        if (Regex.IsMatch(Value, @"^#[0-9A-Fa-f]{6,8}$"))
            return NodeEditorType.Color;
        
        // Numeric types get slider for certain property names
        if (ValueKind == JsonNodeValueKind.Number && 
            (lowerName.Contains("volume") || lowerName.Contains("opacity") || 
             lowerName.Contains("alpha") || lowerName.Contains("percent")))
            return NodeEditorType.Slider;
        
        return NodeEditorType.Default;
    }
    
    private void Validate()
    {
        ValidationError = null;
        
        switch (EditorType)
        {
            case NodeEditorType.Resolution:
                if (!Regex.IsMatch(Value, @"^\d+x\d+$"))
                    ValidationError = "Must be in format WxH (e.g., 1920x1080)";
                break;
            case NodeEditorType.Vector2:
                if (!Regex.IsMatch(Value, @"^-?\d+\.?\d*,-?\d+\.?\d*$"))
                    ValidationError = "Must be in format X,Y";
                break;
            case NodeEditorType.Vector3:
                if (!Regex.IsMatch(Value, @"^-?\d+\.?\d*,-?\d+\.?\d*,-?\d+\.?\d*$"))
                    ValidationError = "Must be in format X,Y,Z";
                break;
            case NodeEditorType.Color:
                if (!Regex.IsMatch(Value, @"^#[0-9A-Fa-f]{6,8}$"))
                    ValidationError = "Must be hex color (#RRGGBB or #RRGGBBAA)";
                break;
        }
        
        // Type-specific validation
        if (ValidationError == null)
        {
            switch (ValueKind)
            {
                case JsonNodeValueKind.Number:
                    if (!double.TryParse(Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                        ValidationError = "Must be a valid number";
                    break;
                case JsonNodeValueKind.Boolean:
                    if (Value != "true" && Value != "false")
                        ValidationError = "Must be true or false";
                    break;
            }
        }
    }
    
    private void UpdateFullPath()
    {
        if (Parent != null)
        {
            FullPath = string.IsNullOrEmpty(Parent.FullPath) 
                ? Name 
                : $"{Parent.FullPath}.{Name}";
        }
        else
        {
            FullPath = Name;
        }
        
        // Update children paths
        foreach (var child in Children)
        {
            child.UpdateFullPath();
        }
    }
    
    private static decimal ParseVectorComponent(string value, int index, char separator)
    {
        var parts = value.Split(separator);
        if (index < parts.Length && decimal.TryParse(parts[index], NumberStyles.Any, 
            CultureInfo.InvariantCulture, out var v))
            return v;
        return 0;
    }
    
    private static string ReplaceVectorComponent(string value, int index, decimal newValue)
    {
        var separator = value.Contains('x') ? 'x' : ',';
        var parts = value.Split(separator);
        if (index < parts.Length)
        {
            parts[index] = separator == 'x' 
                ? ((int)newValue).ToString() 
                : newValue.ToString(CultureInfo.InvariantCulture);
            return string.Join(separator.ToString(), parts);
        }
        return value;
    }
    
    private void MarkModified()
    {
        IsModified = true;
        _onModified?.Invoke();
    }
    
    #endregion
    
    #region Events and Commands
    
    public event EventHandler? RemoveRequested;
    
    [RelayCommand]
    private void Remove()
    {
        RemoveRequested?.Invoke(this, EventArgs.Empty);
    }
    
    [RelayCommand]
    private void AddChild()
    {
        if (ValueKind == JsonNodeValueKind.Object)
        {
            var child = new PropertyNodeViewModel(_onModified)
            {
                Name = GetUniqueName("newProperty"),
                ValueKind = JsonNodeValueKind.String,
                Value = "",
                Parent = this
            };
            child.UpdateFullPath();
            child.RemoveRequested += OnChildRemoveRequested;
            Children.Add(child);
            MarkModified();
        }
        else if (ValueKind == JsonNodeValueKind.Array)
        {
            var child = new PropertyNodeViewModel(_onModified)
            {
                ArrayIndex = Children.Count,
                ValueKind = JsonNodeValueKind.String,
                Value = "",
                Parent = this
            };
            child.UpdateFullPath();
            child.RemoveRequested += OnChildRemoveRequested;
            Children.Add(child);
            MarkModified();
        }
    }
    
    private void OnChildRemoveRequested(object? sender, EventArgs e)
    {
        if (sender is PropertyNodeViewModel child)
        {
            Children.Remove(child);
            
            if (ValueKind == JsonNodeValueKind.Array)
            {
                for (int i = 0; i < Children.Count; i++)
                {
                    Children[i].ArrayIndex = i;
                }
            }
            
            MarkModified();
        }
    }
    
    private string GetUniqueName(string baseName)
    {
        if (!Children.Any(c => c.Name == baseName))
            return baseName;
        
        int counter = 1;
        while (Children.Any(c => c.Name == $"{baseName}{counter}"))
            counter++;
        
        return $"{baseName}{counter}";
    }
    
    #endregion
    
    #region Serialization
    
    public JsonNode? ToJsonNode()
    {
        switch (ValueKind)
        {
            case JsonNodeValueKind.Null:
                return null;
                
            case JsonNodeValueKind.Boolean:
                return Value.Equals("true", StringComparison.OrdinalIgnoreCase);
                
            case JsonNodeValueKind.Number:
                if (long.TryParse(Value, out var longVal))
                    return longVal;
                if (double.TryParse(Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var doubleVal))
                    return doubleVal;
                return 0;
                
            case JsonNodeValueKind.String:
                return Value;
                
            case JsonNodeValueKind.Array:
                var arr = new JsonArray();
                foreach (var child in Children)
                {
                    arr.Add(child.ToJsonNode());
                }
                return arr;
                
            case JsonNodeValueKind.Object:
                var obj = new JsonObject();
                foreach (var child in Children)
                {
                    obj[child.Name] = child.ToJsonNode();
                }
                return obj;
                
            default:
                return Value;
        }
    }
    
    public static PropertyNodeViewModel FromJsonNode(string name, JsonNode? node, Action? onModified = null, 
        int arrayIndex = -1, PropertyNodeViewModel? parent = null, string parentPath = "")
    {
        var vm = new PropertyNodeViewModel(onModified)
        {
            Name = name,
            ArrayIndex = arrayIndex,
            Parent = parent
        };
        
        // Build full path
        if (arrayIndex >= 0)
        {
            vm.FullPath = string.IsNullOrEmpty(parentPath) ? $"[{arrayIndex}]" : $"{parentPath}[{arrayIndex}]";
        }
        else
        {
            vm.FullPath = string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}.{name}";
        }
        
        if (node == null)
        {
            vm.ValueKind = JsonNodeValueKind.Null;
            vm.Value = "null";
        }
        else if (node is JsonValue jv)
        {
            if (jv.TryGetValue<bool>(out var boolVal))
            {
                vm.ValueKind = JsonNodeValueKind.Boolean;
                vm.Value = boolVal.ToString().ToLower();
            }
            else if (jv.TryGetValue<long>(out var longVal))
            {
                vm.ValueKind = JsonNodeValueKind.Number;
                vm.Value = longVal.ToString();
            }
            else if (jv.TryGetValue<double>(out var doubleVal))
            {
                vm.ValueKind = JsonNodeValueKind.Number;
                vm.Value = doubleVal.ToString(CultureInfo.InvariantCulture);
            }
            else if (jv.TryGetValue<string>(out var strVal))
            {
                vm.ValueKind = JsonNodeValueKind.String;
                vm.Value = strVal;
            }
            else
            {
                vm.ValueKind = JsonNodeValueKind.String;
                vm.Value = jv.ToJsonString();
            }
        }
        else if (node is JsonArray jarr)
        {
            vm.ValueKind = JsonNodeValueKind.Array;
            int idx = 0;
            foreach (var item in jarr)
            {
                var child = FromJsonNode("", item, onModified, idx++, vm, vm.FullPath);
                child.RemoveRequested += (s, e) =>
                {
                    if (s is PropertyNodeViewModel c)
                    {
                        vm.Children.Remove(c);
                        for (int i = 0; i < vm.Children.Count; i++)
                            vm.Children[i].ArrayIndex = i;
                        onModified?.Invoke();
                    }
                };
                vm.Children.Add(child);
            }
        }
        else if (node is JsonObject jobj)
        {
            vm.ValueKind = JsonNodeValueKind.Object;
            foreach (var prop in jobj)
            {
                var child = FromJsonNode(prop.Key, prop.Value, onModified, -1, vm, vm.FullPath);
                child.RemoveRequested += (s, e) =>
                {
                    if (s is PropertyNodeViewModel c)
                    {
                        vm.Children.Remove(c);
                        onModified?.Invoke();
                    }
                };
                vm.Children.Add(child);
            }
        }
        
        vm.IsModified = false;
        return vm;
    }
    
    #endregion
}

/// <summary>
/// ViewModel for a reusable JSON property tree editor.
/// Can be used standalone or embedded in other editors.
/// Supports special editors for resolution, vectors, colors, etc.
/// </summary>
public partial class JsonPropertyEditorViewModel : ObservableObject
{
    private JsonObject? _originalJson;
    
    [ObservableProperty]
    private string _title = "Properties";
    
    [ObservableProperty]
    private bool _isDirty;
    
    [ObservableProperty]
    private PropertyNodeViewModel? _selectedNode;
    
    [ObservableProperty]
    private string _searchText = string.Empty;
    
    [ObservableProperty]
    private bool _allowTypeChange = true;
    
    [ObservableProperty]
    private bool _allowAddRemove = true;
    
    [ObservableProperty]
    private bool _showToolbar = true;
    
    public ObservableCollection<PropertyNodeViewModel> RootNodes { get; } = new();
    
    /// <summary>
    /// Event fired when the tree is modified.
    /// </summary>
    public event EventHandler? Modified;
    
    public void LoadFromJson(JsonNode? node)
    {
        RootNodes.Clear();
        _originalJson = node as JsonObject;
        
        if (node is JsonObject obj)
        {
            foreach (var prop in obj)
            {
                var child = PropertyNodeViewModel.FromJsonNode(prop.Key, prop.Value, MarkDirty);
                child.RemoveRequested += OnRootChildRemoveRequested;
                RootNodes.Add(child);
            }
        }
        else if (node is JsonArray arr)
        {
            int idx = 0;
            foreach (var item in arr)
            {
                var child = PropertyNodeViewModel.FromJsonNode("", item, MarkDirty, idx++);
                child.RemoveRequested += OnRootChildRemoveRequested;
                RootNodes.Add(child);
            }
        }
        
        IsDirty = false;
    }
    
    public void LoadFromDictionary(IDictionary<string, string> dict)
    {
        RootNodes.Clear();
        
        foreach (var kvp in dict)
        {
            var node = new PropertyNodeViewModel(MarkDirty)
            {
                Name = kvp.Key,
                FullPath = kvp.Key,
                Value = kvp.Value,
                ValueKind = JsonNodeValueKind.String
            };
            node.RemoveRequested += OnRootChildRemoveRequested;
            RootNodes.Add(node);
        }
        
        IsDirty = false;
    }
    
    private void OnRootChildRemoveRequested(object? sender, EventArgs e)
    {
        if (sender is PropertyNodeViewModel node)
        {
            RootNodes.Remove(node);
            
            for (int i = 0; i < RootNodes.Count; i++)
            {
                if (RootNodes[i].IsArrayElement)
                    RootNodes[i].ArrayIndex = i;
            }
            
            MarkDirty();
        }
    }
    
    public void MarkDirty()
    {
        IsDirty = true;
        Modified?.Invoke(this, EventArgs.Empty);
    }
    
    [RelayCommand]
    private void Save()
    {
        if (_originalJson == null) return;
        
        _originalJson.Clear();
        foreach (var node in RootNodes)
        {
            _originalJson[node.Name] = node.ToJsonNode();
        }
        
        ClearModifiedFlags();
    }
    
    [RelayCommand]
    private void AddProperty()
    {
        var baseName = "newProperty";
        var name = baseName;
        int counter = 1;
        while (RootNodes.Any(n => n.Name == name))
        {
            name = $"{baseName}{counter++}";
        }
        
        var node = new PropertyNodeViewModel(MarkDirty)
        {
            Name = name,
            FullPath = name,
            Value = "",
            ValueKind = JsonNodeValueKind.String,
            IsModified = true
        };
        node.RemoveRequested += OnRootChildRemoveRequested;
        RootNodes.Add(node);
        SelectedNode = node;
        MarkDirty();
    }
    
    [RelayCommand]
    private void AddArrayItem()
    {
        var node = new PropertyNodeViewModel(MarkDirty)
        {
            ArrayIndex = RootNodes.Count,
            FullPath = $"[{RootNodes.Count}]",
            Value = "",
            ValueKind = JsonNodeValueKind.String,
            IsModified = true
        };
        node.RemoveRequested += OnRootChildRemoveRequested;
        RootNodes.Add(node);
        SelectedNode = node;
        MarkDirty();
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
    
    private void SetExpansion(IEnumerable<PropertyNodeViewModel> nodes, bool expanded)
    {
        foreach (var node in nodes)
        {
            node.IsExpanded = expanded;
            SetExpansion(node.Children, expanded);
        }
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
    
    private bool ExpandMatching(IEnumerable<PropertyNodeViewModel> nodes, string search)
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
    
    /// <summary>
    /// Convert the tree back to a JsonObject.
    /// </summary>
    public JsonObject ToJsonObject()
    {
        var result = new JsonObject();
        foreach (var node in RootNodes)
        {
            result[node.Name] = node.ToJsonNode();
        }
        return result;
    }
    
    /// <summary>
    /// Convert the tree back to a JsonArray.
    /// </summary>
    public JsonArray ToJsonArray()
    {
        var result = new JsonArray();
        foreach (var node in RootNodes)
        {
            result.Add(node.ToJsonNode());
        }
        return result;
    }
    
    /// <summary>
    /// Convert to a simple string dictionary (for flat properties).
    /// </summary>
    public Dictionary<string, string> ToStringDictionary()
    {
        var result = new Dictionary<string, string>();
        foreach (var node in RootNodes)
        {
            if (node.IsLeaf)
            {
                result[node.Name] = node.Value;
            }
        }
        return result;
    }
    
    /// <summary>
    /// Reset all modified flags.
    /// </summary>
    public void ClearModifiedFlags()
    {
        ClearModifiedFlags(RootNodes);
        IsDirty = false;
    }
    
    private void ClearModifiedFlags(IEnumerable<PropertyNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            node.IsModified = false;
            ClearModifiedFlags(node.Children);
        }
    }
}
