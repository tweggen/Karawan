# Grid Example

A minimal Karawan engine example that displays a rotating cube with basic lighting.

## What it demonstrates

- Creating a simple 3D scene
- Setting up a camera
- Adding directional and ambient lights
- Creating and rendering a procedural mesh (cube)
- Basic scene animation (rotating cube)

## Project Structure

```
examples/
├── Launcher/                # Generic launcher (separate project)
│   └── Karawan.GenericLauncher.csproj
└── grid/
    ├── Main.cs              # Main game module
    ├── Scene.cs             # Scene with cube, camera, and lights
    ├── grid.csproj          # Game logic project
    └── models/
        ├── game.launch.json # Launch configuration
        └── grid.json        # Game configuration
```

## Building and Running

### Step 1: Build the grid game

```bash
cd examples/grid
dotnet build
```

This creates `grid.dll` in `bin/Debug/net9.0/`

### Step 2: Build the launcher

```bash
cd examples/Launcher
dotnet build
```

### Step 3: Copy the game DLL to the launcher output

```bash
cp ../grid/bin/Debug/net9.0/grid.dll bin/Debug/net9.0/
```

### Step 4: Run from the grid directory (to find models)

```bash
cd ../grid
../Launcher/bin/Debug/net9.0/Karawan.GenericLauncher
```

Or copy/symlink the `models` folder to the launcher's output directory.

## Scene Description

The example creates:
- A **1x1 meter gray cube** at the origin (0, 0, 0)
- A **camera** positioned at (3, 3, 5) looking at the origin
- A **directional light** angled from above
- A **dim ambient light** for fill

The cube rotates slowly around the Y axis for visual interest.

## Configuration Files

### game.launch.json
```json
{
  "game": {
    "configPath": "grid.json",
    "assembly": "grid.dll"
  },
  "branding": {
    "vendor": "Karawan Examples",
    "appName": "grid",
    "windowTitle": "Karawan Grid Example"
  }
}
```

### grid.json
```json
{
  "defaults": {
    "loader": {
      "assembly": "grid.dll"
    }
  },
  "modules": {
    "root": {
      "className": "grid.Main"
    }
  }
}
```

## Customization Ideas

- Change the cube color in `Scene._createCubeInstanceDesc()`
- Adjust camera position and angle
- Add more geometric primitives
- Implement keyboard controls for camera movement
