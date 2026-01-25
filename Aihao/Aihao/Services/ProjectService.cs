using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Aihao.Models;

namespace Aihao.Services;

/// <summary>
/// Service for loading and saving Karawan engine projects
/// </summary>
public class ProjectService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    
    /// <summary>
    /// Load a project from a JSON file
    /// </summary>
    public async Task<AihaoProject> LoadProjectAsync(string projectPath)
    {
        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException($"Project file not found: {projectPath}");
        }
        
        var fullPath = Path.GetFullPath(projectPath);
        var projectDir = Path.GetDirectoryName(fullPath) ?? string.Empty;
        
        var jsonContent = await File.ReadAllTextAsync(fullPath);
        var rootDocument = JsonNode.Parse(jsonContent) as JsonObject;
        
        if (rootDocument == null)
        {
            throw new InvalidDataException("Invalid project file: root must be a JSON object");
        }
        
        var project = new AihaoProject
        {
            Name = rootDocument["name"]?.GetValue<string>() ?? Path.GetFileNameWithoutExtension(projectPath),
            ProjectPath = fullPath,
            ProjectDirectory = projectDir,
            RootDocument = rootDocument
        };
        
        // Parse include paths
        if (rootDocument["include"] is JsonArray includes)
        {
            foreach (var include in includes)
            {
                var includePath = include?.GetValue<string>();
                if (!string.IsNullOrEmpty(includePath))
                {
                    project.IncludePaths.Add(includePath);
                }
            }
        }
        
        // Also check for "includes" (plural)
        if (rootDocument["includes"] is JsonArray includesPlural)
        {
            foreach (var include in includesPlural)
            {
                var includePath = include?.GetValue<string>();
                if (!string.IsNullOrEmpty(includePath))
                {
                    project.IncludePaths.Add(includePath);
                }
            }
        }
        
        // Discover all referenced files
        await DiscoverProjectFilesAsync(project);
        
        // Try to find game executable and solution
        DiscoverBuildPaths(project);
        
        return project;
    }
    
    private async Task DiscoverProjectFilesAsync(AihaoProject project)
    {
        // Add the main project file
        project.Files.Add(new ProjectFile
        {
            RelativePath = Path.GetFileName(project.ProjectPath),
            AbsolutePath = project.ProjectPath,
            FileName = Path.GetFileName(project.ProjectPath),
            Extension = Path.GetExtension(project.ProjectPath).TrimStart('.').ToLower(),
            Exists = true,
            FileType = ProjectFileType.ProjectFile
        });
        
        // Process includes
        foreach (var includePath in project.IncludePaths)
        {
            await ProcessIncludeAsync(project, includePath);
        }
        
        // Scan for file references in the JSON
        if (project.RootDocument != null)
        {
            ScanJsonForFiles(project, project.RootDocument, string.Empty);
        }
    }
    
    private async Task ProcessIncludeAsync(AihaoProject project, string includePath)
    {
        var fullPath = Path.Combine(project.ProjectDirectory, includePath);
        
        // Check if it's a glob pattern
        if (includePath.Contains('*'))
        {
            var dir = Path.GetDirectoryName(fullPath) ?? project.ProjectDirectory;
            var pattern = Path.GetFileName(includePath);
            
            if (Directory.Exists(dir))
            {
                foreach (var file in Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories))
                {
                    AddProjectFile(project, file, ProjectFileType.Include);
                }
            }
        }
        else if (File.Exists(fullPath))
        {
            AddProjectFile(project, fullPath, ProjectFileType.Include);
            
            // If it's a JSON file, parse it for more references
            if (fullPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var content = await File.ReadAllTextAsync(fullPath);
                    var node = JsonNode.Parse(content);
                    if (node is JsonObject obj)
                    {
                        ScanJsonForFiles(project, obj, Path.GetDirectoryName(fullPath) ?? string.Empty);
                    }
                }
                catch
                {
                    // Ignore parse errors in included files
                }
            }
        }
        else if (Directory.Exists(fullPath))
        {
            // Include entire directory
            foreach (var file in Directory.EnumerateFiles(fullPath, "*.*", SearchOption.AllDirectories))
            {
                var fileType = GetFileTypeFromExtension(file);
                AddProjectFile(project, file, fileType);
            }
        }
    }
    
    private void ScanJsonForFiles(AihaoProject project, JsonObject obj, string basePath)
    {
        foreach (var property in obj)
        {
            ScanJsonNode(project, property.Value, basePath, property.Key);
        }
    }
    
    private void ScanJsonNode(AihaoProject project, JsonNode? node, string basePath, string propertyName)
    {
        if (node == null) return;
        
        if (node is JsonValue value && value.TryGetValue<string>(out var str))
        {
            // Check if this looks like a file path
            if (LooksLikeFilePath(str, propertyName))
            {
                var fullPath = Path.IsPathRooted(str) 
                    ? str 
                    : Path.Combine(basePath.Length > 0 ? basePath : project.ProjectDirectory, str);
                
                if (File.Exists(fullPath) || str.Contains('.'))
                {
                    var fileType = GetFileTypeFromExtension(str);
                    AddProjectFile(project, fullPath, fileType, node);
                }
            }
        }
        else if (node is JsonObject childObj)
        {
            ScanJsonForFiles(project, childObj, basePath);
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                ScanJsonNode(project, item, basePath, propertyName);
            }
        }
    }
    
    private static bool LooksLikeFilePath(string value, string propertyName)
    {
        // Check property name hints
        var pathHints = new[] { "path", "file", "source", "texture", "model", "audio", "shader", "animation", "include" };
        foreach (var hint in pathHints)
        {
            if (propertyName.Contains(hint, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        
        // Check value characteristics
        if (value.Contains('/') || value.Contains('\\'))
            return true;
            
        // Check for common extensions
        var extensions = new[] { ".json", ".cs", ".glsl", ".hlsl", ".png", ".jpg", ".fbx", ".obj", ".wav", ".ogg", ".mp3" };
        foreach (var ext in extensions)
        {
            if (value.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        
        return false;
    }
    
    private static ProjectFileType GetFileTypeFromExtension(string path)
    {
        var ext = Path.GetExtension(path).ToLower();
        return ext switch
        {
            ".json" => ProjectFileType.Config,
            ".cs" => ProjectFileType.Source,
            ".glsl" or ".hlsl" or ".vert" or ".frag" or ".shader" => ProjectFileType.Shader,
            ".png" or ".jpg" or ".jpeg" or ".tga" or ".bmp" or ".dds" => ProjectFileType.Texture,
            ".fbx" or ".obj" or ".gltf" or ".glb" or ".dae" => ProjectFileType.Model,
            ".wav" or ".ogg" or ".mp3" or ".flac" => ProjectFileType.Audio,
            ".anim" or ".animation" => ProjectFileType.Animation,
            _ => ProjectFileType.Data
        };
    }
    
    private void AddProjectFile(AihaoProject project, string fullPath, ProjectFileType fileType, JsonNode? sourceNode = null)
    {
        // Check if already added
        foreach (var existing in project.Files)
        {
            if (existing.AbsolutePath.Equals(fullPath, StringComparison.OrdinalIgnoreCase))
                return;
        }
        
        var relativePath = Path.GetRelativePath(project.ProjectDirectory, fullPath);
        
        project.Files.Add(new ProjectFile
        {
            RelativePath = relativePath,
            AbsolutePath = fullPath,
            FileName = Path.GetFileName(fullPath),
            Extension = Path.GetExtension(fullPath).TrimStart('.').ToLower(),
            Exists = File.Exists(fullPath),
            FileType = fileType,
            SourceNode = sourceNode
        });
    }
    
    private void DiscoverBuildPaths(AihaoProject project)
    {
        // Look for common build output locations
        var possibleExePaths = new[]
        {
            Path.Combine(project.ProjectDirectory, "bin", "Debug", "net8.0", $"{project.Name}.exe"),
            Path.Combine(project.ProjectDirectory, "bin", "Release", "net8.0", $"{project.Name}.exe"),
            Path.Combine(project.ProjectDirectory, "..", "bin", "Debug", "net8.0", $"{project.Name}.exe"),
            Path.Combine(project.ProjectDirectory, "..", "bin", "Release", "net8.0", $"{project.Name}.exe"),
        };
        
        foreach (var exePath in possibleExePaths)
        {
            if (File.Exists(exePath))
            {
                project.GameExecutablePath = exePath;
                break;
            }
        }
        
        // Look for solution file
        var possibleSlnPaths = new[]
        {
            Path.Combine(project.ProjectDirectory, $"{project.Name}.sln"),
            Path.Combine(project.ProjectDirectory, "..", $"{project.Name}.sln"),
            Path.Combine(project.ProjectDirectory, "..", "..", "Karawan.sln"),
        };
        
        foreach (var slnPath in possibleSlnPaths)
        {
            if (File.Exists(slnPath))
            {
                project.SolutionPath = slnPath;
                break;
            }
        }
    }
    
    /// <summary>
    /// Save a project back to disk
    /// </summary>
    public async Task SaveProjectAsync(AihaoProject project)
    {
        if (project.RootDocument == null)
        {
            throw new InvalidOperationException("Project has no root document");
        }
        
        var json = project.RootDocument.ToJsonString(JsonOptions);
        await File.WriteAllTextAsync(project.ProjectPath, json);
        project.IsDirty = false;
    }
}
