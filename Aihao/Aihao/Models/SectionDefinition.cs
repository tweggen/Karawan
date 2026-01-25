using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Aihao.Models;

/// <summary>
/// Predefined section definitions known at compile time.
/// These represent the standard structure of a Karawan project file.
/// </summary>
public static class KnownSections
{
    public static readonly SectionDefinition GlobalSettings = new("globalSettings", "/globalSettings", "Global Settings", "⚙️");
    public static readonly SectionDefinition Modules = new("modules", "/modules", "Modules", "🧩");
    public static readonly SectionDefinition Resources = new("resources", "/resources", "Resources", "📦");
    public static readonly SectionDefinition Implementations = new("implementations", "/implementations", "Implementations", "🏭");
    public static readonly SectionDefinition MapProviders = new("mapProviders", "/mapProviders", "Map Providers", "🗺️");
    public static readonly SectionDefinition MetaGen = new("metaGen", "/metaGen", "MetaGen", "🔧");
    public static readonly SectionDefinition Properties = new("properties", "/properties", "Properties", "📋");
    public static readonly SectionDefinition Quests = new("quests", "/quests", "Quests", "📜");
    public static readonly SectionDefinition Layers = new("layers", "/layers", "Layers", "📚");
    public static readonly SectionDefinition Scenes = new("scenes", "/scenes", "Scenes", "🎬");
    public static readonly SectionDefinition Textures = new("textures", "/textures", "Textures", "🖼️");
    public static readonly SectionDefinition Animations = new("animations", "/animations", "Animations", "🎞️");
    public static readonly SectionDefinition Defaults = new("defaults", "/defaults", "Defaults", "📝");
    
    public static readonly IReadOnlyList<SectionDefinition> All = new[]
    {
        GlobalSettings, Modules, Resources, Implementations, MapProviders,
        MetaGen, Properties, Quests, Layers, Scenes, Textures, Animations, Defaults
    };
    
    public static SectionDefinition? GetById(string id)
    {
        foreach (var section in All)
        {
            if (section.Id == id) return section;
        }
        return null;
    }
}

/// <summary>
/// Defines a configuration section. Immutable, known at compile time.
/// </summary>
public sealed class SectionDefinition
{
    /// <summary>
    /// Unique identifier matching the JSON key (e.g., "globalSettings").
    /// </summary>
    public string Id { get; }
    
    /// <summary>
    /// JSON path pointing to the root of this section's content (e.g., "/globalSettings").
    /// </summary>
    public string JsonPath { get; }
    
    /// <summary>
    /// Human-readable display name (e.g., "Global Settings").
    /// </summary>
    public string DisplayName { get; }
    
    /// <summary>
    /// Icon for UI display.
    /// </summary>
    public string Icon { get; }
    
    public SectionDefinition(string id, string jsonPath, string displayName, string icon)
    {
        Id = id;
        JsonPath = jsonPath;
        DisplayName = displayName;
        Icon = icon;
    }
}

/// <summary>
/// Represents a layer contributing content to a section.
/// Layers are stacked by priority - higher priority layers override lower ones.
/// </summary>
public class SectionLayer
{
    /// <summary>
    /// Priority for merge order. Lower values are applied first (base), 
    /// higher values override (overlays). Default base layer is 0.
    /// </summary>
    public int Priority { get; set; }
    
    /// <summary>
    /// Source file path (key in IncludedFiles). 
    /// Null if content is inline in the root file.
    /// </summary>
    public string? FilePath { get; set; }
    
    /// <summary>
    /// Whether this layer is currently active and contributes to the merged view.
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Whether this is a user-added overlay (true) or discovered from __include__ (false).
    /// User overlays can be removed; discovered layers cannot.
    /// </summary>
    public bool IsOverlay { get; set; }
    
    /// <summary>
    /// Display name for UI (defaults to filename or "Inline").
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// Runtime state for a section within a loaded project.
/// </summary>
public class SectionState
{
    /// <summary>
    /// The predefined section definition.
    /// </summary>
    public SectionDefinition Definition { get; }
    
    /// <summary>
    /// Layers contributing to this section, ordered by priority (ascending).
    /// </summary>
    public List<SectionLayer> Layers { get; } = new();
    
    /// <summary>
    /// Whether this section exists in the current project.
    /// </summary>
    public bool Exists => Layers.Count > 0;
    
    public SectionState(SectionDefinition definition)
    {
        Definition = definition;
    }
    
    /// <summary>
    /// Gets all active layers, ordered by priority (lowest first).
    /// </summary>
    public IEnumerable<SectionLayer> GetActiveLayers()
    {
        foreach (var layer in Layers)
        {
            if (layer.IsActive) yield return layer;
        }
    }
    
    /// <summary>
    /// Gets the topmost active layer where changes should be written.
    /// Returns the highest-priority active layer, or null if no layers are active.
    /// </summary>
    public SectionLayer? GetWriteTarget()
    {
        SectionLayer? target = null;
        foreach (var layer in Layers)
        {
            if (layer.IsActive)
            {
                target = layer;
            }
        }
        return target;
    }
    
    /// <summary>
    /// Add an overlay layer at the specified priority.
    /// </summary>
    public void AddOverlay(string filePath, int priority, string displayName)
    {
        var layer = new SectionLayer
        {
            FilePath = filePath,
            Priority = priority,
            IsOverlay = true,
            IsActive = true,
            DisplayName = displayName
        };
        
        // Insert in priority order
        int insertIndex = 0;
        for (int i = 0; i < Layers.Count; i++)
        {
            if (Layers[i].Priority > priority)
            {
                insertIndex = i;
                break;
            }
            insertIndex = i + 1;
        }
        Layers.Insert(insertIndex, layer);
    }
    
    /// <summary>
    /// Remove a user-added overlay layer.
    /// </summary>
    public bool RemoveOverlay(string filePath)
    {
        for (int i = Layers.Count - 1; i >= 0; i--)
        {
            if (Layers[i].IsOverlay && Layers[i].FilePath == filePath)
            {
                Layers.RemoveAt(i);
                return true;
            }
        }
        return false;
    }
}
