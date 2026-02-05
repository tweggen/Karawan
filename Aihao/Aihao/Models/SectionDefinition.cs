using System.Collections.Generic;
using System.IO;
using engine.casette;

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
    public static readonly SectionDefinition Narration = new("narration", "/narration", "Narration", "🎭");
    public static readonly SectionDefinition Characters = new("characters", "/characters", "Characters", "👤");
    public static readonly SectionDefinition LSystems = new("lsystems", "/lsystems", "L-Systems", "🌳");

    public static readonly IReadOnlyList<SectionDefinition> All = new[]
    {
        GlobalSettings, Modules, Resources, Implementations, MapProviders,
        MetaGen, Properties, Quests, Layers, Scenes, Textures, Animations, Defaults, Narration, Characters, LSystems
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
