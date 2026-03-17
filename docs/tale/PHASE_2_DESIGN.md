# Phase 2 Design: Relationship Tiers & Group Types

Comprehensive design for abstracting relationship tier system and group classification into configuration-driven registries.

---

## Part 1: Relationship Tiers

### Current State

**Hardcoded in:** `RelationshipTracker.TierFromTrust()` (lines 190-198)

```csharp
public static string TierFromTrust(float trust)
{
    if (trust < 0.15f) return "stranger";
    if (trust < 0.4f) return "acquaintance";
    if (trust < 0.7f) return "friend";
    return "ally";
}
```

**Fixed tier sequence:** stranger → acquaintance → friend → ally (4 tiers, fixed order)

### Design Goal

Allow game authors to:
- Define custom tier names and thresholds
- Add/remove tiers (2 tiers, 10 tiers, whatever)
- Change tier progression boundaries
- Add tier-specific behavior modifiers (optional)

---

### 1. RelationshipTierDefinition Class

```csharp
namespace engine.tale;

/// <summary>
/// Defines a single relationship tier based on trust level.
/// Tiers are ordered sequences: tier 0 (lowest) → tier N (highest).
/// </summary>
public class RelationshipTierDefinition : IComparable<RelationshipTierDefinition>
{
    /// <summary>Unique identifier (e.g., "stranger", "friend").</summary>
    public string Id { get; set; }

    /// <summary>Display name for UI/logging.</summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Tier order/rank (0 = lowest, higher = better relationships).
    /// Used for sorting and comparison.
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// Minimum trust required to enter this tier (inclusive).
    /// Example: rank 1 with minTrust=0.15 means "trust >= 0.15".
    /// </summary>
    public float MinTrust { get; set; }

    /// <summary>
    /// Maximum trust for this tier (exclusive on upper bound).
    /// Example: rank 1 with maxTrust=0.4 means "trust < 0.4".
    /// Computed from the next tier's minTrust if not specified.
    /// </summary>
    public float MaxTrust { get; set; }

    /// <summary>
    /// Optional behavior modifier applied to NPCs in this tier.
    /// Examples: "cautious", "eager_to_help", "hostile".
    /// Null = no special behavior.
    /// </summary>
    public string BehaviorModifier { get; set; }

    /// <summary>
    /// Optional metadata for future use (e.g., color, icon, sound).
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }

    public int CompareTo(RelationshipTierDefinition other)
    {
        return Rank.CompareTo(other?.Rank ?? 0);
    }
}
```

### 2. RelationshipTierRegistry Class

```csharp
namespace engine.tale;

/// <summary>
/// Registry of relationship tiers, ordered by rank (ascending).
/// Inherited from ObjectRegistry<RelationshipTierDefinition> using tier ID as key.
/// </summary>
public class RelationshipTierRegistry : ObjectRegistry<RelationshipTierDefinition>
{
    private List<RelationshipTierDefinition> _tiersOrderedByRank;

    /// <summary>
    /// Rebuild the ordered tier list after loading all tiers.
    /// Called once during initialization (after all tiers registered).
    /// </summary>
    public void FinalizeOrder()
    {
        var keys = GetKeys();
        _tiersOrderedByRank = new List<RelationshipTierDefinition>(keys.Count);
        foreach (var id in keys)
        {
            var tier = Get(id);
            if (tier != null)
                _tiersOrderedByRank.Add(tier);
        }
        _tiersOrderedByRank.Sort((a, b) => a.Rank.CompareTo(b.Rank));

        // Compute maxTrust for each tier from the next tier's minTrust
        for (int i = 0; i < _tiersOrderedByRank.Count - 1; i++)
        {
            _tiersOrderedByRank[i].MaxTrust = _tiersOrderedByRank[i + 1].MinTrust;
        }
        if (_tiersOrderedByRank.Count > 0)
        {
            _tiersOrderedByRank[_tiersOrderedByRank.Count - 1].MaxTrust = 1.0f;
        }
    }

    /// <summary>
    /// Determine tier from trust level.
    /// Returns the first tier where minTrust <= trust < maxTrust.
    /// </summary>
    public RelationshipTierDefinition GetTierFromTrust(float trust)
    {
        if (_tiersOrderedByRank == null || _tiersOrderedByRank.Count == 0)
            return null;

        foreach (var tier in _tiersOrderedByRank)
        {
            if (trust >= tier.MinTrust && trust < tier.MaxTrust)
                return tier;
        }

        // Fallback: return highest tier if trust >= highest maxTrust
        return _tiersOrderedByRank[_tiersOrderedByRank.Count - 1];
    }

    /// <summary>
    /// Get tier ID from trust level (convenience method).
    /// </summary>
    public string GetTierIdFromTrust(float trust)
    {
        var tier = GetTierFromTrust(trust);
        return tier?.Id ?? "unknown";
    }

    /// <summary>
    /// Get tiers in rank order (lowest → highest).
    /// </summary>
    public IReadOnlyList<RelationshipTierDefinition> GetTiersByRank()
    {
        return _tiersOrderedByRank?.AsReadOnly() ?? new List<RelationshipTierDefinition>().AsReadOnly();
    }
}
```

### 3. Configuration Format: nogame.relationshipTiers.json

```json
{
  "/relationshipTiers": [
    {
      "id": "stranger",
      "displayName": "Stranger",
      "rank": 0,
      "minTrust": 0.0,
      "behaviorModifier": null,
      "metadata": {
        "color": "#888888",
        "icon": "unknown"
      }
    },
    {
      "id": "acquaintance",
      "displayName": "Acquaintance",
      "rank": 1,
      "minTrust": 0.15,
      "behaviorModifier": "neutral",
      "metadata": {
        "color": "#CCCCCC",
        "icon": "person"
      }
    },
    {
      "id": "friend",
      "displayName": "Friend",
      "rank": 2,
      "minTrust": 0.4,
      "behaviorModifier": "helpful",
      "metadata": {
        "color": "#00AA00",
        "icon": "heart"
      }
    },
    {
      "id": "best_friend",
      "displayName": "Best Friend",
      "rank": 3,
      "minTrust": 0.7,
      "behaviorModifier": "devoted",
      "metadata": {
        "color": "#00FF00",
        "icon": "heart_filled"
      }
    },
    {
      "id": "rival",
      "displayName": "Rival",
      "rank": -1,
      "minTrust": -1.0,
      "behaviorModifier": "competitive",
      "metadata": {
        "color": "#FF0000",
        "icon": "crossed_swords"
      }
    }
  ]
}
```

### 4. Refactored Code: RelationshipTracker.cs

```csharp
// BEFORE:
public static string TierFromTrust(float trust)
{
    if (trust < 0.15f) return "stranger";
    if (trust < 0.4f) return "acquaintance";
    if (trust < 0.7f) return "friend";
    return "ally";
}

// AFTER:
public string TierFromTrust(float trust)
{
    var registry = I.Get<RelationshipTierRegistry>();
    return registry.GetTierIdFromTrust(trust);
}
```

### 5. Loader Integration

```csharp
// In TaleModule.cs or equivalent initialization:
I.Register<RelationshipTierRegistry>(() => new RelationshipTierRegistry());

var loader = I.Get<engine.casette.Loader>();
loader.WhenLoaded("/relationshipTiers", (path, tiersNode) =>
{
    var registry = I.Get<RelationshipTierRegistry>();
    if (tiersNode is JsonArray tiersArray)
    {
        foreach (JsonNode tierNode in tiersArray)
        {
            var def = JsonSerializer.Deserialize<RelationshipTierDefinition>(
                tierNode.ToJsonString());
            if (def != null)
                registry.Add(def.Id, def);
        }
        registry.FinalizeOrder();
    }
});
```

### 6. Impact Analysis: RelationshipTracker Changes

| Method | Current | Refactored |
|--------|---------|-----------|
| `TierFromTrust(float)` | Static switch | Instance method, queries registry |
| `RecordInteraction()` | Calls `TierFromTrust()` | No change needed (indirect through instance) |
| `LogRelationshipChanged()` | Gets tier string | No change needed |

---

## Part 2: Group Types

### Current State

**Hardcoded in:** `GroupDetector.ClassifyGroup()` (lines 165-192)

```csharp
private static string ClassifyGroup(List<int> memberIds, IReadOnlyDictionary<int, NpcSchedule> npcs)
{
    float avgWealth = 0, avgMorality = 0, avgAnger = 0, avgReputation = 0;
    int authorityCount = 0;
    int count = 0;

    foreach (int id in memberIds)
    {
        if (!npcs.TryGetValue(id, out var npc)) continue;
        avgWealth += npc.Properties.GetValueOrDefault("wealth", 0.5f);
        avgMorality += npc.Properties.GetValueOrDefault("morality", 0.7f);
        avgAnger += npc.Properties.GetValueOrDefault("anger", 0f);
        avgReputation += npc.Properties.GetValueOrDefault("reputation", 0.5f);
        if (npc.Role == "Authority") authorityCount++;
        count++;
    }

    if (count == 0) return "social";
    avgWealth /= count;
    avgMorality /= count;
    avgAnger /= count;
    avgReputation /= count;

    if (authorityCount > count / 2) return "patrol_unit";
    if (avgWealth < 0.3f && avgMorality < 0.4f) return "criminal";
    if (avgWealth > 0.5f && avgReputation > 0.5f) return "trade";
    return "social";
}
```

**Fixed group types:** criminal, trade, social, patrol_unit (4 types, hardcoded order)

### Design Goal

Allow game authors to:
- Define custom group types with custom classification logic
- Plug in different condition evaluators (similar to InteractionTypeCondition)
- Set classification priority (which types are checked first)
- Add group-specific properties (color, icon, behavior)

---

### 1. GroupTypeDefinition Class

```csharp
namespace engine.tale;

/// <summary>
/// Defines a group type with classification conditions.
/// Groups are detected by evaluating conditions against group members.
/// </summary>
public class GroupTypeDefinition
{
    /// <summary>Unique identifier (e.g., "criminal", "trade").</summary>
    public string Id { get; set; }

    /// <summary>Display name for UI/logging.</summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Classification priority (higher = checked first).
    /// Allows precise control over precedence.
    /// Example: patrol_unit=100, criminal=50, trade=30, social=1.
    /// </summary>
    public int ClassificationPriority { get; set; }

    /// <summary>
    /// Condition class name. Must implement IGroupClassificationCondition.
    /// Example: "engine.tale.conditions.PatrolUnitCondition"
    /// If null, never matches (manual groups only).
    /// </summary>
    public string ConditionClassName { get; set; }

    /// <summary>
    /// Condition parameters as flat dictionary.
    /// Passed to condition implementation for initialization.
    /// </summary>
    public Dictionary<string, float> ConditionParameters { get; set; }

    /// <summary>
    /// Optional metadata (color, icon, behavior hints).
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }
}
```

### 2. IGroupClassificationCondition Interface

```csharp
namespace engine.tale;

/// <summary>
/// Condition evaluator for group classification.
/// Determines if a group matches this group type based on member properties.
/// </summary>
public interface IGroupClassificationCondition
{
    /// <summary>
    /// Initialize condition with parameters from configuration.
    /// </summary>
    void Initialize(Dictionary<string, float> parameters);

    /// <summary>
    /// Check if group members match this group type.
    /// </summary>
    /// <param name="memberIds">NPC IDs in the group (3+ members).</param>
    /// <param name="npcs">NPC schedule dictionary.</param>
    /// <returns>True if group matches this type.</returns>
    bool Matches(List<int> memberIds, IReadOnlyDictionary<int, NpcSchedule> npcs);
}
```

### 3. Built-in Condition Implementations

```csharp
namespace engine.tale.conditions;

/// <summary>
/// Patrol Unit: group with majority authority members.
/// </summary>
public class PatrolUnitCondition : IGroupClassificationCondition
{
    private float _minAuthorityRatio;

    public void Initialize(Dictionary<string, float> parameters)
    {
        _minAuthorityRatio = parameters.GetValueOrDefault("minAuthorityRatio", 0.5f);
    }

    public bool Matches(List<int> memberIds, IReadOnlyDictionary<int, NpcSchedule> npcs)
    {
        if (memberIds.Count == 0) return false;

        var registry = I.Get<RoleRegistry>();
        int authorityCount = 0;

        foreach (int id in memberIds)
        {
            if (npcs.TryGetValue(id, out var npc))
            {
                var roleDef = registry.GetRole(npc.Role);
                if (roleDef?.GroupClassificationMarker == "Authority")
                    authorityCount++;
            }
        }

        return (float)authorityCount / memberIds.Count >= _minAuthorityRatio;
    }
}

/// <summary>
/// Criminal: group with low average wealth and morality.
/// </summary>
public class CriminalCondition : IGroupClassificationCondition
{
    private float _maxWealth;
    private float _maxMorality;

    public void Initialize(Dictionary<string, float> parameters)
    {
        _maxWealth = parameters.GetValueOrDefault("maxWealth", 0.3f);
        _maxMorality = parameters.GetValueOrDefault("maxMorality", 0.4f);
    }

    public bool Matches(List<int> memberIds, IReadOnlyDictionary<int, NpcSchedule> npcs)
    {
        if (memberIds.Count == 0) return false;

        float totalWealth = 0, totalMorality = 0;
        int count = 0;

        foreach (int id in memberIds)
        {
            if (npcs.TryGetValue(id, out var npc))
            {
                totalWealth += npc.Properties.GetValueOrDefault("wealth", 0.5f);
                totalMorality += npc.Properties.GetValueOrDefault("morality", 0.7f);
                count++;
            }
        }

        if (count == 0) return false;
        float avgWealth = totalWealth / count;
        float avgMorality = totalMorality / count;

        return avgWealth < _maxWealth && avgMorality < _maxMorality;
    }
}

/// <summary>
/// Trade Guild: group with high wealth and reputation.
/// </summary>
public class TradeCondition : IGroupClassificationCondition
{
    private float _minWealth;
    private float _minReputation;

    public void Initialize(Dictionary<string, float> parameters)
    {
        _minWealth = parameters.GetValueOrDefault("minWealth", 0.5f);
        _minReputation = parameters.GetValueOrDefault("minReputation", 0.5f);
    }

    public bool Matches(List<int> memberIds, IReadOnlyDictionary<int, NpcSchedule> npcs)
    {
        if (memberIds.Count == 0) return false;

        float totalWealth = 0, totalReputation = 0;
        int count = 0;

        foreach (int id in memberIds)
        {
            if (npcs.TryGetValue(id, out var npc))
            {
                totalWealth += npc.Properties.GetValueOrDefault("wealth", 0.5f);
                totalReputation += npc.Properties.GetValueOrDefault("reputation", 0.5f);
                count++;
            }
        }

        if (count == 0) return false;
        float avgWealth = totalWealth / count;
        float avgReputation = totalReputation / count;

        return avgWealth >= _minWealth && avgReputation >= _minReputation;
    }
}

/// <summary>
/// Social Circle: default fallback (always matches).
/// </summary>
public class SocialCondition : IGroupClassificationCondition
{
    public void Initialize(Dictionary<string, float> parameters) { }
    public bool Matches(List<int> memberIds, IReadOnlyDictionary<int, NpcSchedule> npcs) => true;
}
```

### 4. GroupTypeRegistry Class

```csharp
namespace engine.tale;

/// <summary>
/// Registry of group types, ordered by classification priority.
/// Inherits from ObjectRegistry<GroupTypeDefinition>.
/// </summary>
public class GroupTypeRegistry : ObjectRegistry<GroupTypeDefinition>
{
    private Dictionary<string, IGroupClassificationCondition> _conditionCache = new();
    private List<(string Id, GroupTypeDefinition Def)> _typesOrderedByPriority;

    /// <summary>
    /// Rebuild the priority-ordered list after loading all types.
    /// Called once during initialization.
    /// </summary>
    public void FinalizeOrder()
    {
        var keys = GetKeys();
        _typesOrderedByPriority = new List<(string, GroupTypeDefinition)>(keys.Count);
        foreach (var id in keys)
        {
            var def = Get(id);
            if (def != null)
                _typesOrderedByPriority.Add((id, def));
        }
        // Sort by priority descending (higher priority first)
        _typesOrderedByPriority.Sort((a, b) => b.Item2.ClassificationPriority.CompareTo(a.Item2.ClassificationPriority));
    }

    /// <summary>
    /// Get or create a condition evaluator for a group type.
    /// </summary>
    private IGroupClassificationCondition GetCondition(GroupTypeDefinition def)
    {
        if (string.IsNullOrEmpty(def.ConditionClassName)) return null;
        if (_conditionCache.TryGetValue(def.Id, out var cached)) return cached;

        var type = Type.GetType(def.ConditionClassName);
        if (type == null) return null;

        var instance = Activator.CreateInstance(type) as IGroupClassificationCondition;
        if (instance != null)
        {
            instance.Initialize(def.ConditionParameters ?? new Dictionary<string, float>());
            _conditionCache[def.Id] = instance;
        }
        return instance;
    }

    /// <summary>
    /// Classify a group by evaluating conditions in priority order.
    /// Returns the first matching group type ID, or "social" (default).
    /// </summary>
    public string ClassifyGroup(List<int> memberIds, IReadOnlyDictionary<int, NpcSchedule> npcs)
    {
        if (memberIds == null || memberIds.Count == 0) return "social";

        if (_typesOrderedByPriority == null || _typesOrderedByPriority.Count == 0)
            return "social";

        foreach (var (id, def) in _typesOrderedByPriority)
        {
            var condition = GetCondition(def);
            if (condition != null && condition.Matches(memberIds, npcs))
                return id;
        }

        return "social";
    }
}
```

### 5. Configuration Format: nogame.groupTypes.json

```json
{
  "/groupTypes": [
    {
      "id": "patrol_unit",
      "displayName": "Patrol Unit",
      "classificationPriority": 100,
      "conditionClassName": "engine.tale.conditions.PatrolUnitCondition",
      "conditionParameters": {
        "minAuthorityRatio": 0.5
      },
      "metadata": {
        "color": "#0066CC",
        "icon": "badge",
        "behavior": "coordinated_law_enforcement"
      }
    },
    {
      "id": "criminal",
      "displayName": "Criminal Organization",
      "classificationPriority": 50,
      "conditionClassName": "engine.tale.conditions.CriminalCondition",
      "conditionParameters": {
        "maxWealth": 0.3,
        "maxMorality": 0.4
      },
      "metadata": {
        "color": "#660000",
        "icon": "skull",
        "behavior": "organized_crime"
      }
    },
    {
      "id": "trade",
      "displayName": "Trade Guild",
      "classificationPriority": 30,
      "conditionClassName": "engine.tale.conditions.TradeCondition",
      "conditionParameters": {
        "minWealth": 0.5,
        "minReputation": 0.5
      },
      "metadata": {
        "color": "#CCAA00",
        "icon": "coins",
        "behavior": "mutual_commerce"
      }
    },
    {
      "id": "social",
      "displayName": "Social Circle",
      "classificationPriority": 1,
      "conditionClassName": "engine.tale.conditions.SocialCondition",
      "conditionParameters": {},
      "metadata": {
        "color": "#00AA00",
        "icon": "people",
        "behavior": "friendship"
      }
    }
  ]
}
```

### 6. Refactored Code: GroupDetector.cs

```csharp
// BEFORE:
private static string ClassifyGroup(List<int> memberIds, IReadOnlyDictionary<int, NpcSchedule> npcs)
{
    // ~25 lines of hardcoded logic
    if (authorityCount > count / 2) return "patrol_unit";
    if (avgWealth < 0.3f && avgMorality < 0.4f) return "criminal";
    if (avgWealth > 0.5f && avgReputation > 0.5f) return "trade";
    return "social";
}

// AFTER:
private string ClassifyGroup(List<int> memberIds, IReadOnlyDictionary<int, NpcSchedule> npcs)
{
    var registry = I.Get<GroupTypeRegistry>();
    return registry.ClassifyGroup(memberIds, npcs);
}
```

### 7. Loader Integration

```csharp
// In TaleModule.cs or equivalent initialization:
I.Register<GroupTypeRegistry>(() => new GroupTypeRegistry());

var loader = I.Get<engine.casette.Loader>();
loader.WhenLoaded("/groupTypes", (path, typesNode) =>
{
    var registry = I.Get<GroupTypeRegistry>();
    if (typesNode is JsonArray typesArray)
    {
        foreach (JsonNode typeNode in typesArray)
        {
            var def = JsonSerializer.Deserialize<GroupTypeDefinition>(
                typeNode.ToJsonString());
            if (def != null)
                registry.Add(def.Id, def);
        }
        registry.FinalizeOrder();
    }
});
```

### 8. Impact Analysis: GroupDetector Changes

| Method | Current | Refactored |
|--------|---------|-----------|
| `ClassifyGroup()` | Static, hardcoded logic | Instance method, queries registry |
| `Detect()` | Calls `ClassifyGroup()` | No change needed (indirect) |

---

## Part 3: Integration Plan

### Dependencies

```
RoleRegistry (Phase 1)
    ↓
    ├─→ RelationshipTierRegistry (Phase 2)
    │      Used for: tier-based storylet preconditions (future)
    │
    └─→ GroupTypeRegistry (Phase 2)
           Used for: classify groups, detect gang formation
           Depends on: RoleRegistry for authority detection
```

### Loading Order

1. Load Roles (RoleRegistry)
2. Load InteractionTypes (InteractionTypeRegistry) — may reference roles
3. Load RelationshipTiers (RelationshipTierRegistry)
4. Load GroupTypes (GroupTypeRegistry) — must load after roles
5. Initialize DesSimulation with all registries

### Code Changes Summary

| File | Changes | Effort |
|------|---------|--------|
| `RelationshipTracker.cs` | Replace `TierFromTrust()` implementation | Very Low |
| `GroupDetector.cs` | Replace `ClassifyGroup()` implementation | Very Low |
| `TaleModule.cs` | Add 2 loader hooks | Low |
| `RelationshipTierRegistry.cs` | New file | Low |
| `GroupTypeRegistry.cs` | New file | Low |
| `*Condition.cs` | New condition files (4 classes) | Low |
| Config JSON | Add 2 new config files | None (no code) |

### Testing Strategy

1. **Unit Tests**: Each condition independently (no registries needed)
2. **Integration Tests**: Registry ordering, classification correctness
3. **Config Tests**: Parse JSON, verify tier/group detection
4. **Scenario Tests**: Simulate groups forming and being classified correctly

---

## Part 4: Extension Examples

### Custom Game: Medieval Fantasy

**Custom Tiers:**
```json
{
  "/relationshipTiers": [
    { "id": "despised", "rank": 0, "minTrust": 0.0 },
    { "id": "neutral", "rank": 1, "minTrust": 0.3 },
    { "id": "friendly", "rank": 2, "minTrust": 0.6 },
    { "id": "sworn_brother", "rank": 3, "minTrust": 0.85 }
  ]
}
```

**Custom Group Types:**
```json
{
  "/groupTypes": [
    {
      "id": "mercenary_band",
      "conditionClassName": "myGame.MercenaryBandCondition",
      "conditionParameters": { "weaponsPerMember": 0.8 }
    },
    {
      "id": "merchant_caravan",
      "conditionClassName": "myGame.CaravanCondition",
      "conditionParameters": { "minWealth": 0.7 }
    }
  ]
}
```

### Custom Game: Sci-Fi Corporate

**Custom Tiers:**
```json
{
  "/relationshipTiers": [
    { "id": "rival_corp", "rank": -1, "minTrust": -1.0 },
    { "id": "unknown", "rank": 0, "minTrust": 0.0 },
    { "id": "business_partner", "rank": 1, "minTrust": 0.4 },
    { "id": "alliance", "rank": 2, "minTrust": 0.7 },
    { "id": "merger_candidate", "rank": 3, "minTrust": 0.9 }
  ]
}
```

---

## Summary

**Phase 2 introduces:**
- ✅ RelationshipTierRegistry — configurable trust tiers with custom conditions
- ✅ GroupTypeRegistry — pluggable group classification system
- ✅ 4 built-in conditions (PatrolUnit, Criminal, Trade, Social)
- ✅ Loader hooks for both registries
- ✅ Minimal code changes (2 method implementations + 2 loader hooks)

**Complexity:** Low-Medium
**Effort:** 1-2 days
**Value:** Unlocks tier-based and group-type-specific gameplay
