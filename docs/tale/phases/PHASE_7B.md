# Phase 7B: Building Role Tagging

**Status**: Plan drafted (2026-03-16), pending implementation

**Prerequisites**: Phase 7 (spatial model wire-up, entry points, pathfinding infrastructure)

**Design Goal**: Give non-shop buildings explicit role metadata (residential, office, warehouse, etc.) so that `TalePopulationGenerator` can assign NPCs deterministically by building type, replacing the crude distance-based heuristic currently in Phase 7.

---

## Problem Statement

Phase 7 assigns NPC locations based on a distance heuristic: "assign home locations to buildings closer to downtown, workplace to buildings farther away." This is fragile:

1. **No semantic meaning**: A building is just a building; we don't know if it's a home, office, warehouse, or ruin.
2. **Order-dependent**: Distance computation depends on where we start and which buildings we've already assigned.
3. **Not configurable**: Cannot tell the generator "this neighborhood is all residential" or "this sector is industrial."
4. **Breaks on cluster design**: Different cluster designs (towers vs. sprawl) produce wildly different distance distributions.

Phase 7B introduces an **explicit tagging system** for buildings, mirroring the existing `ShopFront.Tags` pattern. This allows world designers to stamp roles directly onto buildings at the cluster operator level.

---

## Design Principles

1. **Tags are metadata, not code**: Building tags ("residential", "office", "warehouse") are pure data, set by `ClusterOperator` instances during world generation.
2. **Pattern parity with shops**: `ShopFront` has `Tags {"shop Eat", "shop Drink", "shop Game2"}`. Buildings get the same treatment: `Tags {"residential", "office", "warehouse"}`.
3. **Cluster-level operator**: A new `GenerateBuildingRolesOperator` (mirrors `GenerateShopsOperator`) stamps tags onto buildings deterministically from cluster seed.
4. **Immediate benefit**: Once Building.Tags exists and is populated, `TalePopulationGenerator.AssignLocationByRole()` automatically uses tag matching instead of distance heuristics.
5. **Designer control**: World builders can override tags per-cluster, or use the operator's defaults.

---

## Phase 7B-1: Implement ClusterDesc.LocationAttributes.Living & Industrial

**Current State**: `ClusterDesc.LocationAttributes.Living` and `Industrial` are declared but stubbed in `GetAttributeIntensity()`:

```csharp
case LocationAttributes.Living:
    break;  // ← STUB

case LocationAttributes.Industrial:
    break;  // ← STUB
```

**Design Principle**: Define Living and Industrial by **geometric position**, not by counting generated buildings (which would be circular—buildings are generated based on attribute intensity). Instead:
- **Living**: Ring around downtown/shopping, residential neighborhoods with overlap to commercial areas
- **Industrial**: Outermost area (periphery), high intensity at edges, low towards center

**Pattern Reference**: Downtown uses `dist / (size/2)` with Gaussian. Shopping uses `dist / (size/3) + 0.2f` offset to create a ring.

**What to Do**: Implement Living and Industrial following the same pattern:

```csharp
case LocationAttributes.Living:
{
    // Ring around downtown/shopping, overlapping with shopping ring
    // Residential neighborhoods form a middle band with commerce nearby
    var dist = (_pos - v3Spot).Length();
    dist = dist / (_size / 2.5f) + 0.3f;  // Wider radius than shopping
    var gauss = Single.Exp(-(dist * dist));
    return gauss;
}

case LocationAttributes.Industrial:
{
    // Outermost area - inverse of downtown
    // High intensity at periphery, low towards center
    var dist = (_pos - v3Spot).Length();
    dist = dist / (_size / 2f);
    // Invert: high when far from center
    var intensity = 1.0f - Single.Exp(-(dist * dist) * 2f);
    return intensity;
}
```

**Files to Modify**:
- `JoyceCode/engine/world/ClusterDesc.cs` — Implement Living and Industrial cases in `GetAttributeIntensity()`

---

## Phase 7B-2: Add Tags to Building Class

**Current State**: `Building` has no Tags property:

```csharp
public class Building
{
    public string Id;
    public Vector3 Center;
    public Vector3 Dimensions;
    public Quaternion Rotation;
    // ... geometry
    // NO TAGS
}
```

**What to Do**: Mirror `ShopFront.Tags`:

```csharp
public class Building
{
    public string Id;
    public Vector3 Center;
    public Vector3 Dimensions;
    public Quaternion Rotation;

    public Tags Tags { get; } = new();  // ← NEW

    // ... geometry
}
```

The `Tags` class (from JoyceCode) is a simple set of string tags:

```csharp
public class Tags
{
    private HashSet<string> _tags = new();
    public void Add(string tag) => _tags.Add(tag);
    public bool Has(string tag) => _tags.Contains(tag);
    public IEnumerable<string> All => _tags;
}
```

**Files to Modify**:
- `JoyceCode/world/generation/Building.cs` — Add `public Tags Tags { get; } = new();`

---

## Phase 7B-3: Update QuarterGenerator to Assign Building Roles

**Why Here?** Building roles must be determined **at creation time**, not later:
- `QuarterGenerator._createBuildings()` is where Building objects are instantiated
- It already queries `GetAttributeIntensity()` for Downtown (height) and Shopping (shops)
- Roles control building construction, so must be known before geometry generation
- This happens during cluster operator phase, before TALE initialization needs the info

**Current State** (QuarterGenerator.cs):

```csharp
private void _createBuildings(in Quarter quarter, in Estate estate)
{
    // ... existing code ...
    // Already uses intensity to determine height and shops
    float downtownness = _clusterDesc.GetAttributeIntensity(..., LocationAttributes.Downtown);
    float shoppingness = _clusterDesc.GetAttributeIntensity(..., LocationAttributes.Shopping);

    var building = new streets.Building() { ClusterDesc = _clusterDesc };
    building.AddPoints(p);
    building.SetHeight(height);
    // ← Need to assign roles/tags here
}
```

**What to Do**: After creating building (line 221), compute and assign role tags:

```csharp
private void _createBuildings(in Quarter quarter, in Estate estate)
{
    // ... existing code to create building ...
    var building = new streets.Building() { ClusterDesc = _clusterDesc };
    building.AddPoints(p);
    building.SetHeight(height);

    // NEW: Assign building roles based on location attributes
    var buildingRoles = _determineBuildingRoles(building, estate);
    foreach (var role in buildingRoles)
        building.Tags.Add(role);

    quarter.AddDebugTag("haveBuilding", "true");
    // ... rest of existing code ...
}

private List<string> _determineBuildingRoles(Building building, Estate estate)
{
    // Seed from building position for determinism
    var rnd = new RandomSource(_clusterDesc.IdString + "-building-" + building.Id);

    // Compute location attribute intensity at building center
    float livingIntensity = _clusterDesc.GetAttributeIntensity(
        building.GetCenter() + _clusterDesc.Pos,
        ClusterDesc.LocationAttributes.Living);
    float downstairsIntensity = _clusterDesc.GetAttributeIntensity(
        building.GetCenter() + _clusterDesc.Pos,
        ClusterDesc.LocationAttributes.Downtown);
    float industrialIntensity = _clusterDesc.GetAttributeIntensity(
        building.GetCenter() + _clusterDesc.Pos,
        ClusterDesc.LocationAttributes.Industrial);

    // Decision tree: which role(s) does this building fill?
    var roles = new List<string>();

    if (livingIntensity > 0.6f)
        roles.Add("residential");

    if (downstairsIntensity > 0.5f && livingIntensity < 0.7f)
        roles.Add("office");

    if (industrialIntensity > 0.4f)
        roles.Add("warehouse");

    // If no roles assigned, default to residential
    if (roles.Count == 0)
        roles.Add("residential");

    return roles;
}
```

**Files to Modify**:
- `JoyceCode/engine/streets/QuarterGenerator.cs` — Add role computation in `_createBuildings()` and new `_determineBuildingRoles()` method

---

## Phase 7B-4: Update SpatialModel.ExtractFrom()

**Current State**: `SpatialModel.ExtractFrom()` uses crude location type assignment:

```csharp
public static SpatialModel ExtractFrom(ClusterDesc cluster)
{
    // ... extract shops with type="shop"
    // ... extract buildings with type="office" (always!)
    // ... extract streets with type="street_segment"
}
```

**What to Do**: Use `Building.Tags` to set location type:

```csharp
public static SpatialModel ExtractFrom(ClusterDesc cluster)
{
    var model = new SpatialModel();
    float streetHeight = /* ... */;

    // ... extract shops

    // Extract buildings WITH TAG-BASED TYPE
    foreach (var building in cluster.Buildings)
    {
        string locationType = "office";  // default

        if (building.Tags.Has("residential"))
            locationType = "home";
        else if (building.Tags.Has("warehouse"))
            locationType = "warehouse";
        else if (building.Tags.Has("office"))
            locationType = "office";

        var loc = new Location
        {
            Type = locationType,
            Position = building.Center,
            EntryPosition = new Vector3(building.Center.X, streetHeight, building.Center.Z),
            Capacity = EstimateCapacity(building, locationType),
            // ...
        };
        model.AddLocation(loc);
    }

    // ... extract streets

    return model;
}
```

**Files to Modify**:
- `JoyceCode/engine/tale/SpatialModel.cs` — Update `ExtractFrom()` to read `Building.Tags`

---

## Phase 7B-5: Better Entry Positions Using Wall Projection

**Current State** (Phase 7B): Entry positions are building center projected to street height.

**Enhancement** (optional): Use nearest building wall to street for more realistic door positioning:

```csharp
private static Vector3 ComputeBuildingEntryPosition(Building building, Vector3[] streetPoints, float streetHeight)
{
    // Find nearest street point to building
    float minDist = float.MaxValue;
    Vector3 nearestStreetPoint = building.Center;

    foreach (var streetPt in streetPoints)
    {
        float dist = Vector3.Distance(building.Center, streetPt);
        if (dist < minDist)
        {
            minDist = dist;
            nearestStreetPoint = streetPt;
        }
    }

    // Project towards nearest street point (move 1-2 units towards street)
    Vector3 direction = Vector3.Normalize(nearestStreetPoint - building.Center);
    float wallOffset = 2.0f;  // Distance from wall to entry

    return building.Center + direction * wallOffset * (1.0f - (minDist / ClusterSize));
}
```

This is a polish pass and can be deferred if Phase 7B scope needs to be contained.

---

## Phase 7B-6: TalePopulationGenerator Automatic Benefit

**Current State** (Phase 7): `AssignLocationByRole()` uses distance heuristics.

**After Phase 7B**: No code change needed. Once `SpatialModel.ExtractFrom()` correctly reads `Building.Tags`, location type assignment flows through:

```csharp
private int AssignLocationByRole(RandomSource rnd, SpatialModel spatialModel, string role, string preferredType)
{
    // Filter locations by role affinity
    var candidates = spatialModel.GetLocations()
        .Where(loc => IsRoleValidForLocation(role, loc.Type))  // ← NOW USES CORRECT TYPES
        .ToList();

    return candidates.Count > 0
        ? candidates[rnd.NextInt(candidates.Count)].Id
        : -1;
}

private bool IsRoleValidForLocation(string role, string locationType)
{
    return (role, locationType) switch
    {
        ("worker", "office") => true,
        ("worker", "home") => true,
        ("merchant", "shop") => true,
        ("merchant", "home") => true,
        ("socialite", "shop") => true,
        ("socialite", "home") => true,
        ("drifter", "street_segment") => true,
        ("authority", "street_segment") => true,
        _ => locationType == "home"  // Fallback: everyone can live in homes
    };
}
```

**No changes needed** — the benefit is automatic once Building.Tags is populated and read by SpatialModel.

---

## Implementation Order

```
Phase 7B-1: Implement ClusterDesc.LocationAttributes.Living & Industrial
    │
    ▼
Phase 7B-2: Add Building.Tags property
    │
    ▼
Phase 7B-3: Update QuarterGenerator to assign building roles at creation time
    │
    ▼
Phase 7B-4: Update SpatialModel.ExtractFrom() to read Building.Tags
    │
    ▼
(Optional) Phase 7B-5: Wall-based entry position refinement
    │
    (Phase 7B-6 is automatic — no implementation needed)
```

Each sub-phase is ~20-30 minutes of work. Total: 1.5-2 hours for full Phase 7B.

---

## Files Changed Summary

| File | Change | Complexity |
|------|--------|-----------|
| `JoyceCode/engine/world/ClusterDesc.cs` | Implement Living & Industrial intensities | Medium |
| `JoyceCode/engine/world/Building.cs` | Add Tags property | Trivial |
| `JoyceCode/engine/streets/QuarterGenerator.cs` | Assign building roles at creation time | Low |
| `JoyceCode/engine/tale/SpatialModel.cs` | Read Building.Tags in ExtractFrom() | Low |

---

## Design Rationale

### Why Geometric Position (Not Building Counting)?

Attribute intensities must be defined **independently of generated buildings**, because:
- Buildings themselves are generated *based on* attribute intensity values
- Counting buildings to define intensity creates circular logic
- Instead, intensities represent **urban design intent**: "this is where residential should be"

Solution: Define intensity by position/distance (Downtown gaussian from center, Shopping as a ring, Living as a middle ring, Industrial at periphery). This gives us deterministic, predictable urban geography that guides building generation, not the reverse.

### Why Tags Instead of an Enum?

**Tags** (like `ShopFront.Tags`) are flexible:
- A building can have multiple roles: `{"residential", "office"}` means a mixed-use building
- Designers can add new roles without code changes
- Supports edge cases (warehouse that doubles as shipping hub)

An **Enum** would be rigid:
- One role per building
- Need code changes to add new roles
- Less designer control

### Why Assign Roles in QuarterGenerator (Not a Separate Operator)?

Building roles **must be assigned at creation time** because:
- `QuarterGenerator._createBuildings()` is where Building objects are instantiated
- Roles determine building construction characteristics (geometry, height, layout)
- QuarterGenerator already queries attribute intensities for other properties (height, shops)
- Assigning roles at creation keeps all building properties deterministic in one place
- Timeline: QuarterGenerator runs during street generation (early), before TALE initialization and before rendering

A separate cluster operator would be too late—it would run after buildings are created, missing the opportunity to influence construction.

### Why Dynamic Role Computation (Not Hardcoded Enum)?

Dynamic role computation from attribute intensities allows:
- **Cluster variation**: Different clusters get different role distributions based on their geography
- **Designer control**: World generators that query `GetAttributeIntensity()` can weight roles differently
- **Extensibility**: New roles can be added without touching QuarterGenerator code

### Why Start with Phase 7B Instead of Earlier?

Phase 7A–7E (spatial grounding) had to go first because they establish:
- Per-cluster `SpatialModel` instances ✓
- `Location` objects with entry positions ✓
- Fragment-accurate NPC positions ✓

Phase 7B *refines* the location type system, but the spatial infrastructure works without it. Phase 7B just makes it less brittle.

---

## Open Questions

1. **Should industrial/warehouse NPCs exist?** Currently, no "warehouse worker" role. Should we add one?
2. **Mixed-use buildings**: Should a building's role distribution be weighted (60% residential, 40% office) or discrete (tag list)?
3. **Role config override**: Should `TalePopulationGenerator` accept a per-cluster role distribution override at generation time?
4. **Performance**: With deterministic tag computation per building, does cluster generation slow down noticeably for 100+ buildings?

---

## Benefits Delivered

After Phase 7B:

✅ **Semantic building types** — "this is a home, this is an office" instead of distance guessing
✅ **Designer control** — Cluster builders can see and override building roles
✅ **Stable location assignment** — NPCs assigned to locations match cluster design intent
✅ **Easier debugging** — Can query "how many office workers?" without distance heuristics
✅ **Future extensibility** — New building roles can be added without touching TaleManager

