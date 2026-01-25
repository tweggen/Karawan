using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Aihao.Services;

/// <summary>
/// Service for showing file open and save dialogs.
/// Uses Avalonia's StorageProvider API.
/// </summary>
public class FileDialogService
{
    private static readonly FilePickerFileType JsonFileType = new("JSON Files")
    {
        Patterns = new[] { "*.json" },
        MimeTypes = new[] { "application/json" }
    };
    
    private static readonly FilePickerFileType AllFilesType = new("All Files")
    {
        Patterns = new[] { "*.*" }
    };
    
    /// <summary>
    /// Gets the top-level window for dialogs.
    /// </summary>
    private static Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }
    
    /// <summary>
    /// Gets the storage provider from the main window.
    /// </summary>
    private static IStorageProvider? GetStorageProvider()
    {
        return GetMainWindow()?.StorageProvider;
    }
    
    /// <summary>
    /// Show an Open File dialog for selecting a single JSON file.
    /// </summary>
    /// <param name="title">Dialog title</param>
    /// <param name="initialDirectory">Initial directory to show (optional)</param>
    /// <returns>Selected file path, or null if cancelled</returns>
    public async Task<string?> OpenJsonFileAsync(string title = "Open JSON File", string? initialDirectory = null)
    {
        var storageProvider = GetStorageProvider();
        if (storageProvider == null) return null;
        
        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[] { JsonFileType, AllFilesType }
        };
        
        if (!string.IsNullOrEmpty(initialDirectory))
        {
            var folder = await storageProvider.TryGetFolderFromPathAsync(initialDirectory);
            if (folder != null)
            {
                options.SuggestedStartLocation = folder;
            }
        }
        
        var result = await storageProvider.OpenFilePickerAsync(options);
        
        if (result.Count > 0)
        {
            return result[0].Path.LocalPath;
        }
        
        return null;
    }
    
    /// <summary>
    /// Show an Open File dialog for selecting multiple JSON files.
    /// </summary>
    /// <param name="title">Dialog title</param>
    /// <param name="initialDirectory">Initial directory to show (optional)</param>
    /// <returns>List of selected file paths, or empty list if cancelled</returns>
    public async Task<IReadOnlyList<string>> OpenJsonFilesAsync(string title = "Open JSON Files", string? initialDirectory = null)
    {
        var storageProvider = GetStorageProvider();
        if (storageProvider == null) return Array.Empty<string>();
        
        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = true,
            FileTypeFilter = new[] { JsonFileType, AllFilesType }
        };
        
        if (!string.IsNullOrEmpty(initialDirectory))
        {
            var folder = await storageProvider.TryGetFolderFromPathAsync(initialDirectory);
            if (folder != null)
            {
                options.SuggestedStartLocation = folder;
            }
        }
        
        var result = await storageProvider.OpenFilePickerAsync(options);
        
        var paths = new List<string>();
        foreach (var file in result)
        {
            paths.Add(file.Path.LocalPath);
        }
        
        return paths;
    }
    
    /// <summary>
    /// Show a Save File dialog for saving a JSON file.
    /// </summary>
    /// <param name="title">Dialog title</param>
    /// <param name="suggestedFileName">Suggested file name (optional)</param>
    /// <param name="initialDirectory">Initial directory to show (optional)</param>
    /// <returns>Selected file path, or null if cancelled</returns>
    public async Task<string?> SaveJsonFileAsync(
        string title = "Save JSON File", 
        string? suggestedFileName = null, 
        string? initialDirectory = null)
    {
        var storageProvider = GetStorageProvider();
        if (storageProvider == null) return null;
        
        var options = new FilePickerSaveOptions
        {
            Title = title,
            DefaultExtension = "json",
            FileTypeChoices = new[] { JsonFileType, AllFilesType },
            ShowOverwritePrompt = true
        };
        
        if (!string.IsNullOrEmpty(suggestedFileName))
        {
            options.SuggestedFileName = suggestedFileName;
        }
        
        if (!string.IsNullOrEmpty(initialDirectory))
        {
            var folder = await storageProvider.TryGetFolderFromPathAsync(initialDirectory);
            if (folder != null)
            {
                options.SuggestedStartLocation = folder;
            }
        }
        
        var result = await storageProvider.SaveFilePickerAsync(options);
        
        return result?.Path.LocalPath;
    }
    
    /// <summary>
    /// Show a folder picker dialog.
    /// </summary>
    /// <param name="title">Dialog title</param>
    /// <param name="initialDirectory">Initial directory to show (optional)</param>
    /// <returns>Selected folder path, or null if cancelled</returns>
    public async Task<string?> OpenFolderAsync(string title = "Select Folder", string? initialDirectory = null)
    {
        var storageProvider = GetStorageProvider();
        if (storageProvider == null) return null;
        
        var options = new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        };
        
        if (!string.IsNullOrEmpty(initialDirectory))
        {
            var folder = await storageProvider.TryGetFolderFromPathAsync(initialDirectory);
            if (folder != null)
            {
                options.SuggestedStartLocation = folder;
            }
        }
        
        var result = await storageProvider.OpenFolderPickerAsync(options);
        
        if (result.Count > 0)
        {
            return result[0].Path.LocalPath;
        }
        
        return null;
    }
}
