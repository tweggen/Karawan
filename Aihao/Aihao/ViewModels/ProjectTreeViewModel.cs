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
        
        // Add special nodes for JSON sections
        if (project.GlobalSettings != null)
        {
            root.Children.Add(new FileTreeItemViewModel
            {
                Name = "Global Settings",
                NodeType = "globalSettings",
                IsFolder = false,
                Icon = "⚙️"
            });
        }
        
        if (project.Properties != null)
        {
            root.Children.Add(new FileTreeItemViewModel
            {
                Name = "Properties",
                NodeType = "properties",
                IsFolder = false,
                Icon = "📋"
            });
        }
        
        if (project.Metagen != null)
        {
            root.Children.Add(new FileTreeItemViewModel
            {
                Name = "Metagen",
                NodeType = "metagen",
                IsFolder = false,
                Icon = "🔧"
            });
        }
        
        if (project.Resources != null)
        {
            root.Children.Add(new FileTreeItemViewModel
            {
                Name = "Resources",
                NodeType = "resources",
                IsFolder = false,
                Icon = "📦"
            });
        }
        
        // Create file tree from project files
        var filesFolder = new FileTreeItemViewModel
        {
            Name = "Files",
            IsFolder = true,
            IsExpanded = true,
            Icon = "📂"
        };
        
        // Group files by directory
        var directories = new Dictionary<string, FileTreeItemViewModel>();
        
        foreach (var file in project.Files)
        {
            var dirPath = Path.GetDirectoryName(file.RelativePath) ?? string.Empty;
            
            if (string.IsNullOrEmpty(dirPath) || dirPath == ".")
            {
                // Root level file
                filesFolder.Children.Add(CreateFileNode(file));
            }
            else
            {
                // Create directory hierarchy
                var pathParts = dirPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var currentPath = string.Empty;
                var currentParent = filesFolder;
                
                foreach (var part in pathParts)
                {
                    currentPath = Path.Combine(currentPath, part);
                    
                    if (!directories.TryGetValue(currentPath, out var dirNode))
                    {
                        dirNode = new FileTreeItemViewModel
                        {
                            Name = part,
                            FullPath = Path.Combine(project.ProjectDirectory, currentPath),
                            IsFolder = true,
                            Icon = "📂"
                        };
                        directories[currentPath] = dirNode;
                        currentParent.Children.Add(dirNode);
                    }
                    
                    currentParent = dirNode;
                }
                
                currentParent.Children.Add(CreateFileNode(file));
            }
        }
        
        root.Children.Add(filesFolder);
        RootItems.Add(root);
    }
    
    private FileTreeItemViewModel CreateFileNode(ProjectFile file)
    {
        return new FileTreeItemViewModel
        {
            Name = file.FileName,
            FullPath = file.AbsolutePath,
            IsFolder = false,
            IsFile = true,
            FileType = file.FileType,
            Exists = file.Exists,
            Icon = GetFileIcon(file.FileType, file.Extension)
        };
    }
    
    private string GetFileIcon(ProjectFileType fileType, string extension)
    {
        return fileType switch
        {
            ProjectFileType.ProjectFile => "📄",
            ProjectFileType.Source => "📝",
            ProjectFileType.Shader => "✨",
            ProjectFileType.Texture => "🖼️",
            ProjectFileType.Model => "📦",
            ProjectFileType.Audio => "🔊",
            ProjectFileType.Animation => "🎬",
            ProjectFileType.Config => "⚙️",
            _ => extension switch
            {
                "json" => "📋",
                "xml" => "📋",
                "txt" => "📝",
                "md" => "📝",
                _ => "📄"
            }
        };
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
            if (item.IsFolder)
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
    private string _nodeType = string.Empty;
    
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
    
    [ObservableProperty]
    private ProjectFileType _fileType;
    
    public ObservableCollection<FileTreeItemViewModel> Children { get; } = new();
}
