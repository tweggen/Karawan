using System;
using System.Collections.ObjectModel;
using System.IO;
using Aihao.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aihao.ViewModels;

public partial class ProjectTreeViewModel : ObservableObject
{
    [ObservableProperty]
    private FileTreeItemViewModel? _selectedItem;
    
    [ObservableProperty]
    private string _searchText = string.Empty;
    
    public ObservableCollection<FileTreeItemViewModel> RootItems { get; } = new();
    
    public event EventHandler<FileTreeItemViewModel>? FileSelected;
    public event EventHandler<FileTreeItemViewModel>? FileDoubleClicked;
    
    public void LoadProject(AihaoProject project)
    {
        RootItems.Clear();
        
        // Create root node for the project
        var root = new FileTreeItemViewModel
        {
            Name = project.Name,
            FullPath = project.ProjectDirectory,
            IsExpanded = true,
            IsFolder = true,
            Icon = "📁"
        };
        
        // Add sections folder with existing sections
        var sectionsFolder = new FileTreeItemViewModel
        {
            Name = "Sections",
            IsFolder = true,
            IsExpanded = true,
            Icon = "📂"
        };
        
        foreach (var definition in project.GetExistingSections())
        {
            var sectionNode = new FileTreeItemViewModel
            {
                Name = definition.DisplayName,
                NodeType = definition.Id,
                JsonPath = definition.JsonPath,
                IsFolder = false,
                Icon = definition.Icon
            };
            
            sectionsFolder.Children.Add(sectionNode);
        }
        
        root.Children.Add(sectionsFolder);
        
        // Create file tree from tracked files
        var filesFolder = new FileTreeItemViewModel
        {
            Name = "Files",
            IsFolder = true,
            IsExpanded = true,
            Icon = "📂"
        };
        
        // Add root file first
        if (project.Files.TryGetValue(project.RootFilePath, out var rootFile))
        {
            var rootFileNode = new FileTreeItemViewModel
            {
                Name = Path.GetFileName(rootFile.RelativePath),
                FullPath = rootFile.AbsolutePath,
                RelativePath = rootFile.RelativePath,
                IsFolder = false,
                IsFile = true,
                Exists = rootFile.Exists,
                Icon = rootFile.Exists ? "📄" : "⚠️"
            };
            filesFolder.Children.Add(rootFileNode);
        }
        
        // Add additional files (from __include__)
        foreach (var additionalFile in project.Mix.AdditionalFiles)
        {
            var fileName = Path.GetFileName(additionalFile);
            if (fileName == project.RootFilePath) continue;
            
            project.Files.TryGetValue(fileName, out var file);
            var exists = file?.Exists ?? File.Exists(Path.Combine(project.ProjectDirectory, additionalFile));
            
            var fileNode = new FileTreeItemViewModel
            {
                Name = fileName,
                FullPath = Path.Combine(project.ProjectDirectory, additionalFile),
                RelativePath = additionalFile,
                IsFolder = false,
                IsFile = true,
                Exists = exists,
                Icon = exists ? "📄" : "⚠️"
            };
            filesFolder.Children.Add(fileNode);
        }
        
        root.Children.Add(filesFolder);
        RootItems.Add(root);
    }
    
    partial void OnSelectedItemChanged(FileTreeItemViewModel? value)
    {
        if (value != null)
        {
            FileSelected?.Invoke(this, value);
        }
    }
    
    [RelayCommand]
    private void ItemDoubleClicked(FileTreeItemViewModel? item)
    {
        if (item != null)
        {
            if (item.IsFolder && !item.IsFile)
            {
                item.IsExpanded = !item.IsExpanded;
            }
            else
            {
                FileDoubleClicked?.Invoke(this, item);
            }
        }
    }
    
    [RelayCommand]
    private void RefreshTree()
    {
        // TODO: Reload from project
    }
    
    [RelayCommand]
    private void CollapseAll()
    {
        foreach (var item in RootItems)
        {
            CollapseRecursive(item);
        }
    }
    
    private void CollapseRecursive(FileTreeItemViewModel item)
    {
        item.IsExpanded = false;
        foreach (var child in item.Children)
        {
            CollapseRecursive(child);
        }
    }
    
    [RelayCommand]
    private void ExpandAll()
    {
        foreach (var item in RootItems)
        {
            ExpandRecursive(item);
        }
    }
    
    private void ExpandRecursive(FileTreeItemViewModel item)
    {
        item.IsExpanded = true;
        foreach (var child in item.Children)
        {
            ExpandRecursive(child);
        }
    }
}

public partial class FileTreeItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _fullPath = string.Empty;
    
    [ObservableProperty]
    private string _relativePath = string.Empty;
    
    [ObservableProperty]
    private string _nodeType = string.Empty;
    
    [ObservableProperty]
    private string _jsonPath = string.Empty;
    
    [ObservableProperty]
    private string? _includeFilePath;
    
    [ObservableProperty]
    private bool _isFolder;
    
    [ObservableProperty]
    private bool _isFile;
    
    [ObservableProperty]
    private bool _isExpanded;
    
    [ObservableProperty]
    private bool _exists = true;
    
    [ObservableProperty]
    private string _icon = "📄";
    
    public ObservableCollection<FileTreeItemViewModel> Children { get; } = new();
}
