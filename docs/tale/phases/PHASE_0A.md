# Phase 0A — Testbed Infrastructure

**Prerequisites**: None (first step).
**Read also**: `REFERENCE.md` for codebase file locations.

---

## Goal

Create a console project that boots the engine headlessly, generates a cluster (streets, buildings, shops), and extracts a spatial model. No simulation yet — just world generation and data extraction.

## What To Build

### 1. Testbed Console Project

Create `Testbed/Testbed.csproj` referencing: Joyce, JoyceCode, nogame, nogameCode, Splash.Silk.

`TestbedMain.cs` bootstrap sequence (modeled on `Karawan/DesktopMain.cs`):

```
1.  GlobalSettings: set resource path, platform graphics
2.  Register TextureCatalogue
3.  Register casette.Loader with real nogame.json config
4.  AssetImplementation.WithLoader()
5.  Loader.InterpretConfig()
6.  Platform.EasyCreateHeadless() — creates Engine without window
7.  engine.ExecuteLogicalThreadOnly()
8.  engine.CallOnPlatformAvailable()
9.  Register ConsoleLogger
10. Skip: audio, window, icon
11. Loader.StartGame() with TestbedRootModule
```

### 2. TestbedRootModule

A stripped module tree. Activate only:
- `nogame.config.Module` — Mix config
- `nogame.modules.World` — MetaGen + Loader
- `engine.behave.SpawnController` — character spawning

Skip: ScreenComposer, InputEventPipeline, InputController, AutoSave, UI, audio, satnav rendering.

### 3. ClusterViewer

`ClusterViewer : IViewer` that loads all fragments covering a target cluster:

```csharp
public class ClusterViewer : IViewer
{
    private ClusterDesc _cluster;

    public void GetVisibleFragments(ref IList<FragmentVisibility> lsVisib)
    {
        var aabb = _cluster.AABB;
        int iMin = (int)MathF.Floor(aabb.AA.X / MetaGen.FragmentSize);
        int iMax = (int)MathF.Ceiling(aabb.BB.X / MetaGen.FragmentSize);
        int kMin = (int)MathF.Floor(aabb.AA.Z / MetaGen.FragmentSize);
        int kMax = (int)MathF.Ceiling(aabb.BB.Z / MetaGen.FragmentSize);

        for (int k = kMin; k <= kMax; k++)
            for (int i = iMin; i <= iMax; i++)
                lsVisib.Add(new() {
                    How = (byte)(FragmentVisibility.Visible3dNow),
                    I = (short)i, K = (short)k
                });
    }
}
```

Register with `Loader.AddViewer()` and call `WorldLoaderProvideFragments()` to generate all fragments. Keep the viewer registered permanently (prevents fragment purging).

Optionally disable visual-only operators via config conditions:
- `nogame.CreateTrees` → false
- `nogame.CreatePolytopes` → false
- `world.CreateStreetAnnotations` → false
- `world.CreateCubeCharacters` → false
- `world.CreateTramCharacters` → false

### 4. Spatial Model Extraction

After world generation, extract a `SpatialModel` from `ClusterDesc`:

```csharp
public class Location
{
    public int Id;
    public string Type;  // "home", "workplace", "shop", "social_venue", "street_segment"
    public Vector3 Position;
    public int Capacity;
    public string ShopType;  // null if not a shop, else "Eat", "Drink", "Game2"
    // References back to engine data
    public int QuarterIndex;
    public int EstateIndex;
}

public class Route
{
    public int OriginId;
    public int DestinationId;
    public float TravelTimeMinutes;
    public List<int> StreetSegmentIds;
}

public class SpatialModel
{
    public List<Location> Locations;
    public List<Route> Routes;
    // Build from ClusterDesc data
    public static SpatialModel ExtractFrom(ClusterDesc cluster) { ... }
}
```

Sources:
- `ClusterDesc.QuarterStore()` → quarters → estates → buildings (home/workplace locations)
- `ClusterDesc.StrokeStore()` → street points → strokes (routes, travel times)
- `GenerateShopsOperator` tags on `ShopFront` objects (shop locations by type)

### 5. NPC Assignment

Each NPC needs a home, workplace, and social venues assigned from the spatial model:

```
NPC seed → deterministic role (worker/merchant/socialite/drifter)
Role → location requirements:
  - home: pick a building in a residential quarter
  - workplace: pick a building/shop matching role
  - social_venues: pick 1-3 shops/buildings with social attributes
```

All derived deterministically from seed + spatial model.

## Existing Code To Reference

- `Karawan/DesktopMain.cs` — full bootstrap sequence (lines 116-238)
- `Aihao/Aihao/Services/EnginePreviewService.cs` — existing headless engine usage
- `nogameCode/nogame/SetupMetaGen.cs` — `FixedPosViewer` preload pattern (lines 68-96)
- `JoyceCode/engine/world/ClusterDesc.cs` — `QuarterStore()`, `StrokeStore()`, `AABB`, `Size`
- `JoyceCode/engine/streets/Building.cs`, `ShopFront.cs` — building/shop data structures
- `nogameCode/nogame/cities/GenerateShopsOperator.cs` — shop tagging logic
- `models/nogame.metaGen.json` — operator pipeline configuration

## Deliverable

`dotnet run --project Testbed` should:
1. Boot headlessly
2. Generate a cluster (streets, buildings, shops)
3. Extract the spatial model
4. Print statistics to stdout:
   - Fragment count, location count, building count, shop count
   - Street point count, route count
   - NPC assignments (count per role, sample home/workplace pairs)
5. Exit cleanly
