using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aihao.Models;

/// <summary>
/// Represents an item that can be opened/navigated to via Quick Open (Ctrl+P).
/// </summary>
public class QuickOpenItem
{
    /// <summary>
    /// Unique identifier for this item.
    /// </summary>
    public string Id { get; init; } = string.Empty;
    
    /// <summary>
    /// Display name shown in the list.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;
    
    /// <summary>
    /// Optional description/subtitle.
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Category for grouping (e.g., "Editors", "Settings", "Files").
    /// </summary>
    public string Category { get; init; } = "General";
    
    /// <summary>
    /// Icon (emoji or icon name).
    /// </summary>
    public string? Icon { get; init; }
    
    /// <summary>
    /// Keywords for search matching (in addition to display name).
    /// </summary>
    public List<string> Keywords { get; init; } = new();
    
    /// <summary>
    /// Whether this item requires a project to be loaded.
    /// </summary>
    public bool RequiresProject { get; init; }
    
    /// <summary>
    /// The action to execute when this item is selected.
    /// </summary>
    public Func<Task>? Action { get; set; }
}

/// <summary>
/// Defines the mode of the command palette.
/// </summary>
public enum CommandPaletteMode
{
    /// <summary>
    /// Quick Open mode (Ctrl+P) - navigate to things.
    /// </summary>
    QuickOpen,
    
    /// <summary>
    /// Command mode (Ctrl+Shift+P) - execute actions.
    /// </summary>
    Commands
}
