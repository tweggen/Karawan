# Karawan Generic Launcher

A game-agnostic launcher that can run any Karawan game through dynamic assembly loading.

## How It Works

The launcher:
1. **Detects the project root** - If running from a build output directory (e.g., `bin/Debug/net9.0/`), it automatically navigates back to the project root
2. **Finds the game config** - Looks for `game.launch.json` in standard locations (`models/`, `./`)
3. **Loads game assemblies dynamically** - No compile-time dependency on any game project
4. **Starts the game** - Instantiates the root module specified in the config

## Resource Path Detection

The launcher uses smart path detection to handle various launch scenarios:

### Build Output Detection
If the current working directory matches a .NET build output pattern:
- `*/bin/Debug/net*.*/`
- `*/bin/Release/net*.*/`
- `*/bin/Debug/net*.*/win-x64/` (or other RID)
- `*/bin/Release/net*.*/osx-arm64/` (or other RID)

The launcher strips this suffix to find the project root.

### Search Order
1. Project root (if detected) + `models/game.launch.json`
2. Project root (if detected) + `./game.launch.json`
3. CWD + `models/game.launch.json`
4. CWD + `./game.launch.json`
5. Fallback to `./models/`

## Configuration Files

### game.launch.json
Located in the `models/` directory of your game project:

```json
{
  "branding": {
    "windowTitle": "My Game"
  },
  "game": {
    "configPath": "mygame.json",
    "assembly": "mygame.dll"
  }
}
```

### Game Config (e.g., mygame.json)
Standard Karawan game configuration with modules, resources, etc.

## Usage

### From IDE (Rider/Visual Studio)
1. Set working directory to your game project (e.g., `examples/grid/`)
2. Run the launcher executable
3. The launcher detects it's in a build dir and finds your game config

### From Command Line
```bash
cd examples/grid
../../examples/Launcher/bin/Debug/net9.0/Karawan.GenericLauncher
```

### With Run Configuration
Create a run configuration that:
1. Builds the game project first (copies DLL to launcher output)
2. Builds the launcher
3. Sets working directory to the game project
4. Runs the launcher executable

## Example: Grid Project

The grid example demonstrates basic setup:

```
examples/grid/
├── grid.csproj          # Game project (builds grid.dll)
├── Main.cs              # Entry point (IModule)
├── Scene.cs             # Scene implementation
├── models/
│   ├── game.launch.json # Launch config
│   └── grid.json        # Game config
└── README.md
```

The launcher, when run from `examples/grid/`, will:
1. Detect CWD is not a build directory
2. Find `models/game.launch.json`
3. Load `grid.dll` dynamically
4. Instantiate `grid.Main` as the root module
