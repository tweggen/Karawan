using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Aihao.Models;

namespace Aihao.Services;

/// <summary>
/// Service for loading and saving Karawan engine projects.
/// Mirrors the include resolution logic from engine.casette.Mix.
/// </summary>
public class ProjectService
{
    private static readonly JsonDocumentOptions JsonReadOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };
    
    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true
    };
    
    /// <summary>
    /// Load a project from a root JSON file (e.g., nogame.json).
    /// Walks the include hierarchy to discover all related files.
    /// </summary>
    public async Task<AihaoProject> LoadProjectAsync(string projectPath)
    {
        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException($"Project file not found: {projectPath}");
        }
        
        var fullPath = Path.GetFullPath(projectPath);
        var projectDir = Path.GetDirectoryName(fullPath) ?? string.Empty;
        var rootFileName = Path.GetFileName(fullPath);
        
        var project = new AihaoProject
        {
            Name = Path.GetFileNameWithoutExtension(projectPath),
            ProjectDirectory = projectDir,
            RootFilePath = rootFileName
        };
        
        // Initialize all known sections
        project.InitializeSections();
        
        // Load the root file and recursively process includes
        await LoadIncludedFileAsync(project, rootFileName, "/", null);
        
        // Build section layers from discovered files
        BuildSectionLayers(project);
        
        // Extract defaults
        ExtractDefaults(project);
        
        // Discover build paths
        DiscoverBuildPaths(project);
        
        return project;
    }
    
    /// <summary>
    /// Recursively load a JSON file and process its __include__ directives.
    /// </summary>
    private async Task LoadIncludedFileAsync(
        AihaoProject project, 
        string relativePath, 
        string mountPath,
        string? parentPath)
    {
        // Avoid circular includes
        if (project.IncludedFiles.ContainsKey(relativePath))
        {
            // File already loaded - just add as child if not already
            if (parentPath != null && 
                project.IncludedFiles.TryGetValue(parentPath, out var parent) &&
                !parent.ChildPaths.Contains(relativePath))
            {
                parent.ChildPaths.Add(relativePath);
            }
            return;
        }
        
        var absolutePath = Path.Combine(project.ProjectDirectory, relativePath);
        var exists = File.Exists(absolutePath);
        
        var includedFile = new IncludedFile
        {
            RelativePath = relativePath,
            AbsolutePath = absolutePath,
            Exists = exists,
            MountPath = mountPath,
            ParentPath = parentPath
        };
        
        project.IncludedFiles[relativePath] = includedFile;
        
        // Add to parent's children list
        if (parentPath != null && project.IncludedFiles.TryGetValue(parentPath, out var parentFile))
        {
            parentFile.ChildPaths.Add(relativePath);
        }
        
        if (!exists)
        {
            return;
        }
        
        // Parse the JSON content
        try
        {
            var jsonContent = await File.ReadAllTextAsync(absolutePath);
            var jsonNode = JsonNode.Parse(jsonContent, documentOptions: JsonReadOptions);
            
            if (jsonNode is JsonObject jsonObject)
            {
                includedFile.Content = jsonObject;
                
                // Walk the tree looking for __include__ directives
                await ProcessIncludesAsync(project, relativePath, jsonObject, mountPath);
            }
        }
        catch (JsonException ex)
        {
            // Store parse error but continue
            System.Diagnostics.Debug.WriteLine($"JSON parse error in {relativePath}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Walk a JSON object tree looking for __include__ properties.
    /// This mirrors the behavior of Mix._upsertIncludes().
    /// </summary>
    private async Task ProcessIncludesAsync(
        AihaoProject project,
        string currentFilePath,
        JsonObject jsonObject,
        string currentMountPath)
    {
        var queue = new Queue<(JsonObject Obj, string Path)>();
        queue.Enqueue((jsonObject, currentMountPath));
        
        while (queue.Count > 0)
        {
            var (obj, path) = queue.Dequeue();
            
            foreach (var property in obj)
            {
                var childPath = path.EndsWith("/") 
                    ? path + property.Key 
                    : path + "/" + property.Key;
                
                if (property.Value is JsonObject childObj)
                {
                    // Check for __include__ directive
                    if (childObj.TryGetPropertyValue("__include__", out var includeNode) &&
                        includeNode is JsonValue includeValue &&
                        includeValue.TryGetValue<string>(out var includePath) &&
                        !string.IsNullOrEmpty(includePath))
                    {
                        // Recursively load the included file
                        await LoadIncludedFileAsync(project, includePath, childPath, currentFilePath);
                    }
                    else
                    {
                        // Continue walking this object
                        queue.Enqueue((childObj, childPath));
                    }
                }
                else if (property.Value is JsonArray array)
                {
                    // Walk array elements
                    for (int i = 0; i < array.Count; i++)
                    {
                        if (array[i] is JsonObject arrayObj)
                        {
                            queue.Enqueue((arrayObj, $"{childPath}/{i}"));
                        }
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Build section layers from discovered include files.
    /// Maps each known section to its contributing files.
    /// </summary>
    private void BuildSectionLayers(AihaoProject project)
    {
        foreach (var file in project.IncludedFiles.Values)
        {
            // Check if this file's mount path corresponds to a known section
            var mountPath = file.MountPath;
            
            // Extract section ID from mount path (e.g., "/globalSettings" -> "globalSettings")
            if (mountPath.StartsWith("/") && mountPath.Length > 1)
            {
                var sectionId = mountPath.Substring(1);
                
                // Handle nested paths - only take the first segment
                var slashIndex = sectionId.IndexOf('/');
                if (slashIndex > 0)
                {
                    sectionId = sectionId.Substring(0, slashIndex);
                }
                
                var section = project.GetSection(sectionId);
                if (section != null)
                {
                    // Determine priority: root file content = 0, included files = 0 (base layer)
                    // User can add overlays at higher priorities later
                    var priority = 0;
                    
                    var layer = new SectionLayer
                    {
                        Priority = priority,
                        FilePath = file.RelativePath,
                        IsActive = true,
                        IsOverlay = false,
                        DisplayName = Path.GetFileName(file.RelativePath)
                    };
                    
                    section.Layers.Add(layer);
                }
            }
        }
        
        // Also check for inline sections in the root file (no __include__)
        var rootFile = project.RootFile;
        if (rootFile?.Content != null)
        {
            foreach (var property in rootFile.Content)
            {
                var section = project.GetSection(property.Key);
                if (section == null) continue;
                
                // Check if we already have a layer for this section
                bool hasLayer = false;
                foreach (var layer in section.Layers)
                {
                    if (layer.FilePath == rootFile.RelativePath)
                    {
                        hasLayer = true;
                        break;
                    }
                }
                
                // If no layer yet and content is not just an __include__, add inline layer
                if (!hasLayer && property.Value is JsonObject obj)
                {
                    if (!obj.ContainsKey("__include__"))
                    {
                        var layer = new SectionLayer
                        {
                            Priority = 0,
                            FilePath = null, // Inline in root
                            IsActive = true,
                            IsOverlay = false,
                            DisplayName = "Inline"
                        };
                        section.Layers.Add(layer);
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Extract defaults section for loader assembly info.
    /// </summary>
    private void ExtractDefaults(AihaoProject project)
    {
        var rootFile = project.RootFile;
        if (rootFile?.Content == null)
            return;
            
        if (rootFile.Content.TryGetPropertyValue("defaults", out var defaultsNode) &&
            defaultsNode is JsonObject defaults)
        {
            if (defaults.TryGetPropertyValue("loader", out var loaderNode) &&
                loaderNode is JsonObject loader &&
                loader.TryGetPropertyValue("assembly", out var assemblyNode) &&
                assemblyNode is JsonValue assemblyValue &&
                assemblyValue.TryGetValue<string>(out var assembly))
            {
                project.DefaultLoaderAssembly = assembly;
            }
        }
    }
    
    /// <summary>
    /// Try to find game executable and solution paths.
    /// </summary>
    private void DiscoverBuildPaths(AihaoProject project)
    {
        // Look for Karawan.sln in parent directories
        var searchDir = project.ProjectDirectory;
        for (int i = 0; i < 3; i++)
        {
            var slnPath = Path.Combine(searchDir, "Karawan.sln");
            if (File.Exists(slnPath))
            {
                project.SolutionPath = slnPath;
                break;
            }
            
            var parentDir = Path.GetDirectoryName(searchDir);
            if (string.IsNullOrEmpty(parentDir) || parentDir == searchDir)
                break;
            searchDir = parentDir;
        }
        
        // Look for game executable based on project name
        if (project.SolutionPath != null)
        {
            var slnDir = Path.GetDirectoryName(project.SolutionPath) ?? string.Empty;
            var possibleExePaths = new[]
            {
                Path.Combine(slnDir, project.Name, "bin", "Debug", "net8.0", $"{project.Name}.exe"),
                Path.Combine(slnDir, project.Name, "bin", "Debug", "net9.0", $"{project.Name}.exe"),
                Path.Combine(slnDir, project.Name, "bin", "Release", "net8.0", $"{project.Name}.exe"),
                Path.Combine(slnDir, "Karawan", "bin", "Debug", "net8.0", "Karawan.exe"),
                Path.Combine(slnDir, "Karawan", "bin", "Debug", "net9.0", "Karawan.exe"),
            };
            
            foreach (var exePath in possibleExePaths)
            {
                if (File.Exists(exePath))
                {
                    project.GameExecutablePath = exePath;
                    break;
                }
            }
        }
    }
    
    /// <summary>
    /// Save a specific file in the project.
    /// </summary>
    public async Task SaveFileAsync(AihaoProject project, string relativePath)
    {
        if (!project.IncludedFiles.TryGetValue(relativePath, out var file))
        {
            throw new ArgumentException($"File not found in project: {relativePath}");
        }
        
        if (file.Content == null)
        {
            throw new InvalidOperationException($"File has no content: {relativePath}");
        }
        
        var json = file.Content.ToJsonString(JsonWriteOptions);
        await File.WriteAllTextAsync(file.AbsolutePath, json);
        file.IsDirty = false;
    }
    
    /// <summary>
    /// Save all dirty files in the project.
    /// </summary>
    public async Task SaveAllAsync(AihaoProject project)
    {
        foreach (var file in project.IncludedFiles.Values)
        {
            if (file.IsDirty && file.Content != null)
            {
                await SaveFileAsync(project, file.RelativePath);
            }
        }
    }
    
    /// <summary>
    /// Reload a specific file from disk.
    /// </summary>
    public async Task ReloadFileAsync(AihaoProject project, string relativePath)
    {
        if (!project.IncludedFiles.TryGetValue(relativePath, out var file))
        {
            throw new ArgumentException($"File not found in project: {relativePath}");
        }
        
        if (!file.Exists || !File.Exists(file.AbsolutePath))
        {
            file.Exists = false;
            file.Content = null;
            return;
        }
        
        try
        {
            var jsonContent = await File.ReadAllTextAsync(file.AbsolutePath);
            var jsonNode = JsonNode.Parse(jsonContent, documentOptions: JsonReadOptions);
            
            if (jsonNode is JsonObject jsonObject)
            {
                file.Content = jsonObject;
                file.IsDirty = false;
            }
        }
        catch (JsonException)
        {
            // Keep existing content on parse error
        }
    }
    
    /// <summary>
    /// Add an overlay file to a section.
    /// </summary>
    public async Task AddOverlayAsync(AihaoProject project, string sectionId, string filePath, int priority = 10)
    {
        var section = project.GetSection(sectionId);
        if (section == null)
        {
            throw new ArgumentException($"Unknown section: {sectionId}");
        }
        
        // Load the overlay file if not already loaded
        if (!project.IncludedFiles.ContainsKey(filePath))
        {
            await LoadIncludedFileAsync(project, filePath, section.Definition.JsonPath, null);
        }
        
        // Add as overlay layer
        section.AddOverlay(filePath, priority, Path.GetFileName(filePath));
    }
    
    /// <summary>
    /// Remove an overlay from a section.
    /// </summary>
    public void RemoveOverlay(AihaoProject project, string sectionId, string filePath)
    {
        var section = project.GetSection(sectionId);
        section?.RemoveOverlay(filePath);
    }
    
    /// <summary>
    /// Toggle the active state of a layer.
    /// </summary>
    public void SetLayerActive(AihaoProject project, string sectionId, string? filePath, bool active)
    {
        var section = project.GetSection(sectionId);
        if (section == null) return;
        
        foreach (var layer in section.Layers)
        {
            if (layer.FilePath == filePath)
            {
                layer.IsActive = active;
                break;
            }
        }
    }
}
