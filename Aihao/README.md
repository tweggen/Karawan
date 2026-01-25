# Aihao - Karawan Engine Editor

Aihao is a cross-platform desktop IDE/editor for the Karawan game engine, built with Avalonia UI.

## Features

- **Project Tree View**: Browse all project files from `nogame.json` project files
- **Dockable Editor Windows**: Multiple editors for different JSON sections
  - Global Settings Editor
  - Properties Editor  
  - Metagen Configuration Editor
  - Resource List Editor
- **Application Launcher**: Run games with optional debugger integration
  - JetBrains Rider integration
  - Visual Studio integration
  - VS Code integration
- **Console Window**: Filterable log output with search
- **OpenGL Render Window**: Placeholder for tool visualization (connect to Joyce/Splash)

## Requirements

- .NET 8.0 SDK
- Windows, Linux, or macOS

## Building

```bash
cd Aihao
dotnet restore
dotnet build
```

## Running

```bash
dotnet run --project Aihao/Aihao.csproj
```

## Project Structure

```
Aihao/
├── Aihao.sln                 # Solution file
└── Aihao/
    ├── Aihao.csproj          # Project file
    ├── Program.cs            # Entry point
    ├── App.axaml(.cs)        # Application definition
    ├── Models/
    │   └── AihaoProject.cs   # Project model
    ├── ViewModels/
    │   ├── MainWindowViewModel.cs
    │   ├── ProjectTreeViewModel.cs
    │   ├── ConsoleWindowViewModel.cs
    │   ├── GlobalSettingsEditorViewModel.cs
    │   ├── PropertiesEditorViewModel.cs
    │   ├── ResourceListEditorViewModel.cs
    │   ├── MetagenEditorViewModel.cs
    │   ├── OpenGLWindowViewModel.cs
    │   └── Converters.cs
    ├── Views/
    │   ├── MainWindow.axaml(.cs)
    │   ├── ProjectTreeView.axaml(.cs)
    │   ├── ConsoleWindow.axaml(.cs)
    │   ├── GlobalSettingsEditor.axaml(.cs)
    │   ├── PropertiesEditor.axaml(.cs)
    │   ├── ResourceListEditor.axaml(.cs)
    │   ├── MetagenEditor.axaml(.cs)
    │   └── OpenGLWindow.axaml(.cs)
    └── Services/
        ├── ProjectService.cs   # Project loading/saving
        ├── ProcessService.cs   # Game launching/debugging
        └── DockingService.cs   # Window management
```

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+O | Open Project |
| Ctrl+S | Save Project |
| Ctrl+Shift+B | Build Project |
| F5 | Run Game |
| Ctrl+F5 | Debug Game |
| Shift+F5 | Stop Game |

## TODO

- [ ] Integrate with Joyce/Splash for actual OpenGL rendering
- [ ] Implement file dialogs for project opening
- [ ] Add recent projects list
- [ ] Implement undo/redo system
- [ ] Add syntax highlighting for code files
- [ ] Implement drag-and-drop for resources
- [ ] Add asset preview panel
- [ ] Implement project build system

## License

Part of the Karawan engine project.
