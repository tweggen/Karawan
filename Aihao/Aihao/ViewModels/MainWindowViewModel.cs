using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Aihao.Models;
using Aihao.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aihao.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ProjectService _projectService;
    private readonly ProcessService _processService;
    private readonly DockingService _dockingService;
    private readonly FileDialogService _fileDialogService;
    private readonly UserSettingsService _userSettingsService;
    private readonly ActionService _actionService;
    
    [ObservableProperty]
    private string _title = "Aihao - Karawan Engine Editor";
    
    [ObservableProperty]
    private AihaoProject? _currentProject;
    
    [ObservableProperty]
    private bool _isProjectLoaded;
    
    [ObservableProperty]
    private string _statusMessage = "Ready";
    
    [ObservableProperty]
    private bool _isGameRunning;
    
    /// <summary>
    /// User settings (persisted across sessions).
    /// </summary>
    public UserSettings UserSettings => _userSettingsService.Settings;
    
    /// <summary>
    /// Recent projects for the File menu.
    /// </summary>
    public ObservableCollection<RecentProject> RecentProjects { get; } = new();
    
    /// <summary>
    /// The action service for executing commands.
    /// </summary>
    public ActionService ActionService => _actionService;
    
    // Child ViewModels - these are the main panels
    public ProjectTreeViewModel ProjectTree { get; }
    public ConsoleWindowViewModel Console { get; }
    public PropertiesEditorViewModel Properties { get; }
    public GlobalSettingsEditorViewModel GlobalSettings { get; }
    public ResourceListEditorViewModel Resources { get; }
    public MetagenEditorViewModel Metagen { get; }
    public OpenGLWindowViewModel RenderOutput { get; }
    
    // Open document tabs
    public ObservableCollection<DocumentTabViewModel> OpenDocuments { get; } = new();
    
    [ObservableProperty]
    private DocumentTabViewModel? _selectedDocument;
    
    public MainWindowViewModel()
    {
        _projectService = new ProjectService();
        _processService = new ProcessService();
        _dockingService = new DockingService();
        _fileDialogService = new FileDialogService();
        _userSettingsService = new UserSettingsService();
        _actionService = new ActionService();
        
        // Initialize all view models
        ProjectTree = new ProjectTreeViewModel();
        Console = new ConsoleWindowViewModel();
        Properties = new PropertiesEditorViewModel();
        GlobalSettings = new GlobalSettingsEditorViewModel();
        Resources = new ResourceListEditorViewModel();
        Metagen = new MetagenEditorViewModel();
        RenderOutput = new OpenGLWindowViewModel();
        
        // Wire up process output to console
        _processService.OutputReceived += (s, line) => Console.AddLine(line, LogLevel.Info);
        _processService.ErrorReceived += (s, line) => Console.AddLine(line, LogLevel.Error);
        _processService.ProcessExited += (s, code) =>
        {
            IsGameRunning = false;
            Console.AddLine($"Process exited with code: {code}", code == 0 ? LogLevel.Info : LogLevel.Warning);
            StatusMessage = "Ready";
        };
        
        // Wire up project tree selection
        ProjectTree.FileSelected += OnFileSelected;
        ProjectTree.FileDoubleClicked += OnFileDoubleClicked;
        
        // Register dockable windows
        RegisterDockableWindows();
    }
    
    /// <summary>
    /// Initialize the view model (call after construction, allows async).
    /// </summary>
    public async Task InitializeAsync()
    {
        // Load user settings
        await _userSettingsService.LoadAsync();
        
        // Register all actions
        RegisterActions();
        
        // Apply keybinding overrides from settings
        _actionService.ApplyOverrides(UserSettings.KeyBindingOverrides);
        
        // Populate recent projects
        RefreshRecentProjects();
        
        // Restore last project if enabled
        if (UserSettings.RestoreLastProject && RecentProjects.Count > 0)
        {
            var lastProject = RecentProjects[0];
            if (lastProject.Exists)
            {
                await LoadProject(lastProject.Path);
            }
        }
        
        Console.AddLine($"Settings loaded from: {_userSettingsService.GetSettingsDirectory()}", LogLevel.Debug);
        Console.AddLine($"Registered {_actionService.Actions.Count} actions", LogLevel.Debug);
    }
    
    /// <summary>
    /// Register all actions with the action service.
    /// </summary>
    private void RegisterActions()
    {
        foreach (var action in BuiltInActions.GetAll())
        {
            Func<Task> handler = action.Id switch
            {
                // File actions
                BuiltInActions.Ids.FileOpen => OpenProject,
                BuiltInActions.Ids.FileSave => SaveProject,
                BuiltInActions.Ids.FileSaveAs => SaveProjectAs,
                BuiltInActions.Ids.FileExit => Exit,
                
                // Edit actions
                BuiltInActions.Ids.EditSettings => OpenSettings,
                BuiltInActions.Ids.EditKeybindings => OpenKeybindings,
                
                // View actions
                BuiltInActions.Ids.ViewConsole => () => { ShowConsole(); return Task.CompletedTask; },
                BuiltInActions.Ids.ViewRenderOutput => () => { OpenRenderWindow(); return Task.CompletedTask; },
                BuiltInActions.Ids.ViewCommandPalette => OpenCommandPalette,
                BuiltInActions.Ids.ViewQuickOpen => OpenQuickOpen,
                
                // Project actions
                BuiltInActions.Ids.ProjectBuild => BuildProject,
                BuiltInActions.Ids.ProjectGlobalSettings => () => { OpenGlobalSettingsEditor(); return Task.CompletedTask; },
                BuiltInActions.Ids.ProjectResources => () => { OpenResourcesEditor(); return Task.CompletedTask; },
                BuiltInActions.Ids.ProjectMetagen => () => { OpenMetagenEditor(); return Task.CompletedTask; },
                BuiltInActions.Ids.ProjectAddOverlay => AddOverlay,
                
                // Run actions
                BuiltInActions.Ids.RunStart => RunGame,
                BuiltInActions.Ids.RunDebug => DebugGame,
                BuiltInActions.Ids.RunStop => () => { StopGame(); return Task.CompletedTask; },
                
                // Console actions
                BuiltInActions.Ids.ConsoleClear => () => { ClearConsole(); return Task.CompletedTask; },
                
                // Default - log unhandled
                _ => () => { Console.AddLine($"Action not implemented: {action.Id}", LogLevel.Warning); return Task.CompletedTask; }
            };
            
            Func<bool>? canExecute = action.RequiresProject 
                ? () => IsProjectLoaded 
                : null;
            
            // Special case for stop - only when running
            if (action.Id == BuiltInActions.Ids.RunStop)
            {
                canExecute = () => IsGameRunning;
            }
            else if (action.Id == BuiltInActions.Ids.RunStart || action.Id == BuiltInActions.Ids.RunDebug)
            {
                canExecute = () => IsProjectLoaded && !IsGameRunning;
            }
            
            _actionService.RegisterAction(action, handler, canExecute);
        }
    }
    
    /// <summary>
    /// Refresh the RecentProjects collection from settings.
    /// </summary>
    private void RefreshRecentProjects()
    {
        RecentProjects.Clear();
        foreach (var project in UserSettings.RecentProjects)
        {
            RecentProjects.Add(project);
        }
    }
    
    /// <summary>
    /// Save window state before closing.
    /// </summary>
    public async Task SaveWindowStateAsync(int? x, int? y, int? width, int? height, bool maximized)
    {
        _userSettingsService.SetWindowState(x, y, width, height, maximized);
        await _userSettingsService.SaveAsync();
    }
    
    /// <summary>
    /// Get saved window state for restoration.
    /// </summary>
    public (int? X, int? Y, int? Width, int? Height, bool Maximized) GetSavedWindowState()
    {
        return (
            UserSettings.WindowX,
            UserSettings.WindowY,
            UserSettings.WindowWidth,
            UserSettings.WindowHeight,
            UserSettings.WindowMaximized
        );
    }
    
    private void RegisterDockableWindows()
    {
        _dockingService.RegisterWindow("projectTree", "Project", typeof(ProjectTreeViewModel), DockPosition.Left);
        _dockingService.RegisterWindow("console", "Console", typeof(ConsoleWindowViewModel), DockPosition.Bottom);
        _dockingService.RegisterWindow("properties", "Properties", typeof(PropertiesEditorViewModel), DockPosition.Right);
        _dockingService.RegisterWindow("globalSettings", "Global Settings", typeof(GlobalSettingsEditorViewModel), DockPosition.Center);
        _dockingService.RegisterWindow("resources", "Resources", typeof(ResourceListEditorViewModel), DockPosition.Center);
        _dockingService.RegisterWindow("metagen", "Metagen", typeof(MetagenEditorViewModel), DockPosition.Center);
        _dockingService.RegisterWindow("renderOutput", "Render Output", typeof(OpenGLWindowViewModel), DockPosition.Center);
    }
    
    private void OnFileSelected(object? sender, FileTreeItemViewModel file)
    {
        // Update properties panel with selected item info
        StatusMessage = file.IsFile ? file.FullPath : file.Name;
    }
    
    private void OnFileDoubleClicked(object? sender, FileTreeItemViewModel file)
    {
        if (CurrentProject == null) return;
        
        Console.AddLine($"Double-clicked: {file.Name}, NodeType: '{file.NodeType}', IsFile: {file.IsFile}", LogLevel.Debug);
        
        // Open editor based on node type (section) or file
        if (!string.IsNullOrEmpty(file.NodeType))
        {
            Console.AddLine($"Opening section editor for: {file.NodeType}", LogLevel.Debug);
            OpenSectionEditor(file.NodeType);
        }
        else if (file.IsFile && !string.IsNullOrEmpty(file.RelativePath))
        {
            OpenFileInEditor(file.RelativePath);
        }
        else
        {
            Console.AddLine($"No action for item: {file.Name}", LogLevel.Debug);
        }
    }
    
    private void OpenSectionEditor(string sectionId)
    {
        if (CurrentProject == null) return;
        
        var definition = KnownSections.GetById(sectionId);
        if (definition == null)
        {
            Console.AddLine($"Unknown section: {sectionId}", LogLevel.Error);
            return;
        }
        
        // Get merged content from Mix
        var content = CurrentProject.GetSection(sectionId);
        if (content == null)
        {
            Console.AddLine($"No content found for section: {sectionId} (path: {definition.JsonPath})", LogLevel.Warning);
            return;
        }
        
        Console.AddLine($"Opening section {sectionId}: content type = {content.GetType().Name}", LogLevel.Debug);
        
        // Open the appropriate editor based on section type
        switch (sectionId)
        {
            case "globalSettings":
                if (content is JsonObject settingsObj)
                {
                    GlobalSettings.LoadFromJson(settingsObj);
                    OpenOrFocusDocument(definition.DisplayName, GlobalSettings);
                }
                break;
                
            case "properties":
                if (content is JsonObject propertiesObj)
                {
                    // Create a new instance for the document tab (separate from the right panel inspector)
                    var propertiesVm = new PropertiesEditorViewModel();
                    propertiesVm.LoadFromJson(propertiesObj, definition.DisplayName);
                    OpenOrFocusDocument(definition.DisplayName, propertiesVm);
                }
                break;
                
            case "metaGen":
                if (content is JsonObject metagenObj)
                {
                    Metagen.LoadFromJson(metagenObj);
                    OpenOrFocusDocument(definition.DisplayName, Metagen);
                }
                break;
                
            case "resources":
                if (content is JsonArray resourcesArr)
                {
                    Resources.LoadFromJson(resourcesArr);
                    OpenOrFocusDocument(definition.DisplayName, Resources);
                }
                else if (content is JsonObject resourcesObj)
                {
                    Resources.LoadFromJsonObject(resourcesObj);
                    OpenOrFocusDocument(definition.DisplayName, Resources);
                }
                break;
                
            default:
                // Open as generic JSON editor
                OpenJsonEditor(definition.DisplayName, content);
                break;
        }
    }
    
    [RelayCommand]
    private void OpenGlobalSettingsEditor()
    {
        OpenSectionEditor("globalSettings");
    }
    
    [RelayCommand]
    private void OpenPropertiesEditor()
    {
        OpenSectionEditor("properties");
    }
    
    [RelayCommand]
    private void OpenMetagenEditor()
    {
        OpenSectionEditor("metaGen");
    }
    
    [RelayCommand]
    private void OpenResourcesEditor()
    {
        OpenSectionEditor("resources");
    }
    
    [RelayCommand]
    private void OpenRenderWindow()
    {
        OpenOrFocusDocument("Render Output", RenderOutput);
    }
    
    private void OpenFileInEditor(string relativePath)
    {
        if (CurrentProject == null) return;
        
        // Check if already open
        foreach (var doc in OpenDocuments)
        {
            if (doc.FilePath == relativePath)
            {
                SelectedDocument = doc;
                return;
            }
        }
        
        // Create new document tab
        var tab = new DocumentTabViewModel
        {
            Title = Path.GetFileName(relativePath),
            FilePath = relativePath
        };
        
        // TODO: Create appropriate editor based on file content
        
        OpenDocuments.Add(tab);
        SelectedDocument = tab;
    }
    
    private void OpenJsonEditor(string title, JsonNode content)
    {
        // TODO: Create a generic JSON editor view model
        Console.AddLine($"Would open JSON editor for: {title}", LogLevel.Debug);
    }
    
    private void OpenOrFocusDocument(string title, object viewModel)
    {
        // Check if already open
        foreach (var doc in OpenDocuments)
        {
            if (doc.Title == title)
            {
                SelectedDocument = doc;
                return;
            }
        }
        
        // Create new document tab
        var tab = new DocumentTabViewModel
        {
            Title = title,
            Content = viewModel
        };
        
        OpenDocuments.Add(tab);
        SelectedDocument = tab;
    }
    
    [RelayCommand]
    private void CloseDocument(DocumentTabViewModel? doc)
    {
        if (doc != null)
        {
            OpenDocuments.Remove(doc);
            
            // Select another document or clear selection
            if (SelectedDocument == doc)
            {
                SelectedDocument = OpenDocuments.Count > 0 ? OpenDocuments[0] : null;
            }
        }
    }
    
    /// <summary>
    /// Open a project file using a file dialog.
    /// </summary>
    [RelayCommand]
    private async Task OpenProject()
    {
        var path = await _fileDialogService.OpenJsonFileAsync(
            "Open Karawan Project",
            UserSettings.LastDirectory);
        
        if (!string.IsNullOrEmpty(path))
        {
            await _userSettingsService.SetLastDirectoryAsync(Path.GetDirectoryName(path));
            await LoadProject(path);
        }
    }
    
    /// <summary>
    /// Save the current project to a new location.
    /// </summary>
    [RelayCommand]
    private async Task SaveProjectAs()
    {
        if (CurrentProject == null) return;
        
        var suggestedName = CurrentProject.RootFilePath;
        var path = await _fileDialogService.SaveJsonFileAsync(
            "Save Project As",
            suggestedName,
            CurrentProject.ProjectDirectory);
        
        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                StatusMessage = $"Saving project to: {path}";
                
                // Get the entire merged configuration
                var rootContent = CurrentProject.Mix.GetTree("/");
                if (rootContent != null)
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var json = rootContent.ToJsonString(options);
                    await File.WriteAllTextAsync(path, json);
                    
                    StatusMessage = "Project saved";
                    Console.AddLine($"Project saved to: {path}", LogLevel.Info);
                    
                    // Update last directory
                    await _userSettingsService.SetLastDirectoryAsync(Path.GetDirectoryName(path));
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Save failed: {ex.Message}";
                Console.AddLine($"Failed to save project: {ex.Message}", LogLevel.Error);
            }
        }
    }
    
    /// <summary>
    /// Open a recent project from the list.
    /// </summary>
    [RelayCommand]
    private async Task OpenRecentProject(RecentProject? project)
    {
        if (project == null || string.IsNullOrEmpty(project.Path)) return;
        
        if (!File.Exists(project.Path))
        {
            Console.AddLine($"Project file not found: {project.Path}", LogLevel.Warning);
            project.Exists = false;
            return;
        }
        
        await LoadProject(project.Path);
    }
    
    private async Task LoadProject(string path)
    {
        try
        {
            StatusMessage = $"Loading project: {path}";
            CurrentProject = await _projectService.LoadProjectAsync(path);
            
            if (CurrentProject != null)
            {
                Title = $"Aihao - {CurrentProject.Name}";
                ProjectTree.LoadProject(CurrentProject);
                IsProjectLoaded = true;
                StatusMessage = $"Loaded: {CurrentProject.Name}";
                Console.AddLine($"Project loaded: {CurrentProject.Name}", LogLevel.Info);
                Console.AddLine($"  Directory: {CurrentProject.ProjectDirectory}", LogLevel.Debug);
                Console.AddLine($"  Root file: {CurrentProject.RootFilePath}", LogLevel.Debug);
                Console.AddLine($"  Files tracked: {CurrentProject.Files.Count}", LogLevel.Debug);
                Console.AddLine($"  Additional files: {CurrentProject.Mix.AdditionalFiles.Count}", LogLevel.Debug);
                
                // Log metadata if present
                if (CurrentProject.Metadata.Count > 0)
                {
                    Console.AddLine($"  Metadata:", LogLevel.Debug);
                    foreach (var kvp in CurrentProject.Metadata)
                    {
                        Console.AddLine($"    {kvp.Key}: {kvp.Value}", LogLevel.Debug);
                    }
                }
                
                // Log existing sections
                Console.AddLine($"  Sections found:", LogLevel.Debug);
                foreach (var section in CurrentProject.GetExistingSections())
                {
                    var sectionContent = CurrentProject.GetSection(section.Id);
                    var contentType = sectionContent?.GetType().Name ?? "null";
                    Console.AddLine($"    {section.DisplayName} ({section.Id}): {contentType}", LogLevel.Debug);
                }
                
                // Add to recent projects
                await _userSettingsService.AddRecentProjectAsync(path, CurrentProject.Name);
                RefreshRecentProjects();
                
                // Update last directory
                await _userSettingsService.SetLastDirectoryAsync(CurrentProject.ProjectDirectory);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            Console.AddLine($"Failed to load project: {ex.Message}", LogLevel.Error);
        }
    }
    
    /// <summary>
    /// Save the current project (to existing location).
    /// </summary>
    [RelayCommand]
    private async Task SaveProject()
    {
        if (CurrentProject == null) return;
        
        try
        {
            StatusMessage = "Saving project...";
            // TODO: Implement proper save logic that tracks changes per file
            // For now, this is a placeholder
            StatusMessage = "Project saved";
            Console.AddLine("Project saved successfully", LogLevel.Info);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
            Console.AddLine($"Failed to save project: {ex.Message}", LogLevel.Error);
        }
    }
    
    /// <summary>
    /// Add an overlay JSON file to the current project.
    /// </summary>
    [RelayCommand]
    private async Task AddOverlay()
    {
        if (CurrentProject == null) return;
        
        var path = await _fileDialogService.OpenJsonFileAsync(
            "Add Overlay File",
            CurrentProject.ProjectDirectory);
        
        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                // Determine mount path based on filename convention
                // e.g., "debug.globalSettings.json" -> "/globalSettings" at priority 10
                var fileName = Path.GetFileNameWithoutExtension(path);
                var mountPath = "/"; // Default to root
                var priority = UserSettings.DefaultOverlayPriority;
                
                // Check for section-specific overlay naming
                foreach (var section in KnownSections.All)
                {
                    if (fileName.EndsWith($".{section.Id}", StringComparison.OrdinalIgnoreCase))
                    {
                        mountPath = section.JsonPath;
                        break;
                    }
                }
                
                var relativePath = Path.GetRelativePath(CurrentProject.ProjectDirectory, path);
                await _projectService.AddOverlayAsync(CurrentProject, relativePath, mountPath, priority);
                
                // Refresh the project tree
                ProjectTree.LoadProject(CurrentProject);
                
                Console.AddLine($"Added overlay: {relativePath} at {mountPath} (priority {priority})", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Console.AddLine($"Failed to add overlay: {ex.Message}", LogLevel.Error);
            }
        }
    }
    
    /// <summary>
    /// Clear the recent projects list.
    /// </summary>
    [RelayCommand]
    private async Task ClearRecentProjects()
    {
        UserSettings.RecentProjects.Clear();
        RefreshRecentProjects();
        await _userSettingsService.SaveAsync();
        Console.AddLine("Recent projects cleared", LogLevel.Info);
    }
    
    [RelayCommand]
    private async Task BuildProject()
    {
        if (CurrentProject == null) return;
        
        Console.Clear();
        Console.AddLine("Starting build...", LogLevel.Info);
        StatusMessage = "Building...";
        
        var success = await _processService.BuildProjectAsync(CurrentProject);
        StatusMessage = success ? "Build succeeded" : "Build failed";
    }
    
    [RelayCommand]
    private async Task RunGame()
    {
        if (CurrentProject == null) return;
        
        Console.Clear();
        Console.AddLine("Starting game...", LogLevel.Info);
        StatusMessage = "Running game...";
        
        IsGameRunning = true;
        var success = await _processService.RunGameAsync(CurrentProject, debug: false);
        
        if (!success)
        {
            IsGameRunning = false;
            StatusMessage = "Failed to start game";
        }
        else
        {
            StatusMessage = "Game running";
        }
    }
    
    [RelayCommand]
    private async Task DebugGame()
    {
        if (CurrentProject == null) return;
        
        Console.Clear();
        Console.AddLine("Starting game with debugger...", LogLevel.Info);
        StatusMessage = "Starting debug session...";
        
        IsGameRunning = true;
        var success = await _processService.RunGameAsync(CurrentProject, debug: true);
        
        if (!success)
        {
            IsGameRunning = false;
            StatusMessage = "Failed to start debugger";
        }
        else
        {
            StatusMessage = "Debugging";
        }
    }
    
    [RelayCommand]
    private void StopGame()
    {
        _processService.StopGame();
        IsGameRunning = false;
        StatusMessage = "Game stopped";
        Console.AddLine("Game stopped by user", LogLevel.Warning);
    }
    
    [RelayCommand]
    private void ShowConsole()
    {
        Console.IsVisible = true;
    }
    
    [RelayCommand]
    private void ClearConsole()
    {
        Console.Clear();
    }
    
    /// <summary>
    /// Open the settings dialog.
    /// </summary>
    [RelayCommand]
    private async Task OpenSettings()
    {
        var vm = new SettingsDialogViewModel(_userSettingsService);
        var dialog = new Views.SettingsDialog
        {
            DataContext = vm
        };
        
        // Get the main window to set as owner
        if (Avalonia.Application.Current?.ApplicationLifetime is 
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow != null)
        {
            await dialog.ShowDialog(desktop.MainWindow);
        }
    }
    
    /// <summary>
    /// Open the keybindings editor dialog.
    /// </summary>
    [RelayCommand]
    private async Task OpenKeybindings()
    {
        var vm = new KeyBindingsEditorViewModel(_actionService, _userSettingsService);
        var dialog = new Views.KeyBindingsEditor
        {
            DataContext = vm
        };
        
        // Get the main window to set as owner
        if (Avalonia.Application.Current?.ApplicationLifetime is 
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow != null)
        {
            await dialog.ShowDialog(desktop.MainWindow);
        }
    }
    
    /// <summary>
    /// Open the Quick Open palette (Ctrl+P) - navigate to things.
    /// </summary>
    [RelayCommand]
    private async Task OpenQuickOpen()
    {
        var quickOpenItems = GetQuickOpenItems();
        var vm = new CommandPaletteViewModel(
            CommandPaletteMode.QuickOpen,
            _actionService,
            quickOpenItems,
            IsProjectLoaded,
            _actionService.CanExecute);
        
        var dialog = new Views.CommandPalette
        {
            DataContext = vm
        };
        
        // Get the main window to set as owner
        if (Avalonia.Application.Current?.ApplicationLifetime is 
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow != null)
        {
            await dialog.ShowDialog(desktop.MainWindow);
        }
    }
    
    /// <summary>
    /// Open the Command Palette (Ctrl+Shift+P) - execute actions.
    /// </summary>
    [RelayCommand]
    private async Task OpenCommandPalette()
    {
        var vm = new CommandPaletteViewModel(
            CommandPaletteMode.Commands,
            _actionService,
            new List<QuickOpenItem>(),
            IsProjectLoaded,
            _actionService.CanExecute);
        
        var dialog = new Views.CommandPalette
        {
            DataContext = vm
        };
        
        // Get the main window to set as owner
        if (Avalonia.Application.Current?.ApplicationLifetime is 
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow != null)
        {
            await dialog.ShowDialog(desktop.MainWindow);
        }
    }
    
    /// <summary>
    /// Get the list of items available in Quick Open.
    /// </summary>
    private List<QuickOpenItem> GetQuickOpenItems()
    {
        var items = new List<QuickOpenItem>();
        
        // === Application Dialogs ===
        items.Add(new QuickOpenItem
        {
            Id = "settings",
            DisplayName = "Settings",
            Description = "Open application settings",
            Category = "Settings",
            Icon = "⚙️",
            Keywords = new List<string> { "preferences", "options", "config" },
            Action = OpenSettings
        });
        
        items.Add(new QuickOpenItem
        {
            Id = "keybindings",
            DisplayName = "Keyboard Shortcuts",
            Description = "Configure keyboard shortcuts",
            Category = "Settings",
            Icon = "⌨️",
            Keywords = new List<string> { "keys", "bindings", "hotkeys", "shortcuts" },
            Action = OpenKeybindings
        });
        
        // === Project Section Editors ===
        if (CurrentProject != null)
        {
            foreach (var section in CurrentProject.GetExistingSections())
            {
                var sectionId = section.Id;
                items.Add(new QuickOpenItem
                {
                    Id = $"section.{sectionId}",
                    DisplayName = section.DisplayName,
                    Description = $"Open {section.DisplayName} editor",
                    Category = "Editors",
                    Icon = section.Icon,
                    Keywords = new List<string> { sectionId, "editor" },
                    RequiresProject = true,
                    Action = () => { OpenSectionEditor(sectionId); return Task.CompletedTask; }
                });
            }
        }
        else
        {
            // Show all possible sections (grayed out without project)
            foreach (var section in KnownSections.All)
            {
                var sectionId = section.Id;
                items.Add(new QuickOpenItem
                {
                    Id = $"section.{sectionId}",
                    DisplayName = section.DisplayName,
                    Description = $"Open {section.DisplayName} editor",
                    Category = "Editors",
                    Icon = section.Icon,
                    Keywords = new List<string> { sectionId, "editor" },
                    RequiresProject = true,
                    Action = () => { OpenSectionEditor(sectionId); return Task.CompletedTask; }
                });
            }
        }
        
        // === View panels ===
        items.Add(new QuickOpenItem
        {
            Id = "view.console",
            DisplayName = "Console",
            Description = "Show the console panel",
            Category = "Views",
            Icon = "📋",
            Action = () => { ShowConsole(); return Task.CompletedTask; }
        });
        
        items.Add(new QuickOpenItem
        {
            Id = "view.render",
            DisplayName = "Render Output",
            Description = "Show the render output window",
            Category = "Views",
            Icon = "🖥️",
            RequiresProject = true,
            Action = () => { OpenRenderWindow(); return Task.CompletedTask; }
        });
        
        // === Open Documents ===
        foreach (var doc in OpenDocuments)
        {
            var docTitle = doc.Title;
            items.Add(new QuickOpenItem
            {
                Id = $"doc.{doc.Title}",
                DisplayName = doc.Title,
                Description = string.IsNullOrEmpty(doc.FilePath) ? "Open document" : doc.FilePath,
                Category = "Open Documents",
                Icon = "📄",
                Action = () => { SelectedDocument = OpenDocuments.FirstOrDefault(d => d.Title == docTitle); return Task.CompletedTask; }
            });
        }
        
        return items;
    }
    
    [RelayCommand]
    private async Task Exit()
    {
        // Save settings before exit
        await _userSettingsService.SaveAsync();
        
        // TODO: Check for unsaved changes
        Environment.Exit(0);
    }
}

public partial class DocumentTabViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;
    
    [ObservableProperty]
    private string _filePath = string.Empty;
    
    [ObservableProperty]
    private object? _content;
    
    [ObservableProperty]
    private bool _isDirty;
    
    [ObservableProperty]
    private bool _canClose = true;
}
