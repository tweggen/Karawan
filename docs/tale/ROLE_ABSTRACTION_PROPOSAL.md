# Role Abstraction Proposal

## Current State: Hardcoded Roles

The TALE system currently hardcodes 5 roles: **Worker**, **Merchant**, **Socialite**, **Drifter**, **Authority**. Role properties are scattered across the codebase with logic duplicated in multiple switch statements.

### Where Roles Are Hardcoded

| File | Location | What's Hardcoded |
|------|----------|------------------|
| **NpcAssigner.cs** | Line 8-14 | `enum NpcRole { Worker, Merchant, Socialite, Drifter, Authority }` |
| **NpcAssigner.cs** | Line 59-66 | Role distribution (38% worker, 19% merchant, 19% socialite, 19% drifter, 5% authority) |
| **NpcAssigner.cs** | Line 75-100 | Role-specific workplace assignment logic |
| **NpcAssigner.cs** | Line 126-149 | Role-specific property defaults (wealth, reputation, morality ranges) |
| **DesSimulation.cs** | Line 183-191 | Base wake hours: Worker=6h, Merchant=7h, Socialite=9h, Drifter=5h, Authority=6h |
| **DesSimulation.cs** | Line 535-552 | Request type → role mapping (who can fulfill food_delivery, restock_supply, threats, etc.) |
| **GroupDetector.cs** | Line 178 | Authority check for "patrol_unit" classification |
| **RelationshipTracker.cs** | Line 68, 73 | Authority-specific "patrol_check" interaction type |
| **TalePopulationGenerator.cs** | Line 16 | Static role array: `["worker", "merchant", "socialite", "drifter", "authority"]` |
| **TalePopulationGenerator.cs** | Line 21 | Default role weights: `[0.40, 0.15, 0.20, 0.15, 0.10]` |
| **TalePopulationGenerator.cs** | Line 159-218 | Role-specific location preference logic (e.g., merchant can use shop, worker cannot) |
| **TalePopulationGenerator.cs** | Line 251-295 | Role-specific property generation (morality 0.6-0.8 for worker, 0.3-0.7 for drifter, etc.) |
| **TestbedMain.cs** | Line 190 | Hardcoded role string array used during role assignment |

### Properties Per Role

**Base Wake Hour** (when NPC wakes up at simulation start)
- Used in: `DesSimulation.SeedNpc()`

**Default Distribution Weight** (relative likelihood during population generation)
- Used in: `TalePopulationGenerator.PickRole()`
- Can be modified by cluster attribute (downtown, residential, etc.)

**Location Preferences** (which location types this role can inhabit)
- Home: where NPC sleeps
- Workplace: primary work location
- Social Venue: leisure/social spots
- Shop: merchant-specific
- Street Segment: fallback/vagrant
- Used in: `TalePopulationGenerator.AssignLocationByRole()`

**Property Ranges** (initial NPC state)
- Morality (range, e.g., 0.3–0.7)
- Wealth (range, e.g., 0.05–0.25)
- Reputation (base + jitter)
- Used in: `TalePopulationGenerator.GenerateProperties()` and `NpcAssigner`

**Request Type Capabilities** (which interaction request types this role can fulfill)
- Maps role → list of request types (e.g., merchant can fulfill food_delivery, trade_service)
- Used in: `DesSimulation.GetCapableRoles()`, `DesSimulation.CheckAndClaimRequests()`

**Group Classification Marker** (optional special role for group type detection)
- Used in: `GroupDetector.ClassifyGroup()` (Authority → "patrol_unit")

**Interaction Type Bias** (optional role-specific encounter behavior)
- Used in: `RelationshipTracker.DetermineInteractionType()` (Authority → "patrol_check")

---

## Proposed Solution: RoleDefinition System

### 1. New Class: `RoleDefinition`

```csharp
namespace engine.tale;

/// <summary>
/// Metadata for a single NPC role. Loaded from game configuration.
/// Encapsulates all role-specific behavior: location preferences, properties, capabilities.
/// </summary>
public class RoleDefinition
{
    /// <summary>Unique identifier (e.g., "worker", "merchant").</summary>
    public string Id { get; set; }

    /// <summary>Display name (e.g., "Factory Worker").</summary>
    public string DisplayName { get; set; }

    /// <summary>Default spawn probability (0.0–1.0). Normalized against all roles.</summary>
    public float DefaultWeight { get; set; }

    /// <summary>Hour of day when this role typically wakes (0–23.99). Used for initial scheduling.</summary>
    public float BaseWakeHour { get; set; }

    /// <summary>
    /// Location type preferences. Maps StoryletLocationType → preferred location types.
    /// Example: "workplace" → ["workplace", "shop"] means this role prefers workplace or shop for work.
    /// </summary>
    public Dictionary<string, List<string>> LocationPreferences { get; set; }

    /// <summary>
    /// Property ranges for initial NPC generation. Each property maps to [min, max].
    /// Used in deterministic generation from seed.
    /// </summary>
    public Dictionary<string, (float Min, float Max)> PropertyRanges { get; set; }

    /// <summary>
    /// Request types this role can fulfill (e.g., ["food_delivery", "trade_service"]).
    /// Used for interaction request claiming and abstract resolution.
    /// </summary>
    public List<string> FulfillableRequestTypes { get; set; }

    /// <summary>
    /// Optional marker role for group classification.
    /// If "Authority" is in group, classified as "patrol_unit"; if both "merchant" + "trader", classified as "trade".
    /// </summary>
    public string GroupClassificationMarker { get; set; }

    /// <summary>
    /// Optional special interaction type when this role encounters others (e.g., "patrol_check" for Authority).
    /// </summary>
    public string SpecialInteractionType { get; set; }
}
```

### 2. New Class: `RoleRegistry`

Inherits from `ObjectRegistry<RoleDefinition>` to leverage existing thread-safe storage and key management.

```csharp
namespace engine.tale;

/// <summary>
/// Central registry of all roles available in the game.
/// Inherits thread-safe storage from ObjectRegistry<RoleDefinition>.
/// Populated from game configuration during engine load via Loader.WhenLoaded.
/// </summary>
public class RoleRegistry : ObjectRegistry<RoleDefinition>
{
    private float[] _normalizedWeights;

    /// <summary>Get normalized weights for role distribution (0.0–1.0, sum = 1.0).</summary>
    public float[] GetNormalizedWeights()
    {
        if (_normalizedWeights == null)
        {
            var roleIds = GetKeys(); // Thread-safe key retrieval from ObjectRegistry
            float total = 0;
            foreach (var roleId in roleIds)
            {
                var role = Get(roleId);
                if (role != null)
                    total += role.DefaultWeight;
            }

            _normalizedWeights = new float[roleIds.Count];
            for (int i = 0; i < roleIds.Count; i++)
            {
                var role = Get(roleIds[i]);
                _normalizedWeights[i] = (role?.DefaultWeight ?? 0) / (total > 0 ? total : 1);
            }
        }
        return _normalizedWeights;
    }

    /// <summary>Pick a role deterministically from weights.</summary>
    public string PickRoleFromWeights(Random rng)
    {
        var roleIds = GetKeys();
        float[] weights = GetNormalizedWeights();
        float roll = (float)rng.NextDouble();
        float cumulative = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative)
                return roleIds[i];
        }
        return roleIds.Count > 0 ? roleIds[0] : "worker";
    }
}
```

### 3. Configuration Format: `nogame.roles.json`

```json
{
  "/roles": [
    {
      "id": "worker",
      "displayName": "Factory Worker",
      "defaultWeight": 0.40,
      "baseWakeHour": 6.0,
      "locationPreferences": {
        "home": ["home"],
        "workplace": ["workplace"],
        "social_venue": ["social_venue"],
        "eat_venue": ["social_venue"]
      },
      "propertyRanges": {
        "morality": [0.6, 0.8],
        "wealth": [0.3, 0.6],
        "reputation": [0.3, 0.7]
      },
      "fulfillableRequestTypes": [
        "greeting",
        "help_request",
        "argument"
      ],
      "groupClassificationMarker": null,
      "specialInteractionType": null
    },
    {
      "id": "merchant",
      "displayName": "Shop Owner",
      "defaultWeight": 0.15,
      "baseWakeHour": 7.0,
      "locationPreferences": {
        "home": ["home", "shop"],
        "workplace": ["shop"],
        "social_venue": ["social_venue"],
        "eat_venue": ["social_venue"]
      },
      "propertyRanges": {
        "morality": [0.5, 0.8],
        "wealth": [0.5, 0.8],
        "reputation": [0.3, 0.7]
      },
      "fulfillableRequestTypes": [
        "food_delivery",
        "restock_supply",
        "trade_service",
        "greeting"
      ],
      "groupClassificationMarker": null,
      "specialInteractionType": null
    },
    {
      "id": "authority",
      "displayName": "Police Officer",
      "defaultWeight": 0.10,
      "baseWakeHour": 6.0,
      "locationPreferences": {
        "home": ["home"],
        "workplace": ["workplace"],
        "social_venue": ["social_venue"],
        "eat_venue": ["social_venue"]
      },
      "propertyRanges": {
        "morality": [0.6, 0.9],
        "wealth": [0.4, 0.6],
        "reputation": [0.6, 0.9]
      },
      "fulfillableRequestTypes": [
        "crime_report"
      ],
      "groupClassificationMarker": "Authority",
      "specialInteractionType": "patrol_check"
    }
  ]
}
```

### 4. Loading Hook in Loader

```csharp
// In a new TaleModule or during engine initialization:
I.Register<RoleRegistry>(() => new RoleRegistry());

var loader = I.Get<engine.casette.Loader>();
loader.WhenLoaded("/roles", (path, rolesNode) =>
{
    var registry = I.Get<RoleRegistry>();
    if (rolesNode is JsonArray rolesArray)
    {
        foreach (JsonNode roleNode in rolesArray)
        {
            var def = JsonSerializer.Deserialize<RoleDefinition>(roleNode.ToJsonString());
            registry.Register(def);
        }
    }
});
```

### 5. Refactored Files

**DesSimulation.cs** (lines 183-191):
```csharp
// BEFORE:
float baseWakeHour = npc.Role switch
{
    "Worker" => 6f,
    "Merchant" => 7f,
    "Socialite" => 9f,
    "Drifter" => 5f,
    "Authority" => 6f,
    _ => 6f
};

// AFTER:
var roleRegistry = I.Get<RoleRegistry>();
var roleDef = roleRegistry.GetRole(npc.Role) ?? roleRegistry.GetRole("worker");
float baseWakeHour = roleDef.BaseWakeHour;
```

**DesSimulation.cs** (lines 535-552):
```csharp
// BEFORE:
private static HashSet<string> GetCapableRoles(string requestType)
{
    return requestType switch
    {
        "food_delivery" => new HashSet<string> { "merchant", "drifter" },
        "threat" => new HashSet<string> { "drifter" },
        // ...
    };
}

// AFTER:
private HashSet<string> GetCapableRoles(string requestType)
{
    var registry = I.Get<RoleRegistry>();
    var capable = new HashSet<string>();
    foreach (var roleId in registry.RoleOrder)
    {
        var roleDef = registry.GetRole(roleId);
        if (roleDef?.FulfillableRequestTypes?.Contains(requestType) ?? false)
            capable.Add(roleId);
    }
    return capable;
}
```

**TalePopulationGenerator.cs** (lines 16, 21, 89):
```csharp
// REMOVE:
private static readonly string[] Roles = { "worker", "merchant", "socialite", "drifter", "authority" };
private static readonly float[] DefaultRoleWeights = { 0.40f, 0.15f, 0.20f, 0.15f, 0.10f };

// ADD field:
private RoleRegistry _roleRegistry;

// INJECT in constructor or via property
public void SetRoleRegistry(RoleRegistry roleRegistry)
{
    _roleRegistry = roleRegistry;
}

// REFACTOR PickRole():
private string PickRole(RandomSource rnd, ClusterDesc clusterDesc)
{
    var weights = new Dictionary<string, float>();
    var roleIds = _roleRegistry.GetKeys();

    foreach (var roleId in roleIds)
    {
        var roleDef = _roleRegistry.Get(roleId);
        weights[roleId] = roleDef.DefaultWeight;
    }

    // Apply downtown adjustments
    float downtown = clusterDesc.GetAttributeIntensity(clusterDesc.Pos, ClusterDesc.LocationAttributes.Downtown);
    if (weights.ContainsKey("merchant"))
        weights["merchant"] += downtown * 0.10f;
    // ... etc
}
```

**GroupDetector.cs** (line 178):
```csharp
// BEFORE:
if (npc.Role == "Authority") authorityCount++;

// AFTER:
var registry = I.Get<RoleRegistry>();
var roleDef = registry.GetRole(npc.Role);
if (roleDef?.GroupClassificationMarker == "Authority")
    authorityCount++;
```

**RelationshipTracker.cs** (lines 68, 73):
```csharp
// BEFORE:
if (npcA.Role == "Authority" && reputationB < 0.2f && trust < 0.2f)
{
    if (rng.NextDouble() < 0.4)
        return "patrol_check";
}

// AFTER:
var registry = I.Get<RoleRegistry>();
var roleDef = registry.GetRole(npcA.Role);
if (roleDef?.SpecialInteractionType == "patrol_check" && reputationB < 0.2f && trust < 0.2f)
{
    if (rng.NextDouble() < 0.4)
        return "patrol_check";
}
```

---

## Migration Plan

### Phase 1: Infrastructure (Low Risk)
1. ✅ **DONE** — Extended `ObjectFactory<K, T>` with `GetKeys(): IReadOnlyList<K>` method
   - Returns keys in sorted order (SortedDictionary order)
   - Thread-safe with minimal overhead (single enumeration into cached List)
2. Create `RoleDefinition` class
3. Create `RoleRegistry : ObjectRegistry<RoleDefinition>`
4. Add `WhenLoaded` hook in initialization code (TaleModule or Main)
5. **No changes to existing code yet** — new classes coexist with old

### Phase 2: Gradual Adoption (Medium Risk)
1. Refactor `DesSimulation.SeedNpc()` to use RoleRegistry for wake hours
2. Refactor `DesSimulation.GetCapableRoles()` to query RoleRegistry
3. Refactor `GroupDetector.ClassifyGroup()` to check GroupClassificationMarker
4. Test after each refactor

### Phase 3: Population Generation (Higher Risk)
1. Inject RoleRegistry into TalePopulationGenerator
2. Refactor `PickRole()` and `GenerateProperties()` to use RoleRegistry
3. Run full cluster generation test

### Phase 4: Cleanup
1. Remove hardcoded role arrays and enum from NpcAssigner
2. Update Testbed to load from RoleRegistry instead of hardcoded enum

---

## Benefits

- **Game Authoring**: Designers can add/modify roles in `nogame.roles.json` without touching code
- **Consistency**: All role logic flows through RoleRegistry, single source of truth
- **Testability**: Can load different role configs for different test scenarios
- **Extensibility**: New games can define their own role systems (e.g., 3 roles, 10 roles, etc.)
- **Modularity**: Cleaner dependency injection pattern (RoleRegistry injected where needed)

---

## Alternative: Include in Existing JSON

Instead of `nogame.roles.json`, could nest roles under an existing section:

```json
{
  "/tale/roles": [ ... ]
}
```

Or:

```json
{
  "/modules/tale/roles": [ ... ]
}
```

Keeps config more compact but harder to discover. Recommend separate file for clarity.
