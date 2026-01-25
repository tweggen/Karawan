using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Aihao.Models;

/// <summary>
/// Represents a Karawan engine project loaded from a project file (e.g., nogame.json)
/// </summary>
public class AihaoProject
{
    /// <summary>
    /// Project name
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Full path to the project file
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Directory containing the project file
    /// </summary>
    public string ProjectDirectory { get; set; } = string.Empty;
    
    /// <summary>
    /// The raw JSON document from the project file
    /// </summary>
    public JsonObject? RootDocument { get; set; }
    
    /// <summary>
    /// All files referenced by the project
    /// </summary>
    public List<ProjectFile> Files { get; set; } = new();
    
    /// <summary>
    /// Global settings node from the project file
    /// </summary>
    public JsonObject? GlobalSettings => RootDocument?["globalSettings"] as JsonObject;
    
    /// <summary>
    /// Metagen configuration node
    /// </summary>
    public JsonObject? Metagen => RootDocument?["metagen"] as JsonObject;
    
    /// <summary>
    /// Properties node
    /// </summary>
    public JsonObject? Properties => RootDocument?["properties"] as JsonObject;
    
    /// <summary>
    /// Resources array
    /// </summary>
    public JsonArray? Resources => RootDocument?["resources"] as JsonArray;
    
    /// <summary>
    /// Include paths for additional files
    /// </summary>
    public List<string> IncludePaths { get; set; } = new();
    
    /// <summary>
    /// Path to the game executable
    /// </summary>
    public string? GameExecutablePath { get; set; }
    
    /// <summary>
    /// Solution file path for debugging
    /// </summary>
    public string? SolutionPath { get; set; }
    
    /// <summary>
    /// Whether the project has unsaved changes
    /// </summary>
    public bool IsDirty { get; set; }
}

/// <summary>
/// Represents a file within the project
/// </summary>
public class ProjectFile
{
    /// <summary>
    /// Relative path from project root
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Absolute path to the file
    /// </summary>
    public string AbsolutePath { get; set; } = string.Empty;
    
    /// <summary>
    /// File name without directory
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// File extension (lowercase, without dot)
    /// </summary>
    public string Extension { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this file exists on disk
    /// </summary>
    public bool Exists { get; set; }
    
    /// <summary>
    /// File type category
    /// </summary>
    public ProjectFileType FileType { get; set; }
    
    /// <summary>
    /// The JSON node that references this file (if applicable)
    /// </summary>
    public JsonNode? SourceNode { get; set; }
}

public enum ProjectFileType
{
    Unknown,
    ProjectFile,    // The main project json file
    Include,        // Included json files
    Source,         // C# source files
    Shader,         // Shader files
    Texture,        // Image/texture files
    Model,          // 3D model files
    Audio,          // Audio files
    Animation,      // Animation files
    Config,         // Configuration files
    Data            // Generic data files
}
