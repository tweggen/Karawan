using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Aihao.Models;

/// <summary>
/// Per-user application settings, persisted to a JSON file.
/// </summary>
public class UserSettings
{
    /// <summary>
    /// Settings file format version for migration support.
    /// </summary>
    public int Version { get; set; } = 1;
    
    /// <summary>
    /// When these settings were last saved.
    /// </summary>
    public DateTime LastSaved { get; set; } = DateTime.UtcNow;
    
    #region Recent Projects
    
    /// <summary>
    /// List of recently opened project paths, most recent first.
    /// </summary>
    public List<RecentProject> RecentProjects { get; set; } = new();
    
    /// <summary>
    /// Maximum number of recent projects to remember.
    /// </summary>
    public int MaxRecentProjects { get; set; } = 10;
    
    #endregion
    
    #region Window State
    
    /// <summary>
    /// Last window X position.
    /// </summary>
    public int? WindowX { get; set; }
    
    /// <summary>
    /// Last window Y position.
    /// </summary>
    public int? WindowY { get; set; }
    
    /// <summary>
    /// Last window width.
    /// </summary>
    public int? WindowWidth { get; set; }
    
    /// <summary>
    /// Last window height.
    /// </summary>
    public int? WindowHeight { get; set; }
    
    /// <summary>
    /// Whether the window was maximized.
    /// </summary>
    public bool WindowMaximized { get; set; }
    
    #endregion
    
    #region Editor Preferences
    
    /// <summary>
    /// Last directory used in file dialogs.
    /// </summary>
    public string? LastDirectory { get; set; }
    
    /// <summary>
    /// UI theme: "Light", "Dark", or "System".
    /// </summary>
    public string Theme { get; set; } = "Dark";
    
    /// <summary>
    /// Font size for code/JSON editors.
    /// </summary>
    public int EditorFontSize { get; set; } = 13;
    
    /// <summary>
    /// Whether to auto-save settings on changes.
    /// </summary>
    public bool AutoSaveSettings { get; set; } = true;
    
    /// <summary>
    /// Whether to restore last project on startup.
    /// </summary>
    public bool RestoreLastProject { get; set; } = false;
    
    /// <summary>
    /// Console log level filter.
    /// </summary>
    public string ConsoleLogLevel { get; set; } = "Info";
    
    #endregion
    
    #region Overlay Defaults
    
    /// <summary>
    /// Default priority for user-created overlays.
    /// </summary>
    public int DefaultOverlayPriority { get; set; } = 100;
    
    /// <summary>
    /// Default overlay file prefix (e.g., "local" -> "local.globalSettings.json").
    /// </summary>
    public string DefaultOverlayPrefix { get; set; } = "local";
    
    #endregion
    
    #region Module Configuration
    
    /// <summary>
    /// Extensible configuration tree for modules.
    /// Modules can store arbitrary settings under their own key.
    /// Example: ModuleSettings["metagen"] = { "defaultTemplate": "world", ... }
    /// </summary>
    public Dictionary<string, JsonObject> ModuleSettings { get; set; } = new();
    
    #endregion
    
    #region Helper Methods
    
    /// <summary>
    /// Add a project to the recent list, moving it to front if already present.
    /// </summary>
    public void AddRecentProject(string path, string? name = null)
    {
        // Remove if already in list
        RecentProjects.RemoveAll(p => 
            string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase));
        
        // Add to front
        RecentProjects.Insert(0, new RecentProject
        {
            Path = path,
            Name = name ?? System.IO.Path.GetFileNameWithoutExtension(path),
            LastOpened = DateTime.UtcNow
        });
        
        // Trim to max
        while (RecentProjects.Count > MaxRecentProjects)
        {
            RecentProjects.RemoveAt(RecentProjects.Count - 1);
        }
    }
    
    /// <summary>
    /// Get module settings, creating an empty object if not present.
    /// </summary>
    public JsonObject GetModuleSettings(string moduleId)
    {
        if (!ModuleSettings.TryGetValue(moduleId, out var settings))
        {
            settings = new JsonObject();
            ModuleSettings[moduleId] = settings;
        }
        return settings;
    }
    
    /// <summary>
    /// Set a value in module settings.
    /// </summary>
    public void SetModuleSetting(string moduleId, string key, JsonNode? value)
    {
        var settings = GetModuleSettings(moduleId);
        settings[key] = value?.DeepClone();
    }
    
    /// <summary>
    /// Get a typed value from module settings.
    /// </summary>
    public T? GetModuleSetting<T>(string moduleId, string key, T? defaultValue = default)
    {
        var settings = GetModuleSettings(moduleId);
        if (settings.TryGetPropertyValue(key, out var node) && node is JsonValue value)
        {
            if (value.TryGetValue<T>(out var result))
            {
                return result;
            }
        }
        return defaultValue;
    }
    
    #endregion
}

/// <summary>
/// A recently opened project entry.
/// </summary>
public class RecentProject
{
    /// <summary>
    /// Full path to the project file.
    /// </summary>
    public string Path { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name of the project.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// When this project was last opened.
    /// </summary>
    public DateTime LastOpened { get; set; }
    
    /// <summary>
    /// Whether the project file still exists (checked on load).
    /// </summary>
    [JsonIgnore]
    public bool Exists { get; set; } = true;
}
