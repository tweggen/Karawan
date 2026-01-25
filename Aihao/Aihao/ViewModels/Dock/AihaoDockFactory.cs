using System;
using System.Collections.Generic;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;

namespace Aihao.ViewModels.Docking;

/// <summary>
/// Factory that creates the docking layout for Aihao.
/// </summary>
public class AihaoDockFactory : Factory
{
    private readonly ProjectTreeViewModel _projectTree;
    private readonly ConsoleWindowViewModel _console;
    private IRootDock? _rootDock;
    private IDocumentDock? _documentDock;
    
    public AihaoDockFactory(ProjectTreeViewModel projectTree, ConsoleWindowViewModel console)
    {
        _projectTree = projectTree;
        _console = console;
    }
    
    /// <summary>
    /// Gets the document dock where editors are opened.
    /// </summary>
    public IDocumentDock? DocumentDock => _documentDock;
    
    /// <summary>
    /// Gets the root dock.
    /// </summary>
    public IRootDock? RootDock => _rootDock;
    
    public override IRootDock CreateLayout()
    {
        // Create tool windows
        var projectTreeTool = new ProjectTreeToolViewModel(_projectTree);
        var consoleTool = new ConsoleToolViewModel(_console);
        var inspectorTool = new InspectorToolViewModel();
        
        // Create the document dock (center area for editors)
        _documentDock = new DocumentDock
        {
            Id = "DocumentsPane",
            Title = "Documents",
            Proportion = double.NaN,
            IsCollapsable = false,
            CanCreateDocument = false,
            VisibleDockables = CreateList<IDockable>()
        };
        
        // Left tool dock (Project Tree)
        var leftDock = new ToolDock
        {
            Id = "LeftPane",
            Title = "Left",
            Proportion = 0.2,
            VisibleDockables = CreateList<IDockable>(projectTreeTool),
            Alignment = Alignment.Left,
            GripMode = GripMode.Visible
        };
        
        // Right tool dock (Inspector)
        var rightDock = new ToolDock
        {
            Id = "RightPane", 
            Title = "Right",
            Proportion = 0.2,
            VisibleDockables = CreateList<IDockable>(inspectorTool),
            Alignment = Alignment.Right,
            GripMode = GripMode.Visible
        };
        
        // Bottom tool dock (Console)
        var bottomDock = new ToolDock
        {
            Id = "BottomPane",
            Title = "Bottom",
            Proportion = 0.25,
            VisibleDockables = CreateList<IDockable>(consoleTool),
            Alignment = Alignment.Bottom,
            GripMode = GripMode.Visible
        };
        
        // Create the main layout structure
        // Layout: Left | (Center / Bottom) | Right
        
        // Center area with documents on top, console on bottom
        var centerBottomLayout = new ProportionalDock
        {
            Id = "CenterBottomLayout",
            Proportion = double.NaN,
            Orientation = Orientation.Vertical,
            VisibleDockables = CreateList<IDockable>(
                _documentDock,
                new ProportionalDockSplitter(),
                bottomDock
            )
        };
        
        // Main horizontal layout: Left | Center | Right
        var mainLayout = new ProportionalDock
        {
            Id = "MainLayout",
            Proportion = double.NaN,
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(
                leftDock,
                new ProportionalDockSplitter(),
                centerBottomLayout,
                new ProportionalDockSplitter(),
                rightDock
            )
        };
        
        // Root dock
        _rootDock = new RootDock
        {
            Id = "Root",
            Title = "Root",
            IsCollapsable = false,
            VisibleDockables = CreateList<IDockable>(mainLayout),
            ActiveDockable = mainLayout,
            DefaultDockable = mainLayout
        };
        
        return _rootDock;
    }
    
    public override void InitLayout(IDockable layout)
    {
        ContextLocator = new Dictionary<string, Func<object?>>
        {
            ["ProjectTree"] = () => _projectTree,
            ["Console"] = () => _console,
        };
        
        DockableLocator = new Dictionary<string, Func<IDockable?>>
        {
            ["Root"] = () => _rootDock,
            ["DocumentsPane"] = () => _documentDock,
        };
        
        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new HostWindow()
        };
        
        base.InitLayout(layout);
    }
    
    /// <summary>
    /// Add a document to the document dock.
    /// </summary>
    public void AddDocument(IDockable document)
    {
        if (_documentDock?.VisibleDockables != null)
        {
            // Check if already open
            foreach (var existing in _documentDock.VisibleDockables)
            {
                if (existing.Id == document.Id)
                {
                    // Focus existing
                    _documentDock.ActiveDockable = existing;
                    return;
                }
            }
            
            AddDockable(_documentDock, document);
            _documentDock.ActiveDockable = document;
        }
    }
    
    /// <summary>
    /// Close a document by ID.
    /// </summary>
    public void CloseDocument(string documentId)
    {
        if (_documentDock?.VisibleDockables != null)
        {
            foreach (var dockable in _documentDock.VisibleDockables)
            {
                if (dockable.Id == documentId)
                {
                    RemoveDockable(dockable, collapse: true);
                    break;
                }
            }
        }
    }
}
