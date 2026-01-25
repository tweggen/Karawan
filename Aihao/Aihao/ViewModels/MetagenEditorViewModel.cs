using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aihao.ViewModels;

/// <summary>
/// Editor for metagen configuration - code generation settings
/// </summary>
public partial class MetagenEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Metagen Configuration";
    
    [ObservableProperty]
    private bool _isDirty;
    
    [ObservableProperty]
    private string _outputPath = string.Empty;
    
    [ObservableProperty]
    private string _namespace = string.Empty;
    
    [ObservableProperty]
    private bool _generatePartialClasses = true;
    
    [ObservableProperty]
    private bool _generateSerializers = true;
    
    [ObservableProperty]
    private MetagenTypeViewModel? _selectedType;
    
    public ObservableCollection<MetagenTypeViewModel> Types { get; } = new();
    public ObservableCollection<MetagenTemplateViewModel> Templates { get; } = new();
    
    public void LoadFromJson(JsonObject metagenNode)
    {
        OutputPath = metagenNode["outputPath"]?.GetValue<string>() ?? string.Empty;
        Namespace = metagenNode["namespace"]?.GetValue<string>() ?? string.Empty;
        GeneratePartialClasses = metagenNode["generatePartialClasses"]?.GetValue<bool>() ?? true;
        GenerateSerializers = metagenNode["generateSerializers"]?.GetValue<bool>() ?? true;
        
        Types.Clear();
        if (metagenNode["types"] is JsonArray typesArray)
        {
            foreach (var typeNode in typesArray)
            {
                if (typeNode is JsonObject typeObj)
                {
                    var type = new MetagenTypeViewModel
                    {
                        Name = typeObj["name"]?.GetValue<string>() ?? "Unknown",
                        BaseType = typeObj["baseType"]?.GetValue<string>() ?? string.Empty,
                        IsAbstract = typeObj["isAbstract"]?.GetValue<bool>() ?? false,
                        Description = typeObj["description"]?.GetValue<string>() ?? string.Empty
                    };
                    
                    if (typeObj["properties"] is JsonArray propsArray)
                    {
                        foreach (var propNode in propsArray)
                        {
                            if (propNode is JsonObject propObj)
                            {
                                type.Properties.Add(new MetagenPropertyViewModel
                                {
                                    Name = propObj["name"]?.GetValue<string>() ?? "Unknown",
                                    Type = propObj["type"]?.GetValue<string>() ?? "string",
                                    DefaultValue = propObj["default"]?.GetValue<string>() ?? string.Empty,
                                    IsRequired = propObj["required"]?.GetValue<bool>() ?? false
                                });
                            }
                        }
                    }
                    
                    Types.Add(type);
                }
            }
        }
        
        Templates.Clear();
        if (metagenNode["templates"] is JsonArray templatesArray)
        {
            foreach (var templateNode in templatesArray)
            {
                if (templateNode is JsonObject templateObj)
                {
                    Templates.Add(new MetagenTemplateViewModel
                    {
                        Name = templateObj["name"]?.GetValue<string>() ?? "Unknown",
                        Path = templateObj["path"]?.GetValue<string>() ?? string.Empty,
                        OutputPattern = templateObj["outputPattern"]?.GetValue<string>() ?? string.Empty
                    });
                }
            }
        }
    }
    
    [RelayCommand]
    private void AddType()
    {
        var newType = new MetagenTypeViewModel
        {
            Name = "NewType",
            IsNew = true
        };
        Types.Add(newType);
        SelectedType = newType;
        IsDirty = true;
    }
    
    [RelayCommand]
    private void RemoveType(MetagenTypeViewModel? type)
    {
        if (type != null)
        {
            Types.Remove(type);
            IsDirty = true;
        }
    }
    
    [RelayCommand]
    private void AddProperty()
    {
        if (SelectedType != null)
        {
            SelectedType.Properties.Add(new MetagenPropertyViewModel
            {
                Name = "newProperty",
                Type = "string"
            });
            IsDirty = true;
        }
    }
    
    [RelayCommand]
    private void RemoveProperty(MetagenPropertyViewModel? prop)
    {
        if (SelectedType != null && prop != null)
        {
            SelectedType.Properties.Remove(prop);
            IsDirty = true;
        }
    }
    
    [RelayCommand]
    private void GenerateCode()
    {
        // TODO: Trigger metagen code generation
    }
    
    [RelayCommand]
    private void Save()
    {
        // TODO: Save back to JSON
        IsDirty = false;
    }
}

public partial class MetagenTypeViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _baseType = string.Empty;
    
    [ObservableProperty]
    private bool _isAbstract;
    
    [ObservableProperty]
    private string _description = string.Empty;
    
    [ObservableProperty]
    private bool _isNew;
    
    public ObservableCollection<MetagenPropertyViewModel> Properties { get; } = new();
}

public partial class MetagenPropertyViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _type = "string";
    
    [ObservableProperty]
    private string _defaultValue = string.Empty;
    
    [ObservableProperty]
    private bool _isRequired;
    
    [ObservableProperty]
    private string _description = string.Empty;
}

public partial class MetagenTemplateViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _path = string.Empty;
    
    [ObservableProperty]
    private string _outputPattern = string.Empty;
}
