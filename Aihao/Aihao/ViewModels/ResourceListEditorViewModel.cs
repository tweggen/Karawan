using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aihao.ViewModels;

public partial class ResourceListEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Resources";
    
    [ObservableProperty]
    private bool _isDirty;
    
    [ObservableProperty]
    private string _searchText = string.Empty;
    
    [ObservableProperty]
    private string _selectedCategory = "All";
    
    [ObservableProperty]
    private ResourceItemViewModel? _selectedResource;
    
    public ObservableCollection<ResourceItemViewModel> Resources { get; } = new();
    public ObservableCollection<ResourceItemViewModel> FilteredResources { get; } = new();
    public ObservableCollection<string> Categories { get; } = new() { "All" };
    
    public void LoadFromJson(JsonArray resourcesArray)
    {
        Resources.Clear();
        Categories.Clear();
        Categories.Add("All");
        
        foreach (var item in resourcesArray)
        {
            if (item is JsonObject resourceObj)
            {
                var resource = new ResourceItemViewModel
                {
                    Name = resourceObj["name"]?.GetValue<string>() ?? "Unnamed",
                    Type = resourceObj["type"]?.GetValue<string>() ?? "Unknown",
                    Path = resourceObj["path"]?.GetValue<string>() ?? string.Empty,
                    Category = resourceObj["category"]?.GetValue<string>() ?? "Uncategorized"
                };
                
                Resources.Add(resource);
                
                if (!Categories.Contains(resource.Category))
                {
                    Categories.Add(resource.Category);
                }
            }
        }
        
        ApplyFilter();
    }
    
    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedCategoryChanged(string value) => ApplyFilter();
    
    private void ApplyFilter()
    {
        FilteredResources.Clear();
        
        var filtered = Resources.Where(r =>
            (SelectedCategory == "All" || r.Category == SelectedCategory) &&
            (string.IsNullOrEmpty(SearchText) || 
             r.Name.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) ||
             r.Path.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase)));
        
        foreach (var resource in filtered)
        {
            FilteredResources.Add(resource);
        }
    }
    
    [RelayCommand]
    private void AddResource()
    {
        var resource = new ResourceItemViewModel
        {
            Name = "New Resource",
            Type = "Unknown",
            Path = string.Empty,
            Category = "Uncategorized",
            IsNew = true
        };
        Resources.Add(resource);
        ApplyFilter();
        SelectedResource = resource;
        IsDirty = true;
    }
    
    [RelayCommand]
    private void RemoveResource(ResourceItemViewModel? resource)
    {
        if (resource != null)
        {
            Resources.Remove(resource);
            FilteredResources.Remove(resource);
            IsDirty = true;
        }
    }
    
    [RelayCommand]
    private void DuplicateResource(ResourceItemViewModel? resource)
    {
        if (resource != null)
        {
            var duplicate = new ResourceItemViewModel
            {
                Name = resource.Name + " (Copy)",
                Type = resource.Type,
                Path = resource.Path,
                Category = resource.Category,
                IsNew = true
            };
            Resources.Add(duplicate);
            ApplyFilter();
            SelectedResource = duplicate;
            IsDirty = true;
        }
    }
    
    [RelayCommand]
    private void OpenResourceLocation(ResourceItemViewModel? resource)
    {
        if (resource != null && !string.IsNullOrEmpty(resource.Path))
        {
            // TODO: Open file location in system explorer
        }
    }
}

public partial class ResourceItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _type = string.Empty;
    
    [ObservableProperty]
    private string _path = string.Empty;
    
    [ObservableProperty]
    private string _category = string.Empty;
    
    [ObservableProperty]
    private bool _isNew;
    
    [ObservableProperty]
    private long _fileSize;
    
    [ObservableProperty]
    private string _status = "OK";
    
    public string TypeIcon => Type switch
    {
        "Texture" => "🖼️",
        "Model" => "📦",
        "Audio" => "🔊",
        "Script" => "📜",
        "Shader" => "✨",
        "Animation" => "🎬",
        "Material" => "🎨",
        _ => "📄"
    };
}
