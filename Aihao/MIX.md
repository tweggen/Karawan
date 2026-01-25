# Mix - Karawan Engine Configuration System

## Overview

The `Mix` class (`engine.casette.Mix`) is the central configuration manager for the Karawan engine. It implements a **layered JSON configuration system** where multiple JSON files can be merged together, with later/higher-priority layers overriding earlier ones.

This is conceptually similar to:
- Visual Studio Solution (.sln) containing Project files (.csproj)
- CSS cascade where later rules override earlier ones
- Docker multi-stage builds with layered filesystems

## Core Concepts

### 1. Root Project File

The root project file (e.g., `nogame.json`) serves as the entry point and defines the project structure through `__include__` directives:

```json
{
  "resources": { "__include__": "nogame.resources.json" },
  "globalSettings": { "__include__": "nogame.globalSettings.json" },
  "defaults": {
    "loader": {
      "assembly": "nogame.dll"
    }
  },
  "implementations": { "__include__": "nogame.implementations.json" },
  "mapProviders": { "__include__": "nogame.mapProviders.json" },
  "metaGen": { "__include__": "nogame.metaGen.json" },
  "modules": { "__include__": "nogame.modules.json" },
  "properties": { "__include__": "nogame.properties.json" },
  "quests": { "__include__": "nogame.quests.json" },
  "layers": { "__include__": "nogame.layers.json" },
  "scenes": { "__include__": "nogame.scenes.json" },
  "textures": { "__include__": "nogame.textures.json" },
  "animations": { "__include__": "nogame.animations.json" }
}
```

### 2. Include Mechanism (`__include__`)

Any JSON object can contain an `__include__` property with a relative file path. When Mix processes the configuration:

1. It walks the entire JSON tree looking for `__include__` properties
2. For each include found, it loads the referenced file
3. The included content is **upserted** (merged) at that path location
4. Includes can be nested (included files can have their own includes)

**Key behavior:**
- Include paths are relative to the Mix's `Directory` property
- Files are loaded via `engine.Assets` (the asset management system)
- Missing include files generate warnings but don't fail loading
- The `AdditionalFiles` HashSet tracks all included file paths

### 3. View and Merging

The `View` class handles the actual storage and merging of JSON fragments:

**Path System:**
- All paths use JSON Pointer-like syntax: `/settings/theme`, `/modules/root`
- Paths must start with `/`

**Priority System:**
- Each fragment has a priority (integer)
- Lower priority values are applied first
- Higher priority values override lower ones
- Same priority: later version wins

**Merge Rules:**
- **Objects**: Properties are merged recursively; overlapping keys use overlay value
- **Arrays**: Completely replaced (not merged element-by-element)
- **Primitives**: Completely replaced

### 4. Subscription System

Clients can subscribe to changes at specific paths:

```csharp
var subscription = view.Subscribe("/globalSettings", (evt) => {
    // evt.Kind: Added, Removed, Modified
    // evt.NewNode, evt.OldNode
    // evt.Path, evt.Timestamp
});

// Later: subscription.Dispose() to unsubscribe
```

## Class Structure

### Mix.cs

```
engine.casette.Mix
├── Directory: string              // Base directory for resolving includes
├── AdditionalFiles: HashSet<string>  // All included file paths
├── _view: View                    // Internal merged configuration store
│
├── GetTree(path): JsonNode?       // Get merged subtree at path
├── GetTree(path, action)          // Get with callback
├── UpsertFragment(path, element, priority)  // Add/update configuration
├── RemoveFragment(path, priority) // Remove configuration layer
│
└── _upsertIncludes()              // Process __include__ directives
```

### View.cs

```
engine.casette.View
├── _partialsByPath: Dictionary<string, SortedSet<PartialTree>>
├── _mergeCache: Dictionary<string, CacheEntry>
├── _subscribers: Dictionary<string, List<Action<DomChangeEvent>>>
│
├── Upsert(path, element, priority)    // Add/update partial
├── Remove(path, priority?)            // Remove partial(s)
├── Subscribe(path, handler)           // Subscribe to changes
├── GetMergedSubtree(path)             // Get computed merged result
│
└── ComputeMergedSubtree()             // Internal merge algorithm
```

### Loader.cs

The `Loader` class orchestrates loading a complete game configuration:

```
engine.casette.Loader
├── _mix: Mix                      // The Mix instance
├── _jeRoot: JsonElement           // Root JSON document
│
├── InterpretConfig()              // Process loaded configuration
├── StartGame()                    // Initialize and start root module
├── WhenLoaded(path, callback)     // Register for load completion
└── CreateFactoryMethod()          // Create object instances from config
```

## Standard Configuration Sections

Based on `nogame.json`, these are the typical top-level sections:

| Section | Purpose |
|---------|---------|
| `defaults` | Loader assembly configuration |
| `globalSettings` | Engine and game settings (key-value pairs) |
| `modules` | Module definitions including root module |
| `resources` | Asset definitions |
| `implementations` | Factory method definitions |
| `mapProviders` | Map generation providers |
| `metaGen` | Procedural generation operators |
| `properties` | Game-specific properties |
| `quests` | Quest definitions |
| `layers` | Rendering layers |
| `scenes` | Scene definitions |
| `textures` | Texture definitions |
| `animations` | Animation definitions |

## File Hierarchy Example

```
models/
├── nogame.json                    # Root project file
├── nogame.globalSettings.json     # Global settings
├── nogame.modules.json            # Module definitions
├── nogame.resources.json          # Resource definitions
├── nogame.implementations.json    # Factory implementations
├── nogame.metaGen.json            # Procedural generation config
├── nogame.properties.json         # Game properties
├── nogame.quests.json             # Quest definitions
├── nogame.layers.json             # Rendering layers
├── nogame.scenes.json             # Scene definitions
├── nogame.textures.json           # Texture definitions
├── nogame.animations.json         # Animation definitions
└── [subdirectories with assets]
```

## Aihao Architecture

Aihao models the Mix system with explicit support for sections and overlay layers.

### Section Definitions (Compile-Time)

Known sections are predefined in `KnownSections.cs`:

| Section ID | JSON Path | Display Name |
|------------|-----------|-------------|
| globalSettings | /globalSettings | Global Settings |
| modules | /modules | Modules |
| resources | /resources | Resources |
| implementations | /implementations | Implementations |
| mapProviders | /mapProviders | Map Providers |
| metaGen | /metaGen | MetaGen |
| properties | /properties | Properties |
| quests | /quests | Quests |
| layers | /layers | Layers |
| scenes | /scenes | Scenes |
| textures | /textures | Textures |
| animations | /animations | Animations |
| defaults | /defaults | Defaults |

### Layer System (Runtime)

Each section can have multiple layers, similar to how Mix handles priorities:

```
SectionState
├── Definition: SectionDefinition  // The predefined section
└── Layers: List<SectionLayer>     // Ordered by priority
    └── SectionLayer
        ├── Priority: int          // 0 = base, higher = overlay
        ├── FilePath: string?      // Source file (null = inline)
        ├── IsActive: bool         // Can toggle on/off
        └── IsOverlay: bool        // User-added vs discovered
```

### Overlay Use Cases

1. **Debug Configuration**: Add `debug.globalSettings.json` at priority 10
2. **Platform Overrides**: Add `windows.properties.json` for platform-specific settings
3. **User Preferences**: Add `local.globalSettings.json` (gitignored) for personal settings
4. **Mod Support**: Add mod files as high-priority overlays

### Write Target Resolution

When saving changes to a section:
1. Find the topmost (highest priority) active layer
2. Write changes to that layer's file
3. If layer is inline (FilePath = null), write to root file

```csharp
// Get where changes to globalSettings should be saved
var targetPath = project.GetWriteTargetPath("globalSettings");
// Returns "debug.globalSettings.json" if overlay is active,
// or "nogame.globalSettings.json" for base layer
```

## Thread Safety

- `View` uses `ReaderWriterLockSlim` for thread-safe access
- Cache invalidation is automatic on updates
- Subscribers are called with copies to avoid lock contention

## Key Implementation Details

### Include Resolution Flow

```
1. UpsertFragment(path, jsonElement)
   ↓
2. _preprocessUpsert() - adds to view
   ↓
3. _upsertIncludes() - walks tree for __include__
   ↓
4. For each __include__ found:
   a. Resolve path: Directory + includePath
   b. Check if file exists via Assets
   c. Load and parse JSON
   d. Recursively upsert at the include's path
   e. Track in AdditionalFiles
```

### Merge Algorithm

```
ComputeMergedSubtree(requestedPath):
1. Find all relevant partials:
   - Exact matches (path == requestedPath)
   - Ancestors (partial is parent of requested)
   - Descendants (partial is child of requested)

2. Sort by (priority ASC, version ASC)

3. For each partial in order:
   - Ancestor: Extract subtree, merge overlay
   - Exact: Extract subtree, merge overlay
   - Descendant: Ensure path exists, assign overlay

4. Return merged result (cached)
```
