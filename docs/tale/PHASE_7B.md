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

## Phase 7B-1: Implement ClusterDesc.LocationAttributes.Living

**Current State**: `ClusterDesc.LocationAttributes.Living` is declared but stubbed:

```csharp
// In ClusterDesc.cs
public float GetAttributeIntensity(Vector3 position, LocationAttributes attr)
{
    return attr switch
    {
        LocationAttributes.Downtown => /* computed from streets/density */ ...,
        LocationAttributes.Shopping => /* computed from shops */ ...,
        LocationAttributes.Living => 0.5f,  // ← STUB
        LocationAttributes.Industrial => 0.0f,  // ← STUB
        _ => 0.0f
    };
}
```

**What to Do**: Implement proper Living intensity computation:

```csharp
LocationAttributes.Living => _computeLivingIntensity(position),

private float _computeLivingIntensity(Vector3 position)
{
    // High Living intensity near residential buildings
    // Formula: count nearby residential buildings, weighted by proximity
    float intensity = 0.0f;
    foreach (var building in Buildings)
    {
        float distSq = Vector3.DistanceSquared(position, building.Center);
        float distWeight = 1.0f / (1.0f + distSq / (ClusterSize * ClusterSize));
        intensity += distWeight;
    }
    return Math.Min(intensity / Buildings.Count, 1.0f);
}
```

**Files to Modify**:
- `JoyceCode/world/generation/ClusterDesc.cs` — Implement `_computeLivingIntensity()`

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

## Phase 7B-3: Create GenerateBuildingRolesOperator

**Current Pattern**: `GenerateShopsOperator` (in `metaGen/ClusterOperator.cs`) deterministically stamps shop tags:

```csharp
public class GenerateShopsOperator : AClusterOperator
{
    public override void Apply(ClusterDesc cluster)
    {
        // For each shop, derive tags from cluster attributes and shop geometry
        foreach (var shopFront in cluster.ShopFronts)
        {
            var tags = DetermineShopTags(shopFront, cluster);
            foreach (var tag in tags)
                shopFront.Tags.Add(tag);
        }
    }

    private List<string> DetermineShopTags(ShopFront sf, ClusterDesc cluster)
    {
        // Use cluster attribute intensity to decide shop type
        // E.g., high Downtown + high Shopping → "shop Trade"
        // High Shopping alone → "shop Eat" or "shop Drink"
        // Return list like {"shop Eat", "shop Drink"}
    }
}
```

**What to Do**: Create `GenerateBuildingRolesOperator` (same pattern):

```csharp
public class GenerateBuildingRolesOperator : AClusterOperator
{
    public override void Apply(ClusterDesc cluster)
    {
        foreach (var building in cluster.Buildings)
        {
            var tags = DetermineBuildingRoles(building, cluster);
            foreach (var tag in tags)
                building.Tags.Add(tag);
        }
    }

    private List<string> DetermineBuildingRoles(Building building, ClusterDesc cluster)
    {
        // Seed from building position
        var rnd = new RandomSource(cluster.IdString + "-building-" + building.Id);

        // Compute location attribute intensity at building center
        float livingIntensity = cluster.GetAttributeIntensity(building.Center, LocationAttributes.Living);
        float downstairsIntensity = cluster.GetAttributeIntensity(building.Center, LocationAttributes.Downtown);
        float industrialIntensity = cluster.GetAttributeIntensity(building.Center, LocationAttributes.Industrial);

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
}
```

**Files to Create/Modify**:
- `JoyceCode/world/generation/metaGen/GenerateBuildingRolesOperator.cs` — **Create** new operator
- `JoyceCode/world/generation/metaGen/ClusterDesc.cs` — Register operator in build pipeline (after `GenerateShopsOperator`)

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
Phase 7B-1: Implement ClusterDesc.LocationAttributes.Living
    │
    ▼
Phase 7B-2: Add Building.Tags property
    │
    ▼
Phase 7B-3: Create GenerateBuildingRolesOperator
    │
    ▼
Phase 7B-4: Update SpatialModel.ExtractFrom() to read Building.Tags
    │
    ▼
(Optional) Phase 7B-5: Wall-based entry position refinement
    │
    (Phase 7B-6 is automatic — no implementation needed)
```

Each sub-phase is ~30 minutes of work. Total: 2-3 hours for full Phase 7B.

---

## Files Changed Summary

| File | Change | Complexity |
|------|--------|-----------|
| `JoyceCode/world/generation/ClusterDesc.cs` | Implement Living intensity | Medium |
| `JoyceCode/world/generation/Building.cs` | Add Tags property | Trivial |
| `JoyceCode/world/generation/metaGen/GenerateBuildingRolesOperator.cs` | **Create** new operator | Medium |
| `JoyceCode/world/generation/metaGen/ClusterDesc.cs` | Register operator in pipeline | Trivial |
| `JoyceCode/engine/tale/SpatialModel.cs` | Read Building.Tags in ExtractFrom() | Low |

---

## Design Rationale

### Why Tags Instead of an Enum?

**Tags** (like `ShopFront.Tags`) are flexible:
- A building can have multiple roles: `{"residential", "office"}` means a mixed-use building
- Designers can add new roles without code changes
- Supports edge cases (warehouse that doubles as shipping hub)

An **Enum** would be rigid:
- One role per building
- Need code changes to add new roles
- Less designer control

### Why ClusterOperator Instead of Fixed Roles?

A `ClusterOperator` (like `GenerateShopsOperator`) allows:
- **Deterministic but varied**: Same cluster seed always produces same tags, but they're computed from cluster intensity (not hardcoded)
- **Swappable**: Different world generation styles can use different building role operators
- **Testable**: Operator logic can be unit-tested independently

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

