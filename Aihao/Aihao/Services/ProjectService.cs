using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Aihao.Models;
using engine.casette;

namespace Aihao.Services;

/// <summary>
/// Service for loading and saving Karawan engine projects.
/// Uses Mix directly for JSON merging and include resolution.
/// </summary>
public class ProjectService
{
    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true
    };
    
    /// <summary>
    /// Load a project from a root JSON file (e.g., nogame.json).
    /// Creates a Mix instance and loads the configuration tree.
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
        
        // Create the file provider for editor context
        var fileProvider = new EditorFileProvider(projectDir);
        
        // Create Mix with the editor file provider
        var mix = new Mix(fileProvider)
        {
            Directory = "" // Files are relative to projectDir, handled by EditorFileProvider
        };
        
        var project = new AihaoProject
        {
            Name = Path.GetFileNameWithoutExtension(projectPath),
            ProjectDirectory = projectDir,
            RootFilePath = rootFileName,
            Mix = mix
        };
        
        // Load the root file into Mix
        await Task.Run(() =>
        {
            using var stream = File.OpenRead(fullPath);
            mix.UpsertFragment("/", stream, priority: 0);
        });
        
        // Track the root file
        project.Files[rootFileName] = new ProjectFile
        {
            RelativePath = rootFileName,
            AbsolutePath = fullPath,
            Exists = true,
            IsDirty = false
        };
        
        // Track additional files discovered via __include__
        foreach (var additionalFile in mix.AdditionalFiles)
        {
            var relPath = Path.GetFileName(additionalFile);
            var absPath = Path.Combine(projectDir, additionalFile);
            project.Files[relPath] = new ProjectFile
            {
                RelativePath = relPath,
                AbsolutePath = absPath,
                Exists = File.Exists(absPath),
                IsDirty = false
            };
        }
        
        // Extract defaults
        ExtractDefaults(project);
        
        // Discover build paths
        DiscoverBuildPaths(project);
        
        return project;
    }
    
    /// <summary>
    /// Extract defaults section for loader assembly info.
    /// </summary>
    private void ExtractDefaults(AihaoProject project)
    {
        var defaults = project.Mix.GetTree("/defaults");
        if (defaults is not JsonObject defaultsObj)
            return;
            
        if (defaultsObj.TryGetPropertyValue("loader", out var loaderNode) &&
            loaderNode is JsonObject loader &&
            loader.TryGetPropertyValue("assembly", out var assemblyNode) &&
            assemblyNode is JsonValue assemblyValue &&
            assemblyValue.TryGetValue<string>(out var assembly))
        {
            project.DefaultLoaderAssembly = assembly;
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
    /// Add an overlay file at a specific path with given priority.
    /// Higher priority values override lower ones.
    /// </summary>
    public async Task AddOverlayAsync(AihaoProject project, string filePath, string mountPath, int priority)
    {
        var fullPath = Path.Combine(project.ProjectDirectory, filePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Overlay file not found: {fullPath}");
        }
        
        await Task.Run(() =>
        {
            using var stream = File.OpenRead(fullPath);
            project.Mix.UpsertFragment(mountPath, stream, priority);
        });
        
        // Track the file
        project.Files[filePath] = new ProjectFile
        {
            RelativePath = filePath,
            AbsolutePath = fullPath,
            Exists = true,
            IsDirty = false
        };
    }
    
    /// <summary>
    /// Remove an overlay at a specific path and priority.
    /// </summary>
    public void RemoveOverlay(AihaoProject project, string mountPath, int priority)
    {
        project.Mix.RemoveFragment(mountPath, priority);
    }
    
    /// <summary>
    /// Save changes to a specific file.
    /// Note: This requires tracking which changes belong to which file,
    /// which is complex with Mix's merged view. For now, this is a placeholder.
    /// </summary>
    public async Task SaveFileAsync(AihaoProject project, string relativePath, JsonObject content)
    {
        if (!project.Files.TryGetValue(relativePath, out var file))
        {
            throw new ArgumentException($"File not found in project: {relativePath}");
        }
        
        var json = content.ToJsonString(JsonWriteOptions);
        await File.WriteAllTextAsync(file.AbsolutePath, json);
        file.IsDirty = false;
    }
    
    /// <summary>
    /// Reload the entire project from disk.
    /// </summary>
    public async Task<AihaoProject> ReloadProjectAsync(AihaoProject project)
    {
        var fullPath = Path.Combine(project.ProjectDirectory, project.RootFilePath);
        return await LoadProjectAsync(fullPath);
    }
}
