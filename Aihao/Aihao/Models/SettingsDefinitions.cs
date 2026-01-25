using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace Aihao.Models;

/// <summary>
/// Defines a section in the settings tree.
/// </summary>
public class SettingsSection
{
    /// <summary>
    /// Unique identifier for this section.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name shown in the tree.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// Icon for the section (emoji or icon name).
    /// </summary>
    public string Icon { get; set; } = "⚙️";
    
    /// <summary>
    /// Child sections.
    /// </summary>
    public List<SettingsSection> Children { get; set; } = new();
    
    /// <summary>
    /// Settings items in this section.
    /// </summary>
    public List<SettingsItem> Items { get; set; } = new();
}

/// <summary>
/// Type of a settings item, determines the editor control.
/// </summary>
public enum SettingsItemType
{
    String,
    Int,
    Bool,
    Choice,
    Path,
    Color,
    KeyBinding
}

/// <summary>
/// Defines a single setting with its JSON path and metadata.
/// </summary>
public class SettingsItem
{
    /// <summary>
    /// JSON path in the settings file (e.g., "theme" or "modules.metagen.autoRefresh").
    /// Paths starting with "modules." are stored in ModuleSettings.
    /// </summary>
    public string JsonPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name for the setting.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// Description shown as tooltip or help text.
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of editor control to use.
    /// </summary>
    public SettingsItemType Type { get; set; } = SettingsItemType.String;
    
    /// <summary>
    /// Default value if not set.
    /// </summary>
    public object? DefaultValue { get; set; }
    
    /// <summary>
    /// For Choice type: list of available options.
    /// </summary>
    public List<ChoiceOption>? Choices { get; set; }
    
    /// <summary>
    /// For Int type: minimum value.
    /// </summary>
    public int? MinValue { get; set; }
    
    /// <summary>
    /// For Int type: maximum value.
    /// </summary>
    public int? MaxValue { get; set; }
    
    /// <summary>
    /// For Path type: whether to pick a folder instead of file.
    /// </summary>
    public bool IsFolder { get; set; }
    
    /// <summary>
    /// For Path type: file filter (e.g., "*.json").
    /// </summary>
    public string? FileFilter { get; set; }
    
    /// <summary>
    /// Whether this setting requires restart to take effect.
    /// </summary>
    public bool RequiresRestart { get; set; }
    
    /// <summary>
    /// Category within the section for grouping.
    /// </summary>
    public string? Category { get; set; }
}

/// <summary>
/// An option in a choice/dropdown setting.
/// </summary>
public class ChoiceOption
{
    /// <summary>
    /// Value stored in settings.
    /// </summary>
    public string Value { get; set; } = string.Empty;
    
    /// <summary>
    /// Display text shown to user.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional description.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Defines all available settings sections and items.
/// </summary>
public static class SettingsDefinitions
{
    public static List<SettingsSection> GetAllSections()
    {
        return new List<SettingsSection>
        {
            new SettingsSection
            {
                Id = "appearance",
                DisplayName = "Appearance",
                Icon = "🎨",
                Items = new List<SettingsItem>
                {
                    new SettingsItem
                    {
                        JsonPath = "theme",
                        DisplayName = "Theme",
                        Description = "Color theme for the application",
                        Type = SettingsItemType.Choice,
                        DefaultValue = "Dark",
                        Choices = new List<ChoiceOption>
                        {
                            new ChoiceOption { Value = "Light", DisplayName = "Light" },
                            new ChoiceOption { Value = "Dark", DisplayName = "Dark" },
                            new ChoiceOption { Value = "System", DisplayName = "System Default" }
                        }
                    },
                    new SettingsItem
                    {
                        JsonPath = "editorFontSize",
                        DisplayName = "Editor Font Size",
                        Description = "Font size for code and JSON editors",
                        Type = SettingsItemType.Int,
                        DefaultValue = 13,
                        MinValue = 8,
                        MaxValue = 32
                    }
                }
            },
            new SettingsSection
            {
                Id = "general",
                DisplayName = "General",
                Icon = "⚙️",
                Items = new List<SettingsItem>
                {
                    new SettingsItem
                    {
                        JsonPath = "restoreLastProject",
                        DisplayName = "Restore Last Project",
                        Description = "Automatically open the last project on startup",
                        Type = SettingsItemType.Bool,
                        DefaultValue = false
                    },
                    new SettingsItem
                    {
                        JsonPath = "autoSaveSettings",
                        DisplayName = "Auto-Save Settings",
                        Description = "Automatically save settings when changed",
                        Type = SettingsItemType.Bool,
                        DefaultValue = true
                    },
                    new SettingsItem
                    {
                        JsonPath = "maxRecentProjects",
                        DisplayName = "Recent Projects Limit",
                        Description = "Maximum number of recent projects to remember",
                        Type = SettingsItemType.Int,
                        DefaultValue = 10,
                        MinValue = 1,
                        MaxValue = 50
                    },
                    new SettingsItem
                    {
                        JsonPath = "lastDirectory",
                        DisplayName = "Default Directory",
                        Description = "Default directory for file dialogs",
                        Type = SettingsItemType.Path,
                        IsFolder = true
                    }
                }
            },
            new SettingsSection
            {
                Id = "console",
                DisplayName = "Console",
                Icon = "📋",
                Items = new List<SettingsItem>
                {
                    new SettingsItem
                    {
                        JsonPath = "consoleLogLevel",
                        DisplayName = "Log Level",
                        Description = "Minimum log level to display in console",
                        Type = SettingsItemType.Choice,
                        DefaultValue = "Info",
                        Choices = new List<ChoiceOption>
                        {
                            new ChoiceOption { Value = "Debug", DisplayName = "Debug (All)" },
                            new ChoiceOption { Value = "Info", DisplayName = "Info" },
                            new ChoiceOption { Value = "Warning", DisplayName = "Warning" },
                            new ChoiceOption { Value = "Error", DisplayName = "Error Only" }
                        }
                    }
                }
            },
            new SettingsSection
            {
                Id = "overlay",
                DisplayName = "Overlay",
                Icon = "📑",
                Items = new List<SettingsItem>
                {
                    new SettingsItem
                    {
                        JsonPath = "defaultOverlayPriority",
                        DisplayName = "Default Priority",
                        Description = "Default priority for new overlay files (higher overrides lower)",
                        Type = SettingsItemType.Int,
                        DefaultValue = 100,
                        MinValue = 1,
                        MaxValue = 1000
                    },
                    new SettingsItem
                    {
                        JsonPath = "defaultOverlayPrefix",
                        DisplayName = "Default Prefix",
                        Description = "Default prefix for auto-created overlay files (e.g., 'local' -> 'local.globalSettings.json')",
                        Type = SettingsItemType.String,
                        DefaultValue = "local"
                    }
                }
            },
            new SettingsSection
            {
                Id = "modules",
                DisplayName = "Modules",
                Icon = "🧩",
                Children = new List<SettingsSection>
                {
                    new SettingsSection
                    {
                        Id = "modules.metagen",
                        DisplayName = "MetaGen",
                        Icon = "🔧",
                        Items = new List<SettingsItem>
                        {
                            new SettingsItem
                            {
                                JsonPath = "modules.metagen.autoRefresh",
                                DisplayName = "Auto Refresh",
                                Description = "Automatically refresh preview when changes are made",
                                Type = SettingsItemType.Bool,
                                DefaultValue = true
                            },
                            new SettingsItem
                            {
                                JsonPath = "modules.metagen.defaultTemplate",
                                DisplayName = "Default Template",
                                Description = "Default template for new MetaGen configurations",
                                Type = SettingsItemType.String,
                                DefaultValue = "world"
                            }
                        }
                    },
                    new SettingsSection
                    {
                        Id = "modules.resources",
                        DisplayName = "Resources",
                        Icon = "📦",
                        Items = new List<SettingsItem>
                        {
                            new SettingsItem
                            {
                                JsonPath = "modules.resources.showPreview",
                                DisplayName = "Show Preview",
                                Description = "Show resource preview in the editor",
                                Type = SettingsItemType.Bool,
                                DefaultValue = true
                            }
                        }
                    }
                }
            },
            new SettingsSection
            {
                Id = "advanced",
                DisplayName = "Advanced",
                Icon = "🔬",
                Items = new List<SettingsItem>
                {
                    new SettingsItem
                    {
                        JsonPath = "modules.advanced.enableExperimental",
                        DisplayName = "Enable Experimental Features",
                        Description = "Enable features that are still in development",
                        Type = SettingsItemType.Bool,
                        DefaultValue = false,
                        RequiresRestart = true
                    }
                }
            }
        };
    }
}
