using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aihao.Models;

/// <summary>
/// Defines an action that can be triggered by menus, keybindings, or command palette.
/// </summary>
public class ActionDefinition
{
    /// <summary>
    /// Unique identifier for the action (e.g., "aihao.openSettings", "project.build").
    /// </summary>
    public string Id { get; init; } = string.Empty;
    
    /// <summary>
    /// Display name shown in menus and command palette.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;
    
    /// <summary>
    /// Description/tooltip for the action.
    /// </summary>
    public string Description { get; init; } = string.Empty;
    
    /// <summary>
    /// Category for grouping (e.g., "File", "Edit", "Project", "View").
    /// </summary>
    public string Category { get; init; } = "General";
    
    /// <summary>
    /// Icon for the action (emoji or icon name).
    /// </summary>
    public string? Icon { get; init; }
    
    /// <summary>
    /// Default keybinding (can be overridden by user).
    /// </summary>
    public KeyBinding? DefaultKeyBinding { get; init; }
    
    /// <summary>
    /// Whether this action requires a project to be loaded.
    /// </summary>
    public bool RequiresProject { get; init; }
    
    /// <summary>
    /// Whether this action is visible in the command palette.
    /// </summary>
    public bool ShowInCommandPalette { get; init; } = true;
}

/// <summary>
/// Represents a keyboard shortcut.
/// </summary>
public class KeyBinding : IEquatable<KeyBinding>
{
    /// <summary>
    /// The main key (e.g., "S", "F5", "OemComma").
    /// </summary>
    public string Key { get; init; } = string.Empty;
    
    /// <summary>
    /// Modifier keys.
    /// </summary>
    public KeyModifiers Modifiers { get; init; } = KeyModifiers.None;
    
    /// <summary>
    /// Parse a keybinding string like "Ctrl+Shift+S" or "Cmd+,".
    /// </summary>
    public static KeyBinding? Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;
        
        var parts = input.Split('+');
        var modifiers = KeyModifiers.None;
        string? key = null;
        
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            var lower = trimmed.ToLowerInvariant();
            
            switch (lower)
            {
                case "ctrl":
                case "control":
                    modifiers |= KeyModifiers.Control;
                    break;
                case "shift":
                    modifiers |= KeyModifiers.Shift;
                    break;
                case "alt":
                    modifiers |= KeyModifiers.Alt;
                    break;
                case "cmd":
                case "meta":
                case "win":
                case "super":
                    modifiers |= KeyModifiers.Meta;
                    break;
                default:
                    key = trimmed;
                    break;
            }
        }
        
        if (string.IsNullOrEmpty(key))
            return null;
        
        return new KeyBinding { Key = key, Modifiers = modifiers };
    }
    
    /// <summary>
    /// Convert to display string like "Ctrl+Shift+S".
    /// </summary>
    public override string ToString()
    {
        var parts = new List<string>();
        
        if (Modifiers.HasFlag(KeyModifiers.Control))
            parts.Add(OperatingSystem.IsMacOS() ? "⌃" : "Ctrl");
        if (Modifiers.HasFlag(KeyModifiers.Alt))
            parts.Add(OperatingSystem.IsMacOS() ? "⌥" : "Alt");
        if (Modifiers.HasFlag(KeyModifiers.Shift))
            parts.Add(OperatingSystem.IsMacOS() ? "⇧" : "Shift");
        if (Modifiers.HasFlag(KeyModifiers.Meta))
            parts.Add(OperatingSystem.IsMacOS() ? "⌘" : "Win");
        
        parts.Add(FormatKey(Key));
        
        return string.Join(OperatingSystem.IsMacOS() ? "" : "+", parts);
    }
    
    /// <summary>
    /// Convert to Avalonia gesture string for KeyBinding.Gesture.
    /// </summary>
    public string ToGestureString()
    {
        var parts = new List<string>();
        
        if (Modifiers.HasFlag(KeyModifiers.Control))
            parts.Add("Ctrl");
        if (Modifiers.HasFlag(KeyModifiers.Alt))
            parts.Add("Alt");
        if (Modifiers.HasFlag(KeyModifiers.Shift))
            parts.Add("Shift");
        if (Modifiers.HasFlag(KeyModifiers.Meta))
            parts.Add("Cmd");
        
        parts.Add(Key);
        
        return string.Join("+", parts);
    }
    
    private static string FormatKey(string key)
    {
        // Format special keys for display
        return key.ToLowerInvariant() switch
        {
            "oemcomma" => ",",
            "oemperiod" => ".",
            "oemplus" => "+",
            "oemminus" => "-",
            "return" or "enter" => "Enter",
            "back" or "backspace" => "Backspace",
            "escape" or "esc" => "Esc",
            "space" => "Space",
            "tab" => "Tab",
            "delete" or "del" => "Del",
            "insert" or "ins" => "Ins",
            "home" => "Home",
            "end" => "End",
            "pageup" or "pgup" => "PgUp",
            "pagedown" or "pgdn" => "PgDn",
            "up" => "↑",
            "down" => "↓",
            "left" => "←",
            "right" => "→",
            _ => key.Length == 1 ? key.ToUpperInvariant() : key
        };
    }
    
    public bool Equals(KeyBinding? other)
    {
        if (other is null) return false;
        return Key.Equals(other.Key, StringComparison.OrdinalIgnoreCase) && 
               Modifiers == other.Modifiers;
    }
    
    public override bool Equals(object? obj) => Equals(obj as KeyBinding);
    
    public override int GetHashCode() => HashCode.Combine(Key.ToLowerInvariant(), Modifiers);
}

/// <summary>
/// Modifier keys for keybindings.
/// </summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Control = 1,
    Shift = 2,
    Alt = 4,
    Meta = 8  // Cmd on Mac, Win on Windows
}

/// <summary>
/// A user's custom keybinding override.
/// </summary>
public class KeyBindingOverride
{
    /// <summary>
    /// The action ID this binding is for.
    /// </summary>
    public string ActionId { get; set; } = string.Empty;
    
    /// <summary>
    /// The custom keybinding (null to disable the action's keybinding).
    /// </summary>
    public string? KeyBinding { get; set; }
    
    /// <summary>
    /// Whether this override disables the keybinding entirely.
    /// </summary>
    public bool Disabled { get; set; }
}
