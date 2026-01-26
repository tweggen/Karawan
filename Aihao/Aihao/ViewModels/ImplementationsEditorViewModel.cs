using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aihao.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aihao.ViewModels;

/// <summary>
/// How an implementation is created.
/// </summary>
public enum ImplementationCreationType
{
    /// <summary>
    /// Use the interface name as the class name (value is null in JSON).
    /// </summary>
    SelfRegistering,
    
    /// <summary>
    /// Use an explicit class name via constructor.
    /// </summary>
    ExplicitClass,
    
    /// <summary>
    /// Use a static factory method.
    /// </summary>
    FactoryMethod
}

/// <summary>
/// Represents a single property to be injected into an implementation.
/// </summary>
public partial class ImplementationPropertyViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _value = string.Empty;
    
    [ObservableProperty]
    private ImplementationPropertyType _propertyType = ImplementationPropertyType.String;
    
    /// <summary>
    /// For Dictionary properties, the key-value pairs.
    /// </summary>
    public ObservableCollection<KeyValuePair<string, string>> DictionaryEntries { get; } = new();
    
    /// <summary>
    /// Event fired when this property requests removal.
    /// </summary>
    public event EventHandler? RemoveRequested;
    
    /// <summary>
    /// Display value for the property (shows summary for dictionaries).
    /// </summary>
    public string DisplayValue => PropertyType == ImplementationPropertyType.Dictionary
        ? $"{{ {DictionaryEntries.Count} entries }}"
        : Value;
    
    /// <summary>
    /// Index for ComboBox binding.
    /// </summary>
    public int PropertyTypeIndex
    {
        get => (int)PropertyType;
        set
        {
            if (value >= 0 && value <= 2)
            {
                PropertyType = (ImplementationPropertyType)value;
            }
        }
    }
    
    /// <summary>
    /// True if this is a Dictionary type.
    /// </summary>
    public bool IsDictionary => PropertyType == ImplementationPropertyType.Dictionary;
    
    /// <summary>
    /// True if this is NOT a Dictionary type.
    /// </summary>
    public bool IsNotDictionary => PropertyType != ImplementationPropertyType.Dictionary;
    
    partial void OnPropertyTypeChanged(ImplementationPropertyType value)
    {
        OnPropertyChanged(nameof(PropertyTypeIndex));
        OnPropertyChanged(nameof(IsDictionary));
        OnPropertyChanged(nameof(IsNotDictionary));
        OnPropertyChanged(nameof(DisplayValue));
    }
    
    [RelayCommand]
    private void Remove()
    {
        RemoveRequested?.Invoke(this, EventArgs.Empty);
    }
}

public enum ImplementationPropertyType
{
    String,
    Number,
    Dictionary
}

/// <summary>
/// Represents a single implementation entry.
/// </summary>
public partial class ImplementationViewModel : ObservableObject
{
    private readonly ImplementationsEditorViewModel _owner;
    
    [ObservableProperty]
    private CSharpTypeReference _interfaceType = new();
    
    [ObservableProperty]
    private ImplementationCreationType _creationType = ImplementationCreationType.SelfRegistering;
    
    [ObservableProperty]
    private CSharpTypeReference _className = new();
    
    [ObservableProperty]
    private CSharpTypeReference _factoryMethod = new();
    
    [ObservableProperty]
    private string _cassettePath = string.Empty;
    
    [ObservableProperty]
    private bool _hasConfig;
    
    [ObservableProperty]
    private string _configJson = string.Empty;
    
    [ObservableProperty]
    private bool _isExpanded;
    
    [ObservableProperty]
    private bool _isSelected;
    
    [ObservableProperty]
    private bool _isModified;
    
    /// <summary>
    /// The unified property editor for this implementation's properties.
    /// </summary>
    public JsonPropertyEditorViewModel PropertiesEditor { get; }
    
    // Keep old collection for backward compatibility during transition
    public ObservableCollection<ImplementationPropertyViewModel> Properties { get; } = new();
    
    /// <summary>
    /// True if there are no properties.
    /// </summary>
    public bool HasNoProperties => PropertiesEditor.RootNodes.Count == 0;
    
    /// <summary>
    /// Summary text for display in the list.
    /// </summary>
    public string Summary => GetSummary();
    
    /// <summary>
    /// Icon based on creation type.
    /// </summary>
    public string Icon => CreationType switch
    {
        ImplementationCreationType.SelfRegistering => "🔄",
        ImplementationCreationType.ExplicitClass => "📦",
        ImplementationCreationType.FactoryMethod => "🏭",
        _ => "❓"
    };
    
    /// <summary>
    /// Index for ComboBox binding.
    /// </summary>
    public int CreationTypeIndex
    {
        get => (int)CreationType;
        set
        {
            if (value >= 0 && value <= 2)
            {
                CreationType = (ImplementationCreationType)value;
            }
        }
    }
    
    /// <summary>
    /// True if creation type is ExplicitClass.
    /// </summary>
    public bool IsExplicitClass => CreationType == ImplementationCreationType.ExplicitClass;
    
    /// <summary>
    /// True if creation type is FactoryMethod.
    /// </summary>
    public bool IsFactoryMethod => CreationType == ImplementationCreationType.FactoryMethod;
    
    public ImplementationViewModel(ImplementationsEditorViewModel owner)
    {
        _owner = owner;
        InterfaceType.PropertyChanged += (_, _) => OnModified();
        ClassName.PropertyChanged += (_, _) => OnModified();
        FactoryMethod.PropertyChanged += (_, _) => OnModified();
        
        // Initialize the unified property editor
        PropertiesEditor = new JsonPropertyEditorViewModel
        {
            Title = "Properties",
            AllowTypeChange = true,
            AllowAddRemove = true
        };
        PropertiesEditor.Modified += (_, _) => OnModified();
        PropertiesEditor.RootNodes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoProperties));
    }
    
    /// <summary>
    /// Load properties from a JsonObject.
    /// </summary>
    public void LoadProperties(JsonObject? propsObj)
    {
        if (propsObj != null)
        {
            PropertiesEditor.LoadFromJson(propsObj);
        }
        else
        {
            PropertiesEditor.RootNodes.Clear();
        }
        PropertiesEditor.ClearModifiedFlags();
    }
    
    [RelayCommand]
    private void AddProperty()
    {
        PropertiesEditor.AddPropertyCommand.Execute(null);
    }
    
    partial void OnCreationTypeChanged(ImplementationCreationType value)
    {
        OnModified();
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(CreationTypeIndex));
        OnPropertyChanged(nameof(IsExplicitClass));
        OnPropertyChanged(nameof(IsFactoryMethod));
    }
    
    partial void OnCassettePathChanged(string value) => OnModified();
    partial void OnHasConfigChanged(bool value) => OnModified();
    partial void OnConfigJsonChanged(string value) => OnModified();
    
    private void OnModified()
    {
        IsModified = true;
        _owner.MarkDirty();
        OnPropertyChanged(nameof(Summary));
    }
    
    private string GetSummary()
    {
        return CreationType switch
        {
            ImplementationCreationType.SelfRegistering => "(self)",
            ImplementationCreationType.ExplicitClass => ClassName.ShortName,
            ImplementationCreationType.FactoryMethod => FactoryMethod.ShortName,
            _ => "?"
        };
    }
    
    /// <summary>
    /// Convert this view model back to a JSON node for the value part.
    /// </summary>
    public JsonNode? ToJsonValue()
    {
        // Self-registering with no extras = null
        if (CreationType == ImplementationCreationType.SelfRegistering &&
            PropertiesEditor.RootNodes.Count == 0 &&
            !HasConfig &&
            string.IsNullOrEmpty(CassettePath))
        {
            return null;
        }
        
        var obj = new JsonObject();
        
        // Creation type
        switch (CreationType)
        {
            case ImplementationCreationType.ExplicitClass:
                obj["className"] = ClassName.FullName;
                break;
            case ImplementationCreationType.FactoryMethod:
                obj["implementation"] = FactoryMethod.FullName;
                break;
        }
        
        // Cassette path
        if (!string.IsNullOrEmpty(CassettePath))
        {
            obj["cassettePath"] = CassettePath;
        }
        
        // Properties from the unified editor
        if (PropertiesEditor.RootNodes.Count > 0)
        {
            obj["properties"] = PropertiesEditor.ToJsonObject();
        }
        
        // Config
        if (HasConfig && !string.IsNullOrWhiteSpace(ConfigJson))
        {
            try
            {
                var configNode = JsonNode.Parse(ConfigJson);
                obj["config"] = configNode;
            }
            catch
            {
                // Invalid JSON, skip config
            }
        }
        
        return obj;
    }
}

/// <summary>
/// View model for the Implementations editor.
/// Edits the /implementations section of the Mix configuration.
/// </summary>
public partial class ImplementationsEditorViewModel : ObservableObject
{
    private JsonObject? _originalJson;
    
    [ObservableProperty]
    private string _title = "Implementations";
    
    [ObservableProperty]
    private bool _isDirty;
    
    [ObservableProperty]
    private ImplementationViewModel? _selectedImplementation;
    
    /// <summary>
    /// True if an implementation is selected.
    /// </summary>
    public bool HasSelectedImplementation => SelectedImplementation != null;
    
    /// <summary>
    /// True if no implementation is selected.
    /// </summary>
    public bool HasNoSelectedImplementation => SelectedImplementation == null;
    
    partial void OnSelectedImplementationChanged(ImplementationViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedImplementation));
        OnPropertyChanged(nameof(HasNoSelectedImplementation));
    }
    
    [ObservableProperty]
    private string _searchText = string.Empty;
    
    [ObservableProperty]
    private string _filterNamespace = string.Empty;
    
    public ObservableCollection<ImplementationViewModel> Implementations { get; } = new();
    
    /// <summary>
    /// Distinct namespaces from all implementations for filtering.
    /// </summary>
    public ObservableCollection<string> AvailableNamespaces { get; } = new();
    
    /// <summary>
    /// Filtered view of implementations based on search and namespace filter.
    /// </summary>
    public IEnumerable<ImplementationViewModel> FilteredImplementations
    {
        get
        {
            var query = Implementations.AsEnumerable();
            
            if (!string.IsNullOrWhiteSpace(FilterNamespace))
            {
                query = query.Where(i => 
                    i.InterfaceType.Namespace.StartsWith(FilterNamespace, StringComparison.OrdinalIgnoreCase));
            }
            
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.ToLowerInvariant();
                query = query.Where(i =>
                    i.InterfaceType.FullName.ToLowerInvariant().Contains(search) ||
                    i.ClassName.FullName.ToLowerInvariant().Contains(search) ||
                    i.FactoryMethod.FullName.ToLowerInvariant().Contains(search));
            }
            
            return query.OrderBy(i => i.InterfaceType.FullName);
        }
    }
    
    public void LoadFromJson(JsonObject implementationsNode)
    {
        _originalJson = implementationsNode;
        Implementations.Clear();
        AvailableNamespaces.Clear();
        
        var namespaces = new HashSet<string>();
        
        foreach (var property in implementationsNode)
        {
            // Skip internal keys
            if (property.Key.StartsWith("__"))
                continue;
                
            var impl = new ImplementationViewModel(this)
            {
                InterfaceType = CSharpTypeReference.FromType(property.Key)
            };
            
            // Track namespace
            var ns = impl.InterfaceType.Namespace;
            if (!string.IsNullOrEmpty(ns))
            {
                // Add all levels of namespace
                var parts = ns.Split('.');
                var current = "";
                foreach (var part in parts)
                {
                    current = string.IsNullOrEmpty(current) ? part : $"{current}.{part}";
                    namespaces.Add(current);
                }
            }
            
            ParseImplementationValue(impl, property.Value);
            Implementations.Add(impl);
        }
        
        // Sort and add namespaces
        foreach (var ns in namespaces.OrderBy(n => n))
        {
            AvailableNamespaces.Add(ns);
        }
        
        IsDirty = false;
        OnPropertyChanged(nameof(FilteredImplementations));
    }
    
    private void ParseImplementationValue(ImplementationViewModel impl, JsonNode? value)
    {
        if (value == null)
        {
            impl.CreationType = ImplementationCreationType.SelfRegistering;
            return;
        }
        
        if (value is not JsonObject obj)
        {
            impl.CreationType = ImplementationCreationType.SelfRegistering;
            return;
        }
        
        // Determine creation type
        if (obj.TryGetPropertyValue("implementation", out var implNode) &&
            implNode is JsonValue implVal &&
            implVal.TryGetValue<string>(out var implStr) &&
            !string.IsNullOrWhiteSpace(implStr))
        {
            impl.CreationType = ImplementationCreationType.FactoryMethod;
            impl.FactoryMethod = CSharpTypeReference.FromStaticMethod(implStr);
        }
        else if (obj.TryGetPropertyValue("className", out var classNode) &&
                 classNode is JsonValue classVal &&
                 classVal.TryGetValue<string>(out var classStr) &&
                 !string.IsNullOrWhiteSpace(classStr))
        {
            impl.CreationType = ImplementationCreationType.ExplicitClass;
            impl.ClassName = CSharpTypeReference.FromType(classStr);
        }
        else
        {
            impl.CreationType = ImplementationCreationType.SelfRegistering;
        }
        
        // Cassette path
        if (obj.TryGetPropertyValue("cassettePath", out var cassetteNode) &&
            cassetteNode is JsonValue cassetteVal &&
            cassetteVal.TryGetValue<string>(out var cassettePath))
        {
            impl.CassettePath = cassettePath;
        }
        
        // Properties - use the unified property editor
        if (obj.TryGetPropertyValue("properties", out var propsNode) &&
            propsNode is JsonObject propsObj)
        {
            impl.LoadProperties(propsObj);
        }
        
        // Config
        if (obj.TryGetPropertyValue("config", out var configNode) && configNode != null)
        {
            impl.HasConfig = true;
            impl.ConfigJson = configNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
        
        impl.IsModified = false;
    }
    
    public void MarkDirty()
    {
        IsDirty = true;
    }
    
    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredImplementations));
    }
    
    partial void OnFilterNamespaceChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredImplementations));
    }
    
    [RelayCommand]
    private void AddImplementation()
    {
        var newImpl = new ImplementationViewModel(this)
        {
            InterfaceType = new CSharpTypeReference("new.Interface", CSharpReferenceKind.Type),
            CreationType = ImplementationCreationType.SelfRegistering,
            IsModified = true
        };
        
        Implementations.Add(newImpl);
        SelectedImplementation = newImpl;
        IsDirty = true;
        OnPropertyChanged(nameof(FilteredImplementations));
    }
    
    [RelayCommand]
    private void RemoveImplementation(ImplementationViewModel? impl)
    {
        if (impl == null) return;
        
        Implementations.Remove(impl);
        if (SelectedImplementation == impl)
        {
            SelectedImplementation = null;
        }
        IsDirty = true;
        OnPropertyChanged(nameof(FilteredImplementations));
    }
    
    [RelayCommand]
    private void DuplicateImplementation(ImplementationViewModel? impl)
    {
        if (impl == null) return;
        
        var duplicate = new ImplementationViewModel(this)
        {
            InterfaceType = new CSharpTypeReference(impl.InterfaceType.FullName + ".Copy", CSharpReferenceKind.Type),
            CreationType = impl.CreationType,
            ClassName = new CSharpTypeReference(impl.ClassName.FullName, CSharpReferenceKind.Type),
            FactoryMethod = new CSharpTypeReference(impl.FactoryMethod.FullName, CSharpReferenceKind.StaticMethod),
            CassettePath = impl.CassettePath,
            HasConfig = impl.HasConfig,
            ConfigJson = impl.ConfigJson,
            IsModified = true
        };
        
        foreach (var prop in impl.Properties)
        {
            var propCopy = new ImplementationPropertyViewModel
            {
                Name = prop.Name,
                Value = prop.Value,
                PropertyType = prop.PropertyType
            };
            foreach (var entry in prop.DictionaryEntries)
            {
                propCopy.DictionaryEntries.Add(new KeyValuePair<string, string>(entry.Key, entry.Value));
            }
            duplicate.Properties.Add(propCopy);
        }
        
        Implementations.Add(duplicate);
        SelectedImplementation = duplicate;
        IsDirty = true;
        OnPropertyChanged(nameof(FilteredImplementations));
    }
    
    [RelayCommand]
    private void AddProperty()
    {
        if (SelectedImplementation == null) return;
        
        SelectedImplementation.Properties.Add(new ImplementationPropertyViewModel
        {
            Name = "newProperty",
            PropertyType = ImplementationPropertyType.String
        });
        SelectedImplementation.IsModified = true;
        IsDirty = true;
    }
    
    [RelayCommand]
    private void RemoveProperty(ImplementationPropertyViewModel? prop)
    {
        if (SelectedImplementation == null || prop == null) return;
        
        SelectedImplementation.Properties.Remove(prop);
        SelectedImplementation.IsModified = true;
        IsDirty = true;
    }
    
    [RelayCommand]
    private void ClearFilter()
    {
        SearchText = string.Empty;
        FilterNamespace = string.Empty;
    }
    
    [RelayCommand]
    private void Save()
    {
        if (_originalJson == null) return;
        
        // Clear and rebuild the JSON
        _originalJson.Clear();
        
        foreach (var impl in Implementations.OrderBy(i => i.InterfaceType.FullName))
        {
            _originalJson[impl.InterfaceType.FullName] = impl.ToJsonValue();
            impl.IsModified = false;
        }
        
        IsDirty = false;
    }
    
    /// <summary>
    /// Build a new JsonObject from current state (for external saving).
    /// </summary>
    public JsonObject ToJson()
    {
        var result = new JsonObject();
        
        foreach (var impl in Implementations.OrderBy(i => i.InterfaceType.FullName))
        {
            result[impl.InterfaceType.FullName] = impl.ToJsonValue();
        }
        
        return result;
    }
}
