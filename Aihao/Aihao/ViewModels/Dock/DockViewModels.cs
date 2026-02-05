using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;

namespace Aihao.ViewModels.Docking;

/// <summary>
/// Base class for all dockable tool windows (Project Tree, Console, Inspector, etc.)
/// </summary>
public partial class ToolViewModel : Tool
{
    [ObservableProperty]
    private string _icon = "📄";
}

/// <summary>
/// Base class for all dockable document windows (editors opened in tabs)
/// </summary>
public partial class DocumentViewModel : Document
{
    [ObservableProperty]
    private string _icon = "📄";
    
    [ObservableProperty]
    private bool _isDirty;
    
    [ObservableProperty]
    private object? _content;
}

/// <summary>
/// Project Tree tool window
/// </summary>
public partial class ProjectTreeToolViewModel : ToolViewModel
{
    public ProjectTreeViewModel ProjectTree { get; }
    
    public ProjectTreeToolViewModel(ProjectTreeViewModel projectTree)
    {
        ProjectTree = projectTree;
        Id = "ProjectTree";
        Title = "Project";
        Icon = "📁";
        CanClose = false;
        CanPin = true;
    }
}

/// <summary>
/// Console tool window
/// </summary>
public partial class ConsoleToolViewModel : ToolViewModel
{
    public ConsoleWindowViewModel Console { get; }
    
    public ConsoleToolViewModel(ConsoleWindowViewModel console)
    {
        Console = console;
        Id = "Console";
        Title = "Console";
        Icon = "📋";
        CanClose = false;
        CanPin = true;
    }
}

/// <summary>
/// Inspector tool window (shows properties of selected item)
/// </summary>
public partial class InspectorToolViewModel : ToolViewModel
{
    [ObservableProperty]
    private object? _selectedObject;
    
    [ObservableProperty]
    private string _selectedObjectName = "Nothing selected";
    
    public InspectorToolViewModel()
    {
        Id = "Inspector";
        Title = "Inspector";
        Icon = "🔍";
        CanClose = false;
        CanPin = true;
    }
}

/// <summary>
/// Document for Global Settings editor
/// </summary>
public partial class GlobalSettingsDocumentViewModel : DocumentViewModel
{
    public GlobalSettingsEditorViewModel Editor { get; }
    
    public GlobalSettingsDocumentViewModel(GlobalSettingsEditorViewModel editor)
    {
        Editor = editor;
        Content = editor;
        Id = "GlobalSettings";
        Title = "Global Settings";
        Icon = "⚙️";
        CanClose = true;
        CanPin = false;
    }
}

/// <summary>
/// Document for Properties editor (the /properties section, not Inspector)
/// </summary>
public partial class PropertiesDocumentViewModel : DocumentViewModel
{
    public PropertiesEditorViewModel Editor { get; }
    
    public PropertiesDocumentViewModel(PropertiesEditorViewModel editor)
    {
        Editor = editor;
        Content = editor;
        Id = "Properties";
        Title = "Properties";
        Icon = "📋";
        CanClose = true;
        CanPin = false;
    }
}

/// <summary>
/// Document for Resources editor
/// </summary>
public partial class ResourcesDocumentViewModel : DocumentViewModel
{
    public ResourceListEditorViewModel Editor { get; }
    
    public ResourcesDocumentViewModel(ResourceListEditorViewModel editor)
    {
        Editor = editor;
        Content = editor;
        Id = "Resources";
        Title = "Resources";
        Icon = "📦";
        CanClose = true;
        CanPin = false;
    }
}

/// <summary>
/// Document for Metagen editor
/// </summary>
public partial class MetagenDocumentViewModel : DocumentViewModel
{
    public MetagenEditorViewModel Editor { get; }
    
    public MetagenDocumentViewModel(MetagenEditorViewModel editor)
    {
        Editor = editor;
        Content = editor;
        Id = "Metagen";
        Title = "MetaGen";
        Icon = "🔧";
        CanClose = true;
        CanPin = false;
    }
}

/// <summary>
/// Document for Render Output
/// </summary>
public partial class RenderOutputDocumentViewModel : DocumentViewModel
{
    public OpenGLWindowViewModel Renderer { get; }
    
    public RenderOutputDocumentViewModel(OpenGLWindowViewModel renderer)
    {
        Renderer = renderer;
        Content = renderer;
        Id = "RenderOutput";
        Title = "Render Output";
        Icon = "🖥️";
        CanClose = true;
        CanPin = false;
    }
}

/// <summary>
/// Document for Implementations editor
/// </summary>
public partial class ImplementationsDocumentViewModel : DocumentViewModel
{
    public ImplementationsEditorViewModel Editor { get; }
    
    public ImplementationsDocumentViewModel(ImplementationsEditorViewModel editor)
    {
        Editor = editor;
        Content = editor;
        Id = "Implementations";
        Title = "Implementations";
        Icon = "🔌";
        CanClose = true;
        CanPin = false;
    }
}

/// <summary>
/// Document for Narration editor
/// </summary>
public partial class NarrationDocumentViewModel : DocumentViewModel
{
    public NarrationEditorViewModel Editor { get; }

    /// <summary>
    /// Character IDs available for speaker autocomplete.
    /// </summary>
    public IEnumerable<string> CharacterIds { get; }

    public NarrationDocumentViewModel(NarrationEditorViewModel editor, IEnumerable<string>? characterIds = null)
    {
        Editor = editor;
        CharacterIds = characterIds ?? Enumerable.Empty<string>();
        Content = editor;
        Id = "Narration";
        Title = "Narration";
        Icon = "🎭";
        CanClose = true;
        CanPin = false;
    }
}

/// <summary>
/// Document for Characters editor
/// </summary>
public partial class CharactersDocumentViewModel : DocumentViewModel
{
    public CharacterEditorViewModel Editor { get; }

    public CharactersDocumentViewModel(CharacterEditorViewModel editor)
    {
        Editor = editor;
        Content = editor;
        Id = "Characters";
        Title = "Characters";
        Icon = "👤";
        CanClose = true;
        CanPin = false;
    }
}

/// <summary>
/// Document for L-Systems editor
/// </summary>
public partial class LSystemsDocumentViewModel : DocumentViewModel
{
    public LSystem.LSystemEditorViewModel Editor { get; }

    public LSystemsDocumentViewModel(LSystem.LSystemEditorViewModel editor)
    {
        Editor = editor;
        Content = editor;
        Id = "LSystems";
        Title = "L-Systems";
        Icon = "🌳";
        CanClose = true;
        CanPin = false;
    }
}

/// <summary>
/// Generic document for JSON/text file editing
/// </summary>
public partial class FileDocumentViewModel : DocumentViewModel
{
    [ObservableProperty]
    private string _filePath = string.Empty;
    
    [ObservableProperty]
    private string _fileContent = string.Empty;
    
    public FileDocumentViewModel(string title, string filePath)
    {
        Id = $"File:{filePath}";
        Title = title;
        FilePath = filePath;
        Icon = "📄";
        CanClose = true;
        CanPin = false;
    }
}
