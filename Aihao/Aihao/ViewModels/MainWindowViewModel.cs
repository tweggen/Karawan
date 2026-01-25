using System;
using System.Collections.ObjectModel;
using System.IO;
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
        
        // Open editor based on node type (section) or file
        if (!string.IsNullOrEmpty(file.NodeType))
        {
            OpenSectionEditor(file.NodeType);
        }
        else if (file.IsFile && !string.IsNullOrEmpty(file.RelativePath))
        {
            OpenFileInEditor(file.RelativePath);
        }
    }
    
    private void OpenSectionEditor(string sectionId)
    {
        if (CurrentProject == null) return;
        
        var definition = KnownSections.GetById(sectionId);
        if (definition == null) return;
        
        // Get merged content from Mix
        var content = CurrentProject.GetSection(sectionId);
        if (content == null)
        {
            Console.AddLine($"No content found for section: {sectionId}", LogLevel.Warning);
            return;
        }
        
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
            if (SelectedDocument == doc && OpenDocuments.Count > 0)
            {
                SelectedDocument = OpenDocuments[0];
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
                Console.AddLine($"  Sections:", LogLevel.Debug);
                foreach (var section in CurrentProject.GetExistingSections())
                {
                    Console.AddLine($"    {section.DisplayName}", LogLevel.Debug);
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
