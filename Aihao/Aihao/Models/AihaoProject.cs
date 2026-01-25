using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using engine.casette;

namespace Aihao.Models;

/// <summary>
/// Represents a single JSON file in the project hierarchy.
/// Used for tracking file state (dirty, exists) independent of Mix.
/// </summary>
public class ProjectFile
{
    /// <summary>
    /// Relative path from project directory.
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Absolute filesystem path.
    /// </summary>
    public string AbsolutePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this file exists on disk.
    /// </summary>
    public bool Exists { get; set; }
    
    /// <summary>
    /// Whether this file has unsaved changes.
    /// </summary>
    public bool IsDirty { get; set; }
}

/// <summary>
/// Represents a Karawan engine project loaded from a root project file (e.g., nogame.json).
/// 
/// Uses Mix directly for JSON merging and subscription. The project tracks:
/// - File metadata (existence, dirty state)
/// - Build paths (solution, executable)
/// - Section existence
/// </summary>
public class AihaoProject
{
    /// <summary>
    /// Project name (derived from root filename without extension).
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Directory containing the project files.
    /// </summary>
    public string ProjectDirectory { get; set; } = string.Empty;
    
    /// <summary>
    /// Relative path of the root project file (e.g., "nogame.json").
    /// </summary>
    public string RootFilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// The Mix instance that manages the merged JSON tree.
    /// This is the single source of truth for configuration content.
    /// </summary>
    public Mix Mix { get; set; } = null!;
    
    /// <summary>
    /// Map of all JSON files in the project, keyed by relative path.
    /// Used to track file metadata (dirty state, existence).
    /// </summary>
    public Dictionary<string, ProjectFile> Files { get; set; } = new();
    
    /// <summary>
    /// Path to the associated solution file for debugging (e.g., Karawan.sln).
    /// </summary>
    public string? SolutionPath { get; set; }
    
    /// <summary>
    /// Path to the game executable.
    /// </summary>
    public string? GameExecutablePath { get; set; }
    
    /// <summary>
    /// Default loader assembly from the "defaults" section.
    /// </summary>
    public string? DefaultLoaderAssembly { get; set; }
    
    /// <summary>
    /// Gets the merged content for a section.
    /// </summary>
    public JsonNode? GetSection(string sectionId)
    {
        var definition = KnownSections.GetById(sectionId);
        if (definition == null) return null;
        return Mix.GetTree(definition.JsonPath);
    }
    
    /// <summary>
    /// Gets the merged content for a section by definition.
    /// </summary>
    public JsonNode? GetSection(SectionDefinition definition)
    {
        return Mix.GetTree(definition.JsonPath);
    }
    
    /// <summary>
    /// Subscribe to changes at a specific section.
    /// </summary>
    public IDisposable SubscribeToSection(string sectionId, Action<View.DomChangeEvent> handler)
    {
        var definition = KnownSections.GetById(sectionId);
        if (definition == null) throw new ArgumentException($"Unknown section: {sectionId}");
        return Mix.Subscribe(definition.JsonPath, handler);
    }
    
    /// <summary>
    /// Gets all sections that have content in the current project.
    /// </summary>
    public IEnumerable<SectionDefinition> GetExistingSections()
    {
        foreach (var definition in KnownSections.All)
        {
            var content = Mix.GetTree(definition.JsonPath);
            if (content != null)
            {
                yield return definition;
            }
        }
    }
    
    /// <summary>
    /// Whether any file in the project has unsaved changes.
    /// </summary>
    public bool IsDirty
    {
        get
        {
            foreach (var file in Files.Values)
            {
                if (file.IsDirty) return true;
            }
            return false;
        }
    }
}
