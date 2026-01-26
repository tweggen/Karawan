using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aihao.ViewModels;

/// <summary>
/// The type of value a property node holds.
/// </summary>
public enum PropertyValueType
{
    String,
    Number,
    Boolean,
    Null,
    Object,
    Array
}

/// <summary>
/// Represents a single property in a hierarchical property editor.
/// Supports primitive values, nested objects, and arrays.
/// </summary>
public partial class PropertyNode : ObservableObject
{
    private readonly Action? _onModified;
    
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _value = string.Empty;
    
    [ObservableProperty]
    private PropertyValueType _valueType = PropertyValueType.String;
    
    [ObservableProperty]
    private bool _isExpanded = true;
    
    [ObservableProperty]
    private bool _isSelected;
    
    [ObservableProperty]
    private bool _isModified;
    
    [ObservableProperty]
    private bool _isReadOnly;
    
    [ObservableProperty]
    private string? _validationError;
    
    [ObservableProperty]
    private PropertyNode? _parent;
    
    /// <summary>
    /// Child nodes for objects and arrays.
    /// </summary>
    public ObservableCollection<PropertyNode> Children { get; } = new();
    
    /// <summary>
    /// True if this node can have children (object or array).
    /// </summary>
    public bool IsContainer => ValueType == PropertyValueType.Object || ValueType == PropertyValueType.Array;
    
    /// <summary>
    /// True if this is a leaf node with an editable value.
    /// </summary>
    public bool IsLeaf => !IsContainer;
    
    /// <summary>
    /// True if this is an array element (name is numeric index).
    /// </summary>
    public bool IsArrayElement => Parent?.ValueType == PropertyValueType.Array;
    
    /// <summary>
    /// Icon based on value type.
    /// </summary>
    public string Icon => ValueType switch
    {
        PropertyValueType.Object => IsExpanded ? "📂" : "📁",
        PropertyValueType.Array => IsExpanded ? "📋" : "📄",
        PropertyValueType.Boolean => "☑",
        PropertyValueType.Number => "#",
        PropertyValueType.Null => "∅",
        PropertyValueType.String => "𝐓",
        _ => "•"
    };
    
    /// <summary>
    /// Display name (shows index for array elements).
    /// </summary>
    public string DisplayName => IsArrayElement ? $"[{Name}]" : Name;
    
    /// <summary>
    /// Summary text for containers.
    /// </summary>
    public string Summary => ValueType switch
    {
        PropertyValueType.Object => $"{{ {Children.Count} }}",
        PropertyValueType.Array => $"[ {Children.Count} ]",
        _ => ""
    };
    
    /// <summary>
    /// Full JSON path from root.
    /// </summary>
    public string FullPath
    {
        get
        {
            if (Parent == null) return Name;
            var parentPath = Parent.FullPath;
            if (Parent.ValueType == PropertyValueType.Array)
                return $"{parentPath}[{Name}]";
            return string.IsNullOrEmpty(parentPath) ? Name : $"{parentPath}.{Name}";
        }
    }
    
    /// <summary>
    /// Index for ValueType ComboBox binding.
    /// </summary>
    public int ValueTypeIndex
    {
        get => (int)ValueType;
        set
        {
            if (value >= 0 && value <= 5)
            {
                var newType = (PropertyValueType)value;
                if (newType != ValueType)
                {
                    ChangeValueType(newType);
                }
            }
        }
    }
    
    /// <summary>
    /// For boolean editing.
    /// </summary>
    public bool BoolValue
    {
        get => ValueType == PropertyValueType.Boolean && 
               bool.TryParse(Value, out var b) && b;
        set
        {
            if (ValueType == PropertyValueType.Boolean)
            {
                Value = value.ToString().ToLower();
            }
        }
    }
    
    public PropertyNode(Action? onModified = null)
    {
        _onModified = onModified;
        Children.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Summary));
            OnPropertyChanged(nameof(IsContainer));
            OnPropertyChanged(nameof(IsLeaf));
        };
    }
    
    partial void OnNameChanged(string value)
    {
        MarkModified();
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(FullPath));
    }
    
    partial void OnValueChanged(string value)
    {
        Validate();
        MarkModified();
        OnPropertyChanged(nameof(BoolValue));
    }
    
    partial void OnValueTypeChanged(PropertyValueType value)
    {
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(IsContainer));
        OnPropertyChanged(nameof(IsLeaf));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(ValueTypeIndex));
        MarkModified();
    }
    
    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(Icon));
    }
    
    private void MarkModified()
    {
        IsModified = true;
        _onModified?.Invoke();
    }
    
    private void Validate()
    {
        ValidationError = null;
        
        switch (ValueType)
        {
            case PropertyValueType.Number:
                if (!string.IsNullOrEmpty(Value) && !double.TryParse(Value, out _))
                    ValidationError = "Must be a valid number";
                break;
            case PropertyValueType.Boolean:
                if (!string.IsNullOrEmpty(Value) && Value != "true" && Value != "false")
                    ValidationError = "Must be true or false";
                break;
        }
    }
    
    private void ChangeValueType(PropertyValueType newType)
    {
        var oldType = ValueType;
        ValueType = newType;
        
        // Convert value or initialize children
        if (newType == PropertyValueType.Object || newType == PropertyValueType.Array)
        {
            // Becoming a container
            Value = string.Empty;
            if (Children.Count == 0 && oldType != PropertyValueType.Object && oldType != PropertyValueType.Array)
            {
                // Add a sample child
                if (newType == PropertyValueType.Array)
                {
                    AddChild("0", "", PropertyValueType.String);
                }
                else
                {
                    AddChild("key", "", PropertyValueType.String);
                }
            }
        }
        else
        {
            // Becoming a leaf - clear children
            Children.Clear();
            
            // Set default value
            Value = newType switch
            {
                PropertyValueType.Boolean => "false",
                PropertyValueType.Number => "0",
                PropertyValueType.Null => "null",
                _ => ""
            };
        }
    }
    
    /// <summary>
    /// Add a child node.
    /// </summary>
    public PropertyNode AddChild(string name, string value, PropertyValueType type)
    {
        var child = new PropertyNode(_onModified)
        {
            Name = name,
            Value = value,
            ValueType = type,
            Parent = this
        };
        Children.Add(child);
        return child;
    }
    
    /// <summary>
    /// Remove this node from its parent.
    /// </summary>
    [RelayCommand]
    private void Remove()
    {
        Parent?.Children.Remove(this);
        _onModified?.Invoke();
    }
    
    /// <summary>
    /// Add a new child to this container.
    /// </summary>
    [RelayCommand]
    private void AddChildNode()
    {
        if (!IsContainer) return;
        
        if (ValueType == PropertyValueType.Array)
        {
            var index = Children.Count;
            AddChild(index.ToString(), "", PropertyValueType.String);
        }
        else
        {
            var name = "newKey";
            var counter = 1;
            while (Children.Any(c => c.Name == name))
            {
                name = $"newKey{counter++}";
            }
            AddChild(name, "", PropertyValueType.String);
        }
        
        IsExpanded = true;
    }
    
    /// <summary>
    /// Convert to JSON node.
    /// </summary>
    public JsonNode? ToJsonNode()
    {
        return ValueType switch
        {
            PropertyValueType.Null => null,
            PropertyValueType.Boolean => bool.TryParse(Value, out var b) && b,
            PropertyValueType.Number when int.TryParse(Value, out var i) => i,
            PropertyValueType.Number when double.TryParse(Value, out var d) => d,
            PropertyValueType.Number => 0,
            PropertyValueType.String => Value,
            PropertyValueType.Array => BuildJsonArray(),
            PropertyValueType.Object => BuildJsonObject(),
            _ => Value
        };
    }
    
    private JsonArray BuildJsonArray()
    {
        var arr = new JsonArray();
        foreach (var child in Children.OrderBy(c => int.TryParse(c.Name, out var i) ? i : 0))
        {
            arr.Add(child.ToJsonNode());
        }
        return arr;
    }
    
    private JsonObject BuildJsonObject()
    {
        var obj = new JsonObject();
        foreach (var child in Children)
        {
            obj[child.Name] = child.ToJsonNode();
        }
        return obj;
    }
    
    /// <summary>
    /// Load from a JSON node.
    /// </summary>
    public static PropertyNode FromJsonNode(string name, JsonNode? node, Action? onModified = null, PropertyNode? parent = null)
    {
        var propNode = new PropertyNode(onModified)
        {
            Name = name,
            Parent = parent
        };
        
        if (node == null)
        {
            propNode.ValueType = PropertyValueType.Null;
            propNode.Value = "null";
        }
        else if (node is JsonValue jv)
        {
            if (jv.TryGetValue<bool>(out var boolVal))
            {
                propNode.ValueType = PropertyValueType.Boolean;
                propNode.Value = boolVal.ToString().ToLower();
            }
            else if (jv.TryGetValue<int>(out var intVal))
            {
                propNode.ValueType = PropertyValueType.Number;
                propNode.Value = intVal.ToString();
            }
            else if (jv.TryGetValue<double>(out var doubleVal))
            {
                propNode.ValueType = PropertyValueType.Number;
                propNode.Value = doubleVal.ToString();
            }
            else if (jv.TryGetValue<string>(out var strVal))
            {
                propNode.ValueType = PropertyValueType.String;
                propNode.Value = strVal;
            }
            else
            {
                propNode.ValueType = PropertyValueType.String;
                propNode.Value = node.ToJsonString();
            }
        }
        else if (node is JsonArray arr)
        {
            propNode.ValueType = PropertyValueType.Array;
            for (int i = 0; i < arr.Count; i++)
            {
                var child = FromJsonNode(i.ToString(), arr[i], onModified, propNode);
                propNode.Children.Add(child);
            }
        }
        else if (node is JsonObject obj)
        {
            propNode.ValueType = PropertyValueType.Object;
            foreach (var prop in obj)
            {
                var child = FromJsonNode(prop.Key, prop.Value, onModified, propNode);
                propNode.Children.Add(child);
            }
        }
        
        propNode.IsModified = false;
        return propNode;
    }
}

/// <summary>
/// View model for a generic property tree editor.
/// Can be embedded in other editors for editing nested property structures.
/// </summary>
public partial class PropertyTreeEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Properties";
    
    [ObservableProperty]
    private bool _isDirty;
    
    [ObservableProperty]
    private PropertyNode? _selectedNode;
    
    [ObservableProperty]
    private bool _isReadOnly;
    
    [ObservableProperty]
    private string _searchText = string.Empty;
    
    /// <summary>
    /// Root nodes of the property tree.
    /// </summary>
    public ObservableCollection<PropertyNode> RootNodes { get; } = new();
    
    /// <summary>
    /// Load properties from a JSON object.
    /// </summary>
    public void LoadFromJson(JsonObject? obj)
    {
        RootNodes.Clear();
        
        if (obj == null) return;
        
        foreach (var prop in obj)
        {
            var node = PropertyNode.FromJsonNode(prop.Key, prop.Value, MarkDirty);
            RootNodes.Add(node);
        }
        
        IsDirty = false;
    }
    
    /// <summary>
    /// Load properties from a JSON array.
    /// </summary>
    public void LoadFromJsonArray(JsonArray? arr)
    {
        RootNodes.Clear();
        
        if (arr == null) return;
        
        for (int i = 0; i < arr.Count; i++)
        {
            var node = PropertyNode.FromJsonNode(i.ToString(), arr[i], MarkDirty);
            RootNodes.Add(node);
        }
        
        IsDirty = false;
    }
    
    /// <summary>
    /// Convert back to JSON object.
    /// </summary>
    public JsonObject ToJsonObject()
    {
        var obj = new JsonObject();
        foreach (var node in RootNodes)
        {
            obj[node.Name] = node.ToJsonNode();
        }
        return obj;
    }
    
    /// <summary>
    /// Convert back to JSON array.
    /// </summary>
    public JsonArray ToJsonArray()
    {
        var arr = new JsonArray();
        foreach (var node in RootNodes.OrderBy(n => int.TryParse(n.Name, out var i) ? i : 0))
        {
            arr.Add(node.ToJsonNode());
        }
        return arr;
    }
    
    public void MarkDirty()
    {
        IsDirty = true;
    }
    
    [RelayCommand]
    private void AddRootProperty()
    {
        var name = "newProperty";
        var counter = 1;
        while (RootNodes.Any(n => n.Name == name))
        {
            name = $"newProperty{counter++}";
        }
        
        var node = new PropertyNode(MarkDirty)
        {
            Name = name,
            Value = "",
            ValueType = PropertyValueType.String,
            IsModified = true
        };
        RootNodes.Add(node);
        SelectedNode = node;
        IsDirty = true;
    }
    
    [RelayCommand]
    private void RemoveProperty(PropertyNode? node)
    {
        if (node == null) return;
        
        if (node.Parent != null)
        {
            node.Parent.Children.Remove(node);
        }
        else
        {
            RootNodes.Remove(node);
        }
        
        if (SelectedNode == node)
        {
            SelectedNode = null;
        }
        
        IsDirty = true;
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
    
    private void SetExpansion(ObservableCollection<PropertyNode> nodes, bool expanded)
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
    
    private bool ExpandMatching(ObservableCollection<PropertyNode> nodes, string search)
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
}
