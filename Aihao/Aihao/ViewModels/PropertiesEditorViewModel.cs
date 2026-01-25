using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aihao.ViewModels;

public partial class PropertiesEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Properties";
    
    [ObservableProperty]
    private bool _isDirty;
    
    [ObservableProperty]
    private object? _selectedObject;
    
    [ObservableProperty]
    private string _selectedObjectName = string.Empty;
    
    public ObservableCollection<PropertyItemViewModel> Properties { get; } = new();
    
    public void LoadFromJson(JsonObject propertiesNode, string name)
    {
        SelectedObjectName = name;
        Properties.Clear();
        
        ParseJsonObject(propertiesNode, string.Empty);
    }
    
    private void ParseJsonObject(JsonObject obj, string prefix)
    {
        foreach (var property in obj)
        {
            var fullPath = string.IsNullOrEmpty(prefix) ? property.Key : $"{prefix}.{property.Key}";
            
            if (property.Value is JsonObject childObj)
            {
                // Add as expandable category
                Properties.Add(new PropertyItemViewModel
                {
                    Name = property.Key,
                    Path = fullPath,
                    IsCategory = true,
                    Depth = prefix.Split('.').Length
                });
                ParseJsonObject(childObj, fullPath);
            }
            else
            {
                Properties.Add(new PropertyItemViewModel
                {
                    Name = property.Key,
                    Path = fullPath,
                    Value = property.Value?.ToString() ?? string.Empty,
                    ValueType = GetPropertyType(property.Value),
                    Depth = prefix.Split('.').Length
                });
            }
        }
    }
    
    private PropertyType GetPropertyType(JsonNode? node)
    {
        return node switch
        {
            JsonValue v when v.TryGetValue<bool>(out _) => PropertyType.Boolean,
            JsonValue v when v.TryGetValue<int>(out _) => PropertyType.Integer,
            JsonValue v when v.TryGetValue<double>(out _) => PropertyType.Float,
            JsonArray => PropertyType.List,
            _ => PropertyType.String
        };
    }
    
    [RelayCommand]
    private void Save()
    {
        // TODO: Write back to JSON
        IsDirty = false;
    }
    
    partial void OnSelectedObjectChanged(object? value)
    {
        if (value is JsonObject jsonObj)
        {
            LoadFromJson(jsonObj, "Selected Object");
        }
    }
}

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
