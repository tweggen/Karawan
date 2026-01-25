using System;
using System.Collections.Generic;
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
        
        foreach (var sectionState in project.GetExistingSections())
        {
            var sectionNode = new FileTreeItemViewModel
            {
                Name = sectionState.Definition.DisplayName,
                NodeType = sectionState.Definition.Id,
                JsonPath = sectionState.Definition.JsonPath,
                IsFolder = false,
                Icon = sectionState.Definition.Icon
            };
            
            // Show layer count if multiple layers
            if (sectionState.Layers.Count > 1)
            {
                sectionNode.Name = $"{sectionState.Definition.DisplayName} ({sectionState.Layers.Count} layers)";
            }
            
            sectionsFolder.Children.Add(sectionNode);
        }
        
        root.Children.Add(sectionsFolder);
        
        // Create file tree from included files
        var filesFolder = new FileTreeItemViewModel
        {
            Name = "Files",
            IsFolder = true,
            IsExpanded = true,
            Icon = "📂"
        };
        
        // Build tree structure from IncludedFiles
        if (project.RootFile != null)
        {
            var rootFileNode = CreateIncludedFileNode(project, project.RootFile);
            rootFileNode.IsExpanded = true;
            filesFolder.Children.Add(rootFileNode);
        }
        
        root.Children.Add(filesFolder);
        RootItems.Add(root);
    }
    
    private FileTreeItemViewModel CreateIncludedFileNode(AihaoProject project, IncludedFile file)
    {
        var node = new FileTreeItemViewModel
        {
            Name = Path.GetFileName(file.RelativePath),
            FullPath = file.AbsolutePath,
            RelativePath = file.RelativePath,
            JsonPath = file.MountPath,
            IsFolder = file.ChildPaths.Count > 0,
            IsFile = true,
            Exists = file.Exists,
            Icon = file.Exists ? "📄" : "⚠️"
        };
        
        // Add children recursively
        foreach (var childPath in file.ChildPaths)
        {
            if (project.IncludedFiles.TryGetValue(childPath, out var childFile))
            {
                node.Children.Add(CreateIncludedFileNode(project, childFile));
            }
        }
        
        return node;
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
