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
        // Open the appropriate editor based on file type or node type
        if (file.NodeType == "globalSettings" && CurrentProject?.RootDocument != null)
        {
            OpenGlobalSettingsEditor();
        }
        else if (file.NodeType == "metagen" && CurrentProject?.RootDocument != null)
        {
            OpenMetagenEditor();
        }
        else if (file.NodeType == "resources" && CurrentProject?.RootDocument != null)
        {
            OpenResourcesEditor();
        }
        else if (file.IsFile)
        {
            OpenFileInEditor(file.FullPath);
        }
    }
    
    [RelayCommand]
    private void OpenGlobalSettingsEditor()
    {
        if (CurrentProject?.RootDocument?["globalSettings"] is JsonObject settings)
        {
            GlobalSettings.LoadFromJson(settings);
            OpenOrFocusDocument("Global Settings", GlobalSettings);
        }
    }
    
    [RelayCommand]
    private void OpenMetagenEditor()
    {
        if (CurrentProject?.RootDocument?["metagen"] is JsonObject metagen)
        {
            Metagen.LoadFromJson(metagen);
            OpenOrFocusDocument("Metagen", Metagen);
        }
    }
    
    [RelayCommand]
    private void OpenResourcesEditor()
    {
        if (CurrentProject?.RootDocument?["resources"] is JsonArray resources)
        {
            Resources.LoadFromJson(resources);
            OpenOrFocusDocument("Resources", Resources);
        }
    }
    
    [RelayCommand]
    private void OpenRenderWindow()
    {
        OpenOrFocusDocument("Render Output", RenderOutput);
    }
    
    private void OpenFileInEditor(string path)
    {
        // Check if already open
        foreach (var doc in OpenDocuments)
        {
            if (doc.FilePath == path)
            {
                SelectedDocument = doc;
                return;
            }
        }
        
        // Create new document tab
        var tab = new DocumentTabViewModel
        {
            Title = System.IO.Path.GetFileName(path),
            FilePath = path
        };
        
        OpenDocuments.Add(tab);
        SelectedDocument = tab;
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
                Console.AddLine($"  Path: {CurrentProject.ProjectPath}", LogLevel.Debug);
                Console.AddLine($"  Files: {CurrentProject.Files.Count}", LogLevel.Debug);
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
            await _projectService.SaveProjectAsync(CurrentProject);
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
        // Ensure console is visible
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
