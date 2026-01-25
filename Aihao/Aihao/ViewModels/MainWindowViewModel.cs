using System;
using System.Collections.ObjectModel;
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
            Title = System.IO.Path.GetFileName(relativePath),
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
    
    [RelayCommand]
    private async Task OpenProject()
    {
        // TODO: Show file dialog to select project file
        // For now, hardcoded path for testing
        var projectPath = "nogame.json";
        await LoadProject(projectPath);
    }
    
    [RelayCommand]
    private async Task OpenRecentProject(string path)
    {
        await LoadProject(path);
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
                
                // Log existing sections
                Console.AddLine($"  Sections:", LogLevel.Debug);
                foreach (var section in CurrentProject.GetExistingSections())
                {
                    Console.AddLine($"    {section.DisplayName}", LogLevel.Debug);
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            Console.AddLine($"Failed to load project: {ex.Message}", LogLevel.Error);
        }
    }
    
    [RelayCommand]
    private async Task SaveProject()
    {
        if (CurrentProject == null) return;
        
        try
        {
            StatusMessage = "Saving project...";
            // TODO: Implement proper save logic
            // This requires tracking which changes belong to which file
            StatusMessage = "Project saved";
            Console.AddLine("Project saved successfully", LogLevel.Info);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
            Console.AddLine($"Failed to save project: {ex.Message}", LogLevel.Error);
        }
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
    private void Exit()
    {
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
