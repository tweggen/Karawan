# Interaction Type Abstraction Proposal

## Current State: Hardcoded Interaction Types

The TALE system has 12+ hardcoded interaction types scattered across the codebase with duplicated logic and hardcoded parameters.

### Where Interaction Types Are Hardcoded

| File | Location | What's Hardcoded |
|------|----------|------------------|
| **RelationshipTracker.cs** | Line 55-118 | `DetermineInteractionType()` logic with hardcoded selections and conditions |
| **RelationshipTracker.cs** | Line 137-156 | `TrustDelta(string type)` switch statement with 12 interaction types and their deltas |
| **RelationshipTracker.cs** | Line 124-133 | Legacy `DetermineInteractionType()` overload (duplication) |
| **SimMetrics.cs** | Line 72, 74 | Hardcoded conflict classification ("argue", "rob", "intimidate", "blackmail") |
| **DesSimulation.cs** | Line 535-552 | Request type → role mapping still uses hardcoded strings |
| **GroupDetector.cs** | Line 189 | Hardcoded group classification logic based on properties |

### Interaction Types Currently Defined

| Type | Trust Delta | Category | Selection Triggers |
|------|-------------|----------|-------------------|
| `greet` | +0.04 | Positive | trust < 0.2f (default first meeting) |
| `chat` | +0.06 | Positive | trust 0.2-0.8f (common socializing) |
| `trade` | +0.05 | Positive | trust 0.5-0.8f (economic exchange) |
| `help` | +0.10 | Positive | trust 0.5-0.8f (mutual assistance) |
| `argue` | -0.08 | Conflict | anger > 0.5f AND trust < 0.3f |
| `rob` | -0.30 | Conflict | desperation > 0.6f AND morality < 0.3f AND wealth(other) > 0.4f |
| `intimidate` | -0.20 | Conflict | anger > 0.5f AND morality < 0.4f AND trust < 0.3f |
| `recruit` | +0.16 | Special | Both desperate AND morality < 0.4f AND trust > 0.5f |
| `blackmail` | -0.40 | Conflict | (Extracted from data; actual selection logic not visible) |
| `report_crime` | +0.04 | Authority | (Extracted from data; actual selection logic not visible) |
| `patrol_check` | 0.00 | Authority | npcRole == "Authority" AND reputation < 0.2f |
| `arrest` | -0.60 | Authority | (Extracted from data; actual selection logic not visible) |

### Properties Per Interaction Type

**Trust Delta** (impact on relationship trust)
- Positive: increases trust (greet, chat, trade, help, recruit, report_crime)
- Negative: decreases trust (argue, rob, intimidate, blackmail, arrest)
- Neutral: no effect (patrol_check)
- Used in: `RelationshipTracker.TrustDelta()`, `RelationshipTracker.RecordInteraction()`

**Category** (for metrics and classification)
- Positive, Negative, Conflict, Authority, Special
- Used in: `SimMetrics.OnEncounter()` (conflict detection)
- Used in: Logging and analysis

**Selection Conditions** (when this interaction is chosen)
- Complex boolean logic involving:
  - Trust level between NPCs
  - NPC properties (anger, morality, wealth, reputation)
  - Desperation calculation
  - Random probability thresholds
  - Role-specific checks (Authority)
- Used in: `RelationshipTracker.DetermineInteractionType()`

**Priority/Order** (precedence of selection)
- Authority patrol_check checked first (special case)
- Criminal types (rob, intimidate) checked before standard interactions
- Argument checked before default trust-based selection
- Recruit checked before standard selection
- Standard trust-based fallback last
- Used in: `DetermineInteractionType()` method ordering

---

## Proposed Solution: InteractionTypeDefinition System

### 1. New Class: `InteractionTypeDefinition`

```csharp
namespace engine.tale;

/// <summary>
/// Metadata for a single NPC interaction type. Loaded from game configuration.
/// Encapsulates trust impact, selection conditions, and categorization.
/// </summary>
public class InteractionTypeDefinition
{
    /// <summary>Unique identifier (e.g., "greet", "rob").</summary>
    public string Id { get; set; }

    /// <summary>Display name for UI/logging (e.g., "Greeting", "Robbery").</summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Trust delta applied when this interaction occurs.
    /// Positive = increases trust, Negative = decreases trust, Zero = neutral.
    /// </summary>
    public float TrustDelta { get; set; }

    /// <summary>
    /// Category for metrics and classification.
    /// Values: "positive", "negative", "conflict", "authority", "special".
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Priority order for selection. Higher numbers are checked first.
    /// Used to determine order of condition checking in DetermineInteractionType().
    /// Example: Authority interactions priority=100, defaults priority=10.
    /// </summary>
    public int SelectionPriority { get; set; }

    /// <summary>
    /// Selection condition class name. Must implement IInteractionCondition.
    /// Example: "engine.tale.conditions.TrustBasedCondition"
    /// If null, this type is never automatically selected (manual-only).
    /// </summary>
    public string ConditionClassName { get; set; }

    /// <summary>
    /// Condition parameters as a flat property dictionary.
    /// Parsed by the condition class. Examples:
    /// - "minTrust": 0.2, "maxTrust": 0.5
    /// - "minMorality": 0.3, "minDesperation": 0.6
    /// - "probability": 0.4
    /// </summary>
    public Dictionary<string, float> ConditionParameters { get; set; }
}
```

### 2. New Interface: `IInteractionCondition`

```csharp
namespace engine.tale;

/// <summary>
/// Condition evaluator for a specific interaction type.
/// Determines if two NPCs should have this interaction based on their properties.
/// </summary>
public interface IInteractionCondition
{
    /// <summary>
    /// Check if this interaction should occur given two NPCs and current trust.
    /// </summary>
    /// <param name="npcA">Initiating NPC.</param>
    /// <param name="npcB">Receiving NPC.</param>
    /// <param name="trust">Current trust level between them.</param>
    /// <param name="rng">Random number generator for probabilistic conditions.</param>
    /// <returns>True if condition is met and interaction should occur.</returns>
    bool Evaluate(NpcSchedule npcA, NpcSchedule npcB, float trust, Random rng);
}
```

### 3. Built-in Condition Implementations

```csharp
namespace engine.tale.conditions;

public class TrustBasedCondition : IInteractionCondition
{
    private float _minTrust;
    private float _maxTrust;
    private float _probability;

    public void Initialize(Dictionary<string, float> parameters)
    {
        _minTrust = parameters.GetValueOrDefault("minTrust", 0f);
        _maxTrust = parameters.GetValueOrDefault("maxTrust", 1f);
        _probability = parameters.GetValueOrDefault("probability", 1f);
    }

    public bool Evaluate(NpcSchedule npcA, NpcSchedule npcB, float trust, Random rng)
    {
        if (trust < _minTrust || trust > _maxTrust) return false;
        if (rng.NextDouble() > _probability) return false;
        return true;
    }
}

public class CriminalCondition : IInteractionCondition
{
    private float _minDesperation;
    private float _maxMorality;
    private float _minWealthTarget;
    private float _probability;

    public void Initialize(Dictionary<string, float> parameters)
    {
        _minDesperation = parameters.GetValueOrDefault("minDesperation", 0.6f);
        _maxMorality = parameters.GetValueOrDefault("maxMorality", 0.3f);
        _minWealthTarget = parameters.GetValueOrDefault("minWealthTarget", 0.4f);
        _probability = parameters.GetValueOrDefault("probability", 0.15f);
    }

    public bool Evaluate(NpcSchedule npcA, NpcSchedule npcB, float trust, Random rng)
    {
        float desperation = StoryletSelector.ComputeDesperation(npcA);
        float morality = npcA.Properties.GetValueOrDefault("morality", 0.7f);
        float wealthB = npcB.Properties.GetValueOrDefault("wealth", 0.5f);

        if (desperation < _minDesperation) return false;
        if (morality > _maxMorality) return false;
        if (wealthB < _minWealthTarget) return false;
        if (trust >= 0.2f) return false;
        if (rng.NextDouble() > _probability * desperation) return false;
        return true;
    }
}

public class AngerBasedCondition : IInteractionCondition
{
    private float _minAnger;
    private float _maxMorality;
    private float _maxTrust;
    private float _probability;

    public void Initialize(Dictionary<string, float> parameters)
    {
        _minAnger = parameters.GetValueOrDefault("minAnger", 0.5f);
        _maxMorality = parameters.GetValueOrDefault("maxMorality", 0.4f);
        _maxTrust = parameters.GetValueOrDefault("maxTrust", 0.3f);
        _probability = parameters.GetValueOrDefault("probability", 0.2f);
    }

    public bool Evaluate(NpcSchedule npcA, NpcSchedule npcB, float trust, Random rng)
    {
        float angerA = npcA.Properties.GetValueOrDefault("anger", 0f);
        float moralityA = npcA.Properties.GetValueOrDefault("morality", 0.7f);

        if (angerA < _minAnger) return false;
        if (moralityA > _maxMorality) return false;
        if (trust >= _maxTrust) return false;
        if (rng.NextDouble() > _probability) return false;
        return true;
    }
}

public class AuthorityCondition : IInteractionCondition
{
    private float _maxReputation;
    private float _maxTrust;
    private float _probability;

    public void Initialize(Dictionary<string, float> parameters)
    {
        _maxReputation = parameters.GetValueOrDefault("maxReputation", 0.2f);
        _maxTrust = parameters.GetValueOrDefault("maxTrust", 0.2f);
        _probability = parameters.GetValueOrDefault("probability", 0.4f);
    }

    public bool Evaluate(NpcSchedule npcA, NpcSchedule npcB, float trust, Random rng)
    {
        var registry = I.Get<RoleRegistry>();
        var roleDefA = registry.GetRole(npcA.Role);
        if (roleDefA?.SpecialInteractionType != "patrol_check") return false;

        float reputationB = npcB.Properties.GetValueOrDefault("reputation", 0.5f);
        if (reputationB >= _maxReputation) return false;
        if (trust >= _maxTrust) return false;
        if (rng.NextDouble() > _probability) return false;
        return true;
    }
}
```

### 4. New Class: `InteractionTypeRegistry`

```csharp
namespace engine.tale;

/// <summary>
/// Central registry of all interaction types. Inherits from ObjectRegistry<InteractionTypeDefinition>.
/// Provides condition evaluation and trust delta lookup.
/// </summary>
public class InteractionTypeRegistry : ObjectRegistry<InteractionTypeDefinition>
{
    private Dictionary<string, IInteractionCondition> _conditionCache = new();

    /// <summary>
    /// Get or create a condition evaluator for an interaction type.
    /// Reflection-based instantiation from ConditionClassName.
    /// </summary>
    private IInteractionCondition GetCondition(InteractionTypeDefinition def)
    {
        if (string.IsNullOrEmpty(def.ConditionClassName)) return null;
        if (_conditionCache.TryGetValue(def.Id, out var cached)) return cached;

        var type = Type.GetType(def.ConditionClassName);
        if (type == null) return null;

        var instance = Activator.CreateInstance(type) as IInteractionCondition;
        if (instance != null)
        {
            instance.Initialize(def.ConditionParameters ?? new Dictionary<string, float>());
            _conditionCache[def.Id] = instance;
        }
        return instance;
    }

    /// <summary>
    /// Get trust delta for an interaction type.
    /// </summary>
    public float GetTrustDelta(string interactionTypeId)
    {
        var def = Get(interactionTypeId);
        return def?.TrustDelta ?? 0f;
    }

    /// <summary>
    /// Get all interaction types sorted by SelectionPriority (descending).
    /// </summary>
    public IReadOnlyList<(string Id, InteractionTypeDefinition Def)> GetByPriority()
    {
        var result = new List<(string, InteractionTypeDefinition)>();
        var keys = GetKeys();
        foreach (var id in keys)
        {
            var def = Get(id);
            if (def != null)
                result.Add((id, def));
        }
        result.Sort((a, b) => b.Item2.SelectionPriority.CompareTo(a.Item2.SelectionPriority));
        return result.AsReadOnly();
    }

    /// <summary>
    /// Evaluate all conditions in priority order until one matches.
    /// Returns the ID of the first matching interaction type, or null.
    /// </summary>
    public string EvaluateConditions(NpcSchedule npcA, NpcSchedule npcB, float trust, Random rng)
    {
        var prioritized = GetByPriority();
        foreach (var (id, def) in prioritized)
        {
            var condition = GetCondition(def);
            if (condition != null && condition.Evaluate(npcA, npcB, trust, rng))
                return id;
        }
        return null;
    }
}
```

### 5. Configuration Format: `nogame.interactions.json`

```json
{
  "/interactions": [
    {
      "id": "greet",
      "displayName": "Greeting",
      "trustDelta": 0.04,
      "category": "positive",
      "selectionPriority": 10,
      "conditionClassName": "engine.tale.conditions.TrustBasedCondition",
      "conditionParameters": {
        "minTrust": 0.0,
        "maxTrust": 0.2,
        "probability": 1.0
      }
    },
    {
      "id": "chat",
      "displayName": "Chat",
      "trustDelta": 0.06,
      "category": "positive",
      "selectionPriority": 10,
      "conditionClassName": "engine.tale.conditions.TrustBasedCondition",
      "conditionParameters": {
        "minTrust": 0.2,
        "maxTrust": 0.8,
        "probability": 0.5
      }
    },
    {
      "id": "trade",
      "displayName": "Trade",
      "trustDelta": 0.05,
      "category": "positive",
      "selectionPriority": 10,
      "conditionClassName": "engine.tale.conditions.TrustBasedCondition",
      "conditionParameters": {
        "minTrust": 0.5,
        "maxTrust": 0.8,
        "probability": 0.5
      }
    },
    {
      "id": "help",
      "displayName": "Mutual Aid",
      "trustDelta": 0.10,
      "category": "positive",
      "selectionPriority": 10,
      "conditionClassName": "engine.tale.conditions.TrustBasedCondition",
      "conditionParameters": {
        "minTrust": 0.5,
        "maxTrust": 1.0,
        "probability": 0.5
      }
    },
    {
      "id": "argue",
      "displayName": "Argument",
      "trustDelta": -0.08,
      "category": "conflict",
      "selectionPriority": 20,
      "conditionClassName": "engine.tale.conditions.AngerBasedCondition",
      "conditionParameters": {
        "minAnger": 0.5,
        "maxMorality": 0.4,
        "maxTrust": 0.3,
        "probability": 0.3
      }
    },
    {
      "id": "rob",
      "displayName": "Robbery",
      "trustDelta": -0.30,
      "category": "conflict",
      "selectionPriority": 30,
      "conditionClassName": "engine.tale.conditions.CriminalCondition",
      "conditionParameters": {
        "minDesperation": 0.6,
        "maxMorality": 0.3,
        "minWealthTarget": 0.4,
        "probability": 0.15
      }
    },
    {
      "id": "intimidate",
      "displayName": "Intimidation",
      "trustDelta": -0.20,
      "category": "conflict",
      "selectionPriority": 25,
      "conditionClassName": "engine.tale.conditions.AngerBasedCondition",
      "conditionParameters": {
        "minAnger": 0.5,
        "maxMorality": 0.4,
        "maxTrust": 0.3,
        "probability": 0.2
      }
    },
    {
      "id": "recruit",
      "displayName": "Recruitment",
      "trustDelta": 0.16,
      "category": "special",
      "selectionPriority": 35,
      "conditionClassName": null,
      "conditionParameters": {}
    },
    {
      "id": "patrol_check",
      "displayName": "Patrol Check",
      "trustDelta": 0.0,
      "category": "authority",
      "selectionPriority": 50,
      "conditionClassName": "engine.tale.conditions.AuthorityCondition",
      "conditionParameters": {
        "maxReputation": 0.2,
        "maxTrust": 0.2,
        "probability": 0.4
      }
    },
    {
      "id": "blackmail",
      "displayName": "Blackmail",
      "trustDelta": -0.40,
      "category": "conflict",
      "selectionPriority": 28,
      "conditionClassName": null,
      "conditionParameters": {}
    }
  ]
}
```

### 6. Refactored Code: `RelationshipTracker.cs`

```csharp
// BEFORE:
public string DetermineInteractionType(float trust, NpcSchedule npcA, NpcSchedule npcB, Random rng)
{
    // ~50 lines of hardcoded logic with nested conditions
    if (npcA.Role == "Authority" && reputationB < 0.2f && trust < 0.2f) { /* ... */ }
    if (desperationA > 0.6f && moralityA < 0.3f && wealthB > 0.4f && trust < 0.2f) { /* ... */ }
    // ... more hardcoded logic
}

public static float TrustDelta(string interactionType)
{
    return interactionType switch
    {
        "greet" => 0.04f,
        "chat" => 0.06f,
        // ... all hardcoded
    };
}

// AFTER:
public string DetermineInteractionType(float trust, NpcSchedule npcA, NpcSchedule npcB, Random rng)
{
    var registry = I.Get<InteractionTypeRegistry>();
    var matched = registry.EvaluateConditions(npcA, npcB, trust, rng);

    // Fallback to "greet" if no condition matched
    return matched ?? "greet";
}

public float TrustDelta(string interactionType)
{
    var registry = I.Get<InteractionTypeRegistry>();
    return registry.GetTrustDelta(interactionType);
}
```

### 7. Refactored Code: `SimMetrics.cs`

```csharp
// BEFORE:
if (interactionType is "rob" or "intimidate" or "blackmail")
    FirstConflictDay = completedDay;

// AFTER:
var registry = I.Get<InteractionTypeRegistry>();
var def = registry.Get(interactionType);
if (def?.Category == "conflict")
    FirstConflictDay = completedDay;
```

---

## Benefits

✅ **Game Authoring** — designers define interactions in JSON without touching code
✅ **Extensibility** — new interaction types just require JSON + condition class
✅ **Flexibility** — complex conditions via pluggable `IInteractionCondition` system
✅ **Maintainability** — single source of truth for interaction properties (trust delta, category)
✅ **Testing** — can load different interaction sets for different scenarios
✅ **Data-Driven** — trust deltas and probabilities tunable in config

---

## Migration Plan

### Phase 1: Infrastructure
1. Create `IInteractionCondition` interface
2. Create `InteractionTypeDefinition` class
3. Create `InteractionTypeRegistry : ObjectRegistry<InteractionTypeDefinition>`
4. Implement built-in condition classes (TrustBasedCondition, CriminalCondition, etc.)
5. Add loader hook for `/interactions` path

### Phase 2: RelationshipTracker Refactor
1. Update `DetermineInteractionType()` to use registry conditions
2. Replace `TrustDelta()` switch with registry lookup
3. Update `RecordInteraction()` to use registry (no code change needed, just indirect call)

### Phase 3: SimMetrics Refactor
1. Replace hardcoded conflict type check with category lookup
2. Update logging to use category-based filtering

### Phase 4: Cleanup
1. Verify all interaction types properly defined in config
2. Test with different interaction sets to validate extensibility

---

## Extension Example: Custom Game

A game author could define entirely different interaction types:

```json
{
  "/interactions": [
    {
      "id": "bow",
      "displayName": "Respectful Bow",
      "trustDelta": 0.08,
      "category": "positive",
      "selectionPriority": 10,
      "conditionClassName": "myGame.conditions.HonorBasedCondition",
      "conditionParameters": {
        "minHonor": 0.7,
        "probability": 0.8
      }
    },
    {
      "id": "duel",
      "displayName": "Challenge to Duel",
      "trustDelta": -0.50,
      "category": "conflict",
      "selectionPriority": 40,
      "conditionClassName": "myGame.conditions.DuelCondition",
      "conditionParameters": {
        "minHonor": 0.5,
        "minAnger": 0.6,
        "probability": 0.3
      }
    }
  ]
}
```

No engine code changes needed — just JSON + custom condition class!
