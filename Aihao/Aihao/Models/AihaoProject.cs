using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;

namespace Aihao.Models;

/// <summary>
/// Represents a single JSON file in the project hierarchy.
/// Files are linked via ChildPaths which reference other entries in the parent AihaoProject.IncludedFiles map.
/// </summary>
public class IncludedFile
{
    /// <summary>
    /// Relative path from project directory (also serves as the key in the map).
    /// For the root file, this is the filename (e.g., "nogame.json").
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
    /// The JSON path where this file's content is mounted (e.g., "/globalSettings").
    /// For the root file, this is "/".
    /// </summary>
    public string MountPath { get; set; } = "/";
    
    /// <summary>
    /// Key (RelativePath) of the file that includes this one.
    /// Null for the root project file.
    /// </summary>
    public string? ParentPath { get; set; }
    
    /// <summary>
    /// Keys (RelativePaths) of files directly included by this file via __include__ directives.
    /// </summary>
    public List<string> ChildPaths { get; set; } = new();
    
    /// <summary>
    /// The parsed JSON content of this file (when loaded).
    /// </summary>
    public JsonObject? Content { get; set; }
    
    /// <summary>
    /// Whether this file has unsaved changes.
    /// </summary>
    public bool IsDirty { get; set; }
}

/// <summary>
/// Represents a Karawan engine project loaded from a root project file (e.g., nogame.json).
/// 
/// The project is structured as:
/// - A hierarchy of JSON files linked via __include__ directives (IncludedFiles)
/// - Predefined sections with layered content support (Sections)
/// 
/// This is analogous to a VS Solution containing Project files, with the addition
/// of overlay support similar to union filesystems.
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
    /// This is also the key for the root entry in IncludedFiles.
    /// </summary>
    public string RootFilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Map of all JSON files in the project, keyed by relative path.
    /// Includes the root file and all files referenced via __include__.
    /// </summary>
    public Dictionary<string, IncludedFile> IncludedFiles { get; set; } = new();
    
    /// <summary>
    /// Runtime state for each predefined section.
    /// Keyed by section ID (e.g., "globalSettings").
    /// </summary>
    public Dictionary<string, SectionState> Sections { get; } = new();
    
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
    /// Gets the root IncludedFile entry.
    /// </summary>
    public IncludedFile? RootFile => 
        IncludedFiles.TryGetValue(RootFilePath, out var file) ? file : null;
    
    /// <summary>
    /// Whether any file in the project has unsaved changes.
    /// </summary>
    public bool IsDirty
    {
        get
        {
            foreach (var file in IncludedFiles.Values)
            {
                if (file.IsDirty) return true;
            }
            return false;
        }
    }
    
    /// <summary>
    /// Initialize section states for all known sections.
    /// Call this after creating the project instance.
    /// </summary>
    public void InitializeSections()
    {
        foreach (var definition in KnownSections.All)
        {
            Sections[definition.Id] = new SectionState(definition);
        }
    }
    
    /// <summary>
    /// Gets the section state for a known section ID.
    /// Returns null if the section ID is not recognized.
    /// </summary>
    public SectionState? GetSection(string sectionId)
    {
        return Sections.TryGetValue(sectionId, out var state) ? state : null;
    }
    
    /// <summary>
    /// Gets all sections that exist in this project (have at least one layer).
    /// </summary>
    public IEnumerable<SectionState> GetExistingSections()
    {
        foreach (var state in Sections.Values)
        {
            if (state.Exists) yield return state;
        }
    }
    
    /// <summary>
    /// Gets all files that include the specified file.
    /// </summary>
    public IEnumerable<IncludedFile> GetParents(string relativePath)
    {
        foreach (var file in IncludedFiles.Values)
        {
            if (file.ChildPaths.Contains(relativePath))
            {
                yield return file;
            }
        }
    }
    
    /// <summary>
    /// Gets all descendant files (children, grandchildren, etc.) of the specified file.
    /// </summary>
    public IEnumerable<IncludedFile> GetDescendants(string relativePath)
    {
        if (!IncludedFiles.TryGetValue(relativePath, out var file))
            yield break;
            
        var visited = new HashSet<string>();
        var queue = new Queue<string>(file.ChildPaths);
        
        while (queue.Count > 0)
        {
            var childPath = queue.Dequeue();
            if (visited.Contains(childPath))
                continue;
                
            visited.Add(childPath);
            
            if (IncludedFiles.TryGetValue(childPath, out var childFile))
            {
                yield return childFile;
                foreach (var grandchildPath in childFile.ChildPaths)
                {
                    queue.Enqueue(grandchildPath);
                }
            }
        }
    }
    
    /// <summary>
    /// Resolves the file path where changes to a section should be written.
    /// Returns the topmost active layer's file, or null if the section doesn't exist
    /// or has no writable target.
    /// </summary>
    public string? GetWriteTargetPath(string sectionId)
    {
        var section = GetSection(sectionId);
        return section?.GetWriteTarget()?.FilePath;
    }
}
