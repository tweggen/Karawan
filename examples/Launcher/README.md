# Karawan Generic Launcher

A game-agnostic desktop launcher for any Karawan engine game.

## Key Feature: Dynamic Game Loading

This launcher has **no compile-time dependencies** on any game project. Instead, it:

1. Reads `game.launch.json` to find the game configuration
2. Pre-loads the game assembly (DLL) at runtime
3. Uses the `casette.Loader` to dynamically instantiate the root module

This means **the same launcher binary can run any Karawan game**.

## Usage

### Running a Game

1. Build your game project to produce `yourgame.dll`
2. Copy `yourgame.dll` to the launcher's output directory
3. Create a `models/` folder with:
   - `game.launch.json` - points to your game config and assembly
   - `yourgame.json` - your game's configuration
4. Run the launcher

### Configuration

#### game.launch.json
```json
{
  "game": {
    "configPath": "yourgame.json",
    "assembly": "yourgame.dll"
  },
  "branding": {
    "vendor": "Your Company",
    "appName": "yourgame",
    "windowTitle": "Your Game Title"
  },
  "platform": {
    "createOSD": "false",
    "createUI": "true"
  }
}
```

#### yourgame.json
```json
{
  "defaults": {
    "loader": {
      "assembly": "yourgame.dll"
    }
  },
  "modules": {
    "root": {
      "className": "YourNamespace.Main"
    }
  },
  "globalSettings": {
    "list": [
      { "key": "nogame.framebuffer.resolution", "value": "1280x720" }
    ]
  }
}
```

## Assembly Search Paths

The launcher looks for game assemblies in these locations (in order):

1. Launcher's base directory (`bin/Debug/net9.0/`)
2. Resource path (where `game.launch.json` is)
3. Parent of resource path
4. `../bin/Debug/net9.0/` relative to resource path
5. The assembly name as-is (system search)

## Building

```bash
cd examples/Launcher
dotnet build
```

## Running the Grid Example

```bash
# Build the grid example
cd examples/grid
dotnet build

# Copy grid.dll to the launcher
cp bin/Debug/net9.0/grid.dll ../Launcher/bin/Debug/net9.0/

# Run from the grid directory (so it finds the models folder)
cd ../Launcher/bin/Debug/net9.0
./Karawan.GenericLauncher
```

Or set up a symbolic link / copy the models folder to the launcher output.

## Project Structure

```
examples/
├── Launcher/
│   ├── Karawan.GenericLauncher.csproj  # No game references!
│   ├── DesktopMain.cs                   # Entry point
│   └── AssetImplementation.cs           # Asset loader
└── grid/
    ├── grid.csproj                      # Example game
    ├── Main.cs
    ├── Scene.cs
    └── models/
        ├── game.launch.json
        └── grid.json
```
