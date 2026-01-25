using System.Collections.Generic;

namespace Aihao.Models;

/// <summary>
/// Defines all built-in actions and their default keybindings.
/// </summary>
public static class BuiltInActions
{
    // Action ID constants for type-safe references
    public static class Ids
    {
        // File actions
        public const string FileOpen = "aihao.file.open";
        public const string FileSave = "aihao.file.save";
        public const string FileSaveAs = "aihao.file.saveAs";
        public const string FileClose = "aihao.file.close";
        public const string FileExit = "aihao.file.exit";
        
        // Edit actions
        public const string EditUndo = "aihao.edit.undo";
        public const string EditRedo = "aihao.edit.redo";
        public const string EditCut = "aihao.edit.cut";
        public const string EditCopy = "aihao.edit.copy";
        public const string EditPaste = "aihao.edit.paste";
        public const string EditSelectAll = "aihao.edit.selectAll";
        public const string EditSettings = "aihao.edit.settings";
        public const string EditKeybindings = "aihao.edit.keybindings";
        
        // View actions
        public const string ViewProjectTree = "aihao.view.projectTree";
        public const string ViewConsole = "aihao.view.console";
        public const string ViewProperties = "aihao.view.properties";
        public const string ViewRenderOutput = "aihao.view.renderOutput";
        public const string ViewCommandPalette = "aihao.view.commandPalette";
        
        // Project actions
        public const string ProjectBuild = "aihao.project.build";
        public const string ProjectRebuild = "aihao.project.rebuild";
        public const string ProjectClean = "aihao.project.clean";
        public const string ProjectGlobalSettings = "aihao.project.globalSettings";
        public const string ProjectResources = "aihao.project.resources";
        public const string ProjectMetagen = "aihao.project.metagen";
        public const string ProjectAddOverlay = "aihao.project.addOverlay";
        
        // Run actions
        public const string RunStart = "aihao.run.start";
        public const string RunDebug = "aihao.run.debug";
        public const string RunStop = "aihao.run.stop";
        public const string RunRestart = "aihao.run.restart";
        
        // Console actions  
        public const string ConsoleClear = "aihao.console.clear";
        public const string ConsoleToggle = "aihao.console.toggle";
    }
    
    /// <summary>
    /// Get all built-in action definitions.
    /// </summary>
    public static List<ActionDefinition> GetAll()
    {
        return new List<ActionDefinition>
        {
            // === File ===
            new ActionDefinition
            {
                Id = Ids.FileOpen,
                DisplayName = "Open Project...",
                Description = "Open an existing project",
                Category = "File",
                Icon = "📂",
                DefaultKeyBinding = new KeyBinding { Key = "O", Modifiers = KeyModifiers.Control }
            },
            new ActionDefinition
            {
                Id = Ids.FileSave,
                DisplayName = "Save Project",
                Description = "Save the current project",
                Category = "File",
                Icon = "💾",
                DefaultKeyBinding = new KeyBinding { Key = "S", Modifiers = KeyModifiers.Control },
                RequiresProject = true
            },
            new ActionDefinition
            {
                Id = Ids.FileSaveAs,
                DisplayName = "Save Project As...",
                Description = "Save the project to a new location",
                Category = "File",
                Icon = "💾",
                DefaultKeyBinding = new KeyBinding { Key = "S", Modifiers = KeyModifiers.Control | KeyModifiers.Shift },
                RequiresProject = true
            },
            new ActionDefinition
            {
                Id = Ids.FileExit,
                DisplayName = "Exit",
                Description = "Exit the application",
                Category = "File",
                Icon = "🚪",
                DefaultKeyBinding = new KeyBinding { Key = "Q", Modifiers = KeyModifiers.Control }
            },
            
            // === Edit ===
            new ActionDefinition
            {
                Id = Ids.EditUndo,
                DisplayName = "Undo",
                Description = "Undo the last action",
                Category = "Edit",
                Icon = "↩️",
                DefaultKeyBinding = new KeyBinding { Key = "Z", Modifiers = KeyModifiers.Control }
            },
            new ActionDefinition
            {
                Id = Ids.EditRedo,
                DisplayName = "Redo",
                Description = "Redo the last undone action",
                Category = "Edit",
                Icon = "↪️",
                DefaultKeyBinding = new KeyBinding { Key = "Y", Modifiers = KeyModifiers.Control }
            },
            new ActionDefinition
            {
                Id = Ids.EditSettings,
                DisplayName = "Settings...",
                Description = "Open application settings",
                Category = "Edit",
                Icon = "⚙️",
                DefaultKeyBinding = new KeyBinding { Key = "OemComma", Modifiers = KeyModifiers.Control }
            },
            new ActionDefinition
            {
                Id = Ids.EditKeybindings,
                DisplayName = "Keyboard Shortcuts...",
                Description = "Configure keyboard shortcuts",
                Category = "Edit",
                Icon = "⌨️",
                DefaultKeyBinding = new KeyBinding { Key = "K", Modifiers = KeyModifiers.Control | KeyModifiers.Shift }
            },
            
            // === View ===
            new ActionDefinition
            {
                Id = Ids.ViewProjectTree,
                DisplayName = "Project Tree",
                Description = "Show/hide the project tree panel",
                Category = "View",
                Icon = "🌳"
            },
            new ActionDefinition
            {
                Id = Ids.ViewConsole,
                DisplayName = "Console",
                Description = "Show/hide the console panel",
                Category = "View",
                Icon = "📋",
                DefaultKeyBinding = new KeyBinding { Key = "J", Modifiers = KeyModifiers.Control }
            },
            new ActionDefinition
            {
                Id = Ids.ViewProperties,
                DisplayName = "Properties",
                Description = "Show/hide the properties panel",
                Category = "View",
                Icon = "📝"
            },
            new ActionDefinition
            {
                Id = Ids.ViewRenderOutput,
                DisplayName = "Render Output",
                Description = "Show the render output window",
                Category = "View",
                Icon = "🖥️",
                RequiresProject = true
            },
            new ActionDefinition
            {
                Id = Ids.ViewCommandPalette,
                DisplayName = "Command Palette",
                Description = "Open the command palette",
                Category = "View",
                Icon = "🔍",
                DefaultKeyBinding = new KeyBinding { Key = "P", Modifiers = KeyModifiers.Control | KeyModifiers.Shift },
                ShowInCommandPalette = false // Don't show command palette in itself
            },
            
            // === Project ===
            new ActionDefinition
            {
                Id = Ids.ProjectBuild,
                DisplayName = "Build",
                Description = "Build the current project",
                Category = "Project",
                Icon = "🔨",
                DefaultKeyBinding = new KeyBinding { Key = "B", Modifiers = KeyModifiers.Control | KeyModifiers.Shift },
                RequiresProject = true
            },
            new ActionDefinition
            {
                Id = Ids.ProjectRebuild,
                DisplayName = "Rebuild",
                Description = "Clean and rebuild the project",
                Category = "Project",
                Icon = "🔨",
                RequiresProject = true
            },
            new ActionDefinition
            {
                Id = Ids.ProjectClean,
                DisplayName = "Clean",
                Description = "Clean build outputs",
                Category = "Project",
                Icon = "🧹",
                RequiresProject = true
            },
            new ActionDefinition
            {
                Id = Ids.ProjectGlobalSettings,
                DisplayName = "Global Settings",
                Description = "Open the global settings editor",
                Category = "Project",
                Icon = "⚙️",
                RequiresProject = true
            },
            new ActionDefinition
            {
                Id = Ids.ProjectResources,
                DisplayName = "Resources",
                Description = "Open the resources editor",
                Category = "Project",
                Icon = "📦",
                RequiresProject = true
            },
            new ActionDefinition
            {
                Id = Ids.ProjectMetagen,
                DisplayName = "Metagen",
                Description = "Open the metagen editor",
                Category = "Project",
                Icon = "🔧",
                RequiresProject = true
            },
            new ActionDefinition
            {
                Id = Ids.ProjectAddOverlay,
                DisplayName = "Add Overlay...",
                Description = "Add an overlay file to the project",
                Category = "Project",
                Icon = "📑",
                RequiresProject = true
            },
            
            // === Run ===
            new ActionDefinition
            {
                Id = Ids.RunStart,
                DisplayName = "Start",
                Description = "Run the game",
                Category = "Run",
                Icon = "▶️",
                DefaultKeyBinding = new KeyBinding { Key = "F5", Modifiers = KeyModifiers.None },
                RequiresProject = true
            },
            new ActionDefinition
            {
                Id = Ids.RunDebug,
                DisplayName = "Start with Debugger",
                Description = "Run the game with debugger attached",
                Category = "Run",
                Icon = "🐛",
                DefaultKeyBinding = new KeyBinding { Key = "F5", Modifiers = KeyModifiers.Control },
                RequiresProject = true
            },
            new ActionDefinition
            {
                Id = Ids.RunStop,
                DisplayName = "Stop",
                Description = "Stop the running game",
                Category = "Run",
                Icon = "⏹️",
                DefaultKeyBinding = new KeyBinding { Key = "F5", Modifiers = KeyModifiers.Shift },
                RequiresProject = true
            },
            new ActionDefinition
            {
                Id = Ids.RunRestart,
                DisplayName = "Restart",
                Description = "Restart the game",
                Category = "Run",
                Icon = "🔄",
                DefaultKeyBinding = new KeyBinding { Key = "F5", Modifiers = KeyModifiers.Control | KeyModifiers.Shift },
                RequiresProject = true
            },
            
            // === Console ===
            new ActionDefinition
            {
                Id = Ids.ConsoleClear,
                DisplayName = "Clear Console",
                Description = "Clear console output",
                Category = "Console",
                Icon = "🗑️"
            }
        };
    }
}
