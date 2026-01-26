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
/// Specialized editor types for property values.
/// </summary>
public enum SpecializedEditorType
{
    Default,
    Resolution,   // WxH format (e.g., 1920x1080)
    Vector2,      // X,Y format
    Vector3,      // X,Y,Z format
    Color,        // #RRGGBB or #RRGGBBAA
    Slider        // Numeric with slider control
}

/// <summary>
/// Represents a node in a JSON property tree.
/// Can be a leaf (string, number, bool, null) or a branch (object, array).
/// Supports specialized editors based on key patterns or value formats.
/// </summary>
public partial class PropertyNodeViewModel : ObservableObject
{
    private readonly Action? _onModified;
    
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _fullPath = string.Empty;
    
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
    /// Specialized editor type based on key pattern or value format.
    /// </summary>
    public SpecializedEditorType EditorType => DetermineEditorType();
    
    /// <summary>
    /// Icon for the node based on type.
    /// </summary>
    public string Icon
    {
        get
        {
            if (IsBranch)
            {
                return ValueKind == JsonNodeValueKind.Array 
                    ? (IsExpanded ? "📋" : "📑")
                    : (IsExpanded ? "📂" : "📁");
            }
            
            return EditorType switch
            {
                SpecializedEditorType.Resolution => "📐",
                SpecializedEditorType.Vector2 => "↔",
                SpecializedEditorType.Vector3 => "⊞",
                SpecializedEditorType.Color => "🎨",
                SpecializedEditorType.Slider => "🎚",
                _ => ValueKind switch
                {
                    JsonNodeValueKind.Boolean => "☑",
                    JsonNodeValueKind.Number => "#",
                    JsonNodeValueKind.Null => "∅",
                    _ => "𝐓"
                }
            };
        }
    }
    
    /// <summary>
    /// Summary text for collapsed branches or display.
    /// </summary>
    public string Summary
    {
        get
        {
            if (IsLeaf) return Value;
            if (ValueKind == JsonNodeValueKind.Array)
                return $"[{Children.Count} items]";
            if (ValueKind == JsonNodeValueKind.Object)
                return $"{{ {Children.Count} }}";
            return "";
        }
    }
    
    /// <summary>
    /// ComboBox index for value kind selection.
    /// </summary>
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
    
    #endregion
    
    #region Type-specific visibility properties
    
    public bool IsStringType => ValueKind == JsonNodeValueKind.String && EditorType == SpecializedEditorType.Default;
    public bool IsNumberType => ValueKind == JsonNodeValueKind.Number && EditorType != SpecializedEditorType.Slider;
    public bool IsBooleanType => ValueKind == JsonNodeValueKind.Boolean;
    public bool IsNullType => ValueKind == JsonNodeValueKind.Null;
    public bool IsResolutionType => EditorType == SpecializedEditorType.Resolution;
    public bool IsVector2Type => EditorType == SpecializedEditorType.Vector2;
    public bool IsVector3Type => EditorType == SpecializedEditorType.Vector3;
    public bool IsColorType => EditorType == SpecializedEditorType.Color;
    public bool IsSliderType => EditorType == SpecializedEditorType.Slider;
    
    #endregion
    
    #region Specialized Editor Properties
    
    /// <summary>
    /// Boolean value for checkbox binding.
    /// </summary>
    public bool BoolValue
    {
        get => Value.Equals("true", StringComparison.OrdinalIgnoreCase);
        set
        {
            Value = value ? "true" : "false";
            OnPropertyChanged();
        }
    }
    
    /// <summary>
    /// For resolution/vector - X component.
    /// </summary>
    public decimal VectorX
    {
        get => ParseVectorComponent(Value, 0);
        set => Value = ReplaceVectorComponent(Value, 0, value);
    }
    
    /// <summary>
    /// For resolution/vector - Y component.
    /// </summary>
    public decimal VectorY
    {
        get => ParseVectorComponent(Value, 1);
        set => Value = ReplaceVectorComponent(Value, 1, value);
    }
    
    /// <summary>
    /// For vector3 - Z component.
    /// </summary>
    public decimal VectorZ
    {
        get => ParseVectorComponent(Value, 2);
        set => Value = ReplaceVectorComponent(Value, 2, value);
    }
    
    /// <summary>
    /// For slider control (0-100 range typically).
    /// </summary>
    public double SliderValue
    {
        get => double.TryParse(Value, out var d) ? d : 0;
        set => Value = value.ToString("G", CultureInfo.InvariantCulture);
    }
    
    /// <summary>
    /// Color value for color picker.
    /// </summary>
    public string ColorValue
    {
        get => Value.StartsWith("#") ? Value : "#000000";
        set
        {
            if (value.StartsWith("#"))
                Value = value;
        }
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
        
        // Notify all computed properties
        OnPropertyChanged(nameof(BoolValue));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(VectorX));
        OnPropertyChanged(nameof(VectorY));
        OnPropertyChanged(nameof(VectorZ));
        OnPropertyChanged(nameof(SliderValue));
        OnPropertyChanged(nameof(ColorValue));
        OnPropertyChanged(nameof(EditorType));
        NotifyEditorTypeProperties();
    }
    
    partial void OnNameChanged(string value)
    {
        UpdateFullPath();
        MarkModified();
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(EditorType));
        NotifyEditorTypeProperties();
    }
    
    partial void OnFullPathChanged(string value)
    {
        OnPropertyChanged(nameof(EditorType));
        NotifyEditorTypeProperties();
    }
    
    partial void OnValueKindChanged(JsonNodeValueKind value)
    {
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(IsLeaf));
        OnPropertyChanged(nameof(IsBranch));
        OnPropertyChanged(nameof(ValueKindIndex));
        OnPropertyChanged(nameof(Summary));
        NotifyEditorTypeProperties();
        
        // Clear children when changing to leaf type
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
    
    private void NotifyEditorTypeProperties()
    {
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(IsStringType));
        OnPropertyChanged(nameof(IsNumberType));
        OnPropertyChanged(nameof(IsBooleanType));
        OnPropertyChanged(nameof(IsNullType));
        OnPropertyChanged(nameof(IsResolutionType));
        OnPropertyChanged(nameof(IsVector2Type));
        OnPropertyChanged(nameof(IsVector3Type));
        OnPropertyChanged(nameof(IsColorType));
        OnPropertyChanged(nameof(IsSliderType));
    }
    
    #endregion
    
    #region Editor Type Detection
    
    private SpecializedEditorType DetermineEditorType()
    {
        if (!IsLeaf) return SpecializedEditorType.Default;
        
        var lowerPath = FullPath.ToLowerInvariant();
        var lowerName = Name.ToLowerInvariant();
        
        // Check key patterns first
        if (lowerPath.EndsWith(".resolution") || lowerName == "resolution")
            return SpecializedEditorType.Resolution;
        
        if (lowerPath.EndsWith(".color") || lowerName == "color" || lowerName.EndsWith("color"))
            return SpecializedEditorType.Color;
        
        if (lowerPath.EndsWith(".position") || lowerPath.EndsWith(".scale") || 
            lowerPath.EndsWith(".rotation") || lowerPath.EndsWith(".vector3") ||
            lowerName == "position" || lowerName == "scale" || lowerName == "rotation")
            return SpecializedEditorType.Vector3;
        
        if (lowerPath.EndsWith(".size") || lowerPath.EndsWith(".offset") || 
            lowerPath.EndsWith(".vector2") || lowerName == "size" || lowerName == "offset")
            return SpecializedEditorType.Vector2;
        
        // Slider for volume/distance-like values
        if (ValueKind == JsonNodeValueKind.Number && 
            (lowerName.Contains("volume") || lowerName.Contains("opacity") || 
             lowerName.Contains("alpha") || lowerName.Contains("intensity")))
            return SpecializedEditorType.Slider;
        
        // Check value format
        if (Regex.IsMatch(Value, @"^\d+x\d+$"))
            return SpecializedEditorType.Resolution;
        
        if (Regex.IsMatch(Value, @"^-?\d+\.?\d*,-?\d+\.?\d*,-?\d+\.?\d*$"))
            return SpecializedEditorType.Vector3;
        
        if (Regex.IsMatch(Value, @"^-?\d+\.?\d*,-?\d+\.?\d*$"))
            return SpecializedEditorType.Vector2;
        
        if (Regex.IsMatch(Value, @"^#[0-9A-Fa-f]{6,8}$"))
            return SpecializedEditorType.Color;
        
        return SpecializedEditorType.Default;
    }
    
    #endregion
    
    #region Validation
    
    private void Validate()
    {
        ValidationError = null;
        
        switch (EditorType)
        {
            case SpecializedEditorType.Resolution:
                if (!Regex.IsMatch(Value, @"^\d+x\d+$"))
                    ValidationError = "Format: WxH (e.g., 1920x1080)";
                break;
            case SpecializedEditorType.Vector2:
                if (!Regex.IsMatch(Value, @"^-?\d+\.?\d*,-?\d+\.?\d*$"))
                    ValidationError = "Format: X,Y";
                break;
            case SpecializedEditorType.Vector3:
                if (!Regex.IsMatch(Value, @"^-?\d+\.?\d*,-?\d+\.?\d*,-?\d+\.?\d*$"))
                    ValidationError = "Format: X,Y,Z";
                break;
            case SpecializedEditorType.Color:
                if (!Regex.IsMatch(Value, @"^#[0-9A-Fa-f]{6,8}$"))
                    ValidationError = "Format: #RRGGBB or #RRGGBBAA";
                break;
        }
        
        // Type-specific validation
        if (ValidationError == null)
        {
            switch (ValueKind)
            {
                case JsonNodeValueKind.Number:
                    if (!double.TryParse(Value, out _))
                        ValidationError = "Must be a valid number";
                    break;
                case JsonNodeValueKind.Boolean:
                    if (Value != "true" && Value != "false")
                        ValidationError = "Must be true or false";
                    break;
            }
        }
    }
    
    #endregion
    
    #region Vector/Resolution Helpers
    
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
    
    private string ReplaceVectorComponent(string value, int index, decimal newValue)
    {
        // Handle "WxH" format (resolution)
        if (value.Contains('x') || EditorType == SpecializedEditorType.Resolution)
        {
            var parts = value.Contains('x') ? value.Split('x') : new[] { "0", "0" };
            while (parts.Length <= index)
                parts = parts.Concat(new[] { "0" }).ToArray();
            parts[index] = ((int)newValue).ToString();
            return string.Join("x", parts);
        }
        
        // Handle "X,Y" or "X,Y,Z" format
        var commaparts = value.Contains(',') ? value.Split(',') : new[] { "0", "0", "0" };
        while (commaparts.Length <= index)
            commaparts = commaparts.Concat(new[] { "0" }).ToArray();
        commaparts[index] = newValue.ToString(CultureInfo.InvariantCulture);
        return string.Join(",", commaparts.Take(EditorType == SpecializedEditorType.Vector3 ? 3 : 2));
    }
    
    #endregion
    
    #region Path Management
    
    private void UpdateFullPath()
    {
        if (Parent != null)
        {
            FullPath = IsArrayElement 
                ? $"{Parent.FullPath}[{ArrayIndex}]" 
                : string.IsNullOrEmpty(Parent.FullPath) ? Name : $"{Parent.FullPath}.{Name}";
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
    
    #endregion
    
    private void MarkModified()
    {
        IsModified = true;
        _onModified?.Invoke();
    }
    
    /// <summary>
    /// Event fired when this node requests to be removed.
    /// </summary>
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
            
            // Reindex array elements
            if (ValueKind == JsonNodeValueKind.Array)
            {
                for (int i = 0; i < Children.Count; i++)
                {
                    Children[i].ArrayIndex = i;
                    Children[i].UpdateFullPath();
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
    
    /// <summary>
    /// Convert this node and its children to a JsonNode.
    /// </summary>
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
    
    /// <summary>
    /// Create a PropertyNodeViewModel from a JsonNode.
    /// </summary>
    public static PropertyNodeViewModel FromJsonNode(
        string name, 
        JsonNode? node, 
        Action? onModified = null, 
        int arrayIndex = -1,
        PropertyNodeViewModel? parent = null,
        string parentPath = "")
    {
        var vm = new PropertyNodeViewModel(onModified)
        {
            Name = name,
            ArrayIndex = arrayIndex,
            Parent = parent
        };
        
        // Calculate full path
        if (arrayIndex >= 0)
            vm.FullPath = string.IsNullOrEmpty(parentPath) ? $"[{arrayIndex}]" : $"{parentPath}[{arrayIndex}]";
        else
            vm.FullPath = string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}.{name}";
        
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
                        {
                            vm.Children[i].ArrayIndex = i;
                            vm.Children[i].UpdateFullPath();
                        }
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
}

/// <summary>
/// ViewModel for a reusable JSON property tree editor.
/// Can be used standalone or embedded in other editors.
/// Supports specialized editors for various value types.
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
    
    /// <summary>
    /// Load from a JsonObject (for flat key-value settings like globalSettings).
    /// Keys with dots are treated as paths and create a tree structure.
    /// </summary>
    public void LoadFromFlatJson(JsonObject flatSettings)
    {
        _originalJson = flatSettings;
        RootNodes.Clear();
        
        // Build tree from flat key-value pairs (keys like "audio.volume" become nested)
        foreach (var property in flatSettings)
        {
            AddNodeFromPath(property.Key, property.Value);
        }
        
        // Sort children at each level
        SortChildren(RootNodes);
        
        IsDirty = false;
        ClearModifiedFlags();
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
                var node = new PropertyNodeViewModel(MarkDirty)
                {
                    Name = part,
                    FullPath = currentPath
                };
                node.RemoveRequested += OnRootChildRemoveRequested;
                
                if (isLast)
                {
                    SetNodeValue(node, value);
                }
                else
                {
                    // Intermediate branch node
                    node.ValueKind = JsonNodeValueKind.Object;
                }
                
                currentLevel.Add(node);
                existing = node;
            }
            else if (isLast)
            {
                SetNodeValue(existing, value);
            }
            
            currentLevel = existing.Children;
        }
    }
    
    private static void SetNodeValue(PropertyNodeViewModel node, JsonNode? value)
    {
        if (value == null)
        {
            node.ValueKind = JsonNodeValueKind.Null;
            node.Value = "null";
        }
        else if (value is JsonValue jv)
        {
            if (jv.TryGetValue<bool>(out var boolVal))
            {
                node.ValueKind = JsonNodeValueKind.Boolean;
                node.Value = boolVal.ToString().ToLower();
            }
            else if (jv.TryGetValue<int>(out var intVal))
            {
                node.ValueKind = JsonNodeValueKind.Number;
                node.Value = intVal.ToString();
            }
            else if (jv.TryGetValue<double>(out var doubleVal))
            {
                node.ValueKind = JsonNodeValueKind.Number;
                node.Value = doubleVal.ToString(CultureInfo.InvariantCulture);
            }
            else if (jv.TryGetValue<string>(out var strVal))
            {
                node.ValueKind = JsonNodeValueKind.String;
                node.Value = strVal;
            }
        }
        else if (value is JsonArray || value is JsonObject)
        {
            // For nested objects/arrays, recursively build
            var tempNode = PropertyNodeViewModel.FromJsonNode(node.Name, value, null, -1, null, 
                node.FullPath.Contains('.') ? node.FullPath.Substring(0, node.FullPath.LastIndexOf('.')) : "");
            node.ValueKind = tempNode.ValueKind;
            node.Value = tempNode.Value;
            foreach (var child in tempNode.Children)
            {
                child.Parent = node;
                node.Children.Add(child);
            }
        }
        
        node.IsModified = false;
    }
    
    /// <summary>
    /// Load from a JsonNode (for nested structures like implementation properties).
    /// </summary>
    public void LoadFromJson(JsonNode? node)
    {
        RootNodes.Clear();
        
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
    
    private void SortChildren(ObservableCollection<PropertyNodeViewModel> nodes)
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
    
    [RelayCommand]
    private void Save()
    {
        if (_originalJson != null)
        {
            // Rebuild flat JSON from tree
            _originalJson.Clear();
            SaveNodesToFlatJson(_originalJson, RootNodes, "");
        }
        
        ClearModifiedFlags();
        IsDirty = false;
    }
    
    private void SaveNodesToFlatJson(JsonObject target, IEnumerable<PropertyNodeViewModel> nodes, string prefix)
    {
        foreach (var node in nodes)
        {
            var path = string.IsNullOrEmpty(prefix) ? node.Name : $"{prefix}.{node.Name}";
            
            if (node.IsLeaf)
            {
                target[path] = node.ToJsonNode();
            }
            else
            {
                SaveNodesToFlatJson(target, node.Children, path);
            }
        }
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
    /// Convert the tree to flat JSON (dot-separated keys).
    /// </summary>
    public JsonObject ToFlatJsonObject()
    {
        var result = new JsonObject();
        SaveNodesToFlatJson(result, RootNodes, "");
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
