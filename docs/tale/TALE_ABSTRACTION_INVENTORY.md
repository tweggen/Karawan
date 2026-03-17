# TALE Abstraction Inventory

Comprehensive analysis of all hardcoded TALE properties that could be abstracted into configuration.

---

## Already Proposed for Abstraction

### 1. ✅ Roles (RoleRegistry)
**Status:** Proposed in ROLE_ABSTRACTION_PROPOSAL.md
- Role names, distribution weights, base wake hours
- Location preferences, property ranges per role
- Request capabilities, group classification markers

### 2. ✅ Interaction Types (InteractionTypeRegistry)
**Status:** Proposed in INTERACTION_TYPE_ABSTRACTION_PROPOSAL.md
- Interaction type names, trust deltas
- Selection conditions (pluggable IInteractionCondition)
- Categories, priorities for evaluation order

---

## Additional Abstractable Systems

### 3. Relationship Tiers (RelationshipTierRegistry)

**Currently Hardcoded:** `RelationshipTracker.TierFromTrust()` (line 190-198)

```csharp
// CURRENT:
if (trust < 0.15f) return "stranger";
if (trust < 0.4f) return "acquaintance";
if (trust < 0.7f) return "friend";
return "ally";
```

**Properties:**
- Tier name (stranger, acquaintance, friend, ally)
- Minimum trust threshold (0.0–1.0)
- Display name for UI
- Behavior modifier (optional: affects decision-making)

**Used In:**
- `RelationshipTracker.TierFromTrust()` — determines tier from trust level
- `RelationshipTracker.RecordInteraction()` — logged when tier changes
- `JsonlEventLogger.LogRelationshipChanged()` — event output
- Future: relationship-based storylet preconditions

**Configuration Example:**
```json
{
  "/relationshipTiers": [
    {
      "id": "stranger",
      "displayName": "Stranger",
      "minTrust": 0.0,
      "maxTrust": 0.15
    },
    {
      "id": "acquaintance",
      "displayName": "Acquaintance",
      "minTrust": 0.15,
      "maxTrust": 0.4
    },
    {
      "id": "friend",
      "displayName": "Friend",
      "minTrust": 0.4,
      "maxTrust": 0.7
    },
    {
      "id": "ally",
      "displayName": "Ally",
      "minTrust": 0.7,
      "maxTrust": 1.0
    }
  ]
}
```

---

### 4. Group Types (GroupTypeRegistry)

**Currently Hardcoded:** `GroupDetector.ClassifyGroup()` (line 165-192)

```csharp
// CURRENT:
if (authorityCount > count / 2) return "patrol_unit";
if (avgWealth < 0.3f && avgMorality < 0.4f) return "criminal";
if (avgWealth > 0.5f && avgReputation > 0.5f) return "trade";
return "social";
```

**Properties:**
- Group type name (criminal, trade, social, patrol_unit)
- Display name for UI/logging
- Classification conditions (criteria-based)
- Primary color/visual indicator (optional for UI)

**Used In:**
- `GroupDetector.ClassifyGroup()` — determines group type
- `SimMetrics.OnGroupDetection()` — tracks group formation by type
- `JsonlEventLogger.LogGroupFormed()` — event logging
- `SimMetrics.FirstGangFormationDay` — triggers on "criminal" groups

**Configuration Example:**
```json
{
  "/groupTypes": [
    {
      "id": "patrol_unit",
      "displayName": "Patrol Unit",
      "priority": 1,
      "classificationClassName": "engine.tale.conditions.PatrolUnitCondition",
      "classificationParameters": {
        "authorityRatio": 0.5
      }
    },
    {
      "id": "criminal",
      "displayName": "Criminal Organization",
      "priority": 2,
      "classificationClassName": "engine.tale.conditions.CriminalCondition",
      "classificationParameters": {
        "maxWealth": 0.3,
        "maxMorality": 0.4
      }
    },
    {
      "id": "trade",
      "displayName": "Trade Guild",
      "priority": 3,
      "classificationClassName": "engine.tale.conditions.TradeCondition",
      "classificationParameters": {
        "minWealth": 0.5,
        "minReputation": 0.5
      }
    },
    {
      "id": "social",
      "displayName": "Social Circle",
      "priority": 100,
      "classificationClassName": null,
      "classificationParameters": {}
    }
  ]
}
```

---

### 5. Desperation Calculation (DesperationModel)

**Currently Hardcoded:** `StoryletSelector.ComputeDesperation()` (line 37-44)

```csharp
// CURRENT:
return Math.Clamp(
    hunger * 0.4f +
    (1f - wealth) * 0.3f +
    anger * 0.2f +
    (1f - health) * 0.1f, 0f, 1f);
```

**Properties:**
- Component weights (hunger, wealth inverse, anger, health inverse)
- Optional: minimum threshold for "desperate" classification
- Optional: formula variant (linear, exponential, piecewise)

**Used In:**
- `StoryletSelector.ComputeDesperation()` — all storylet precondition checks
- `RelationshipTracker.DetermineInteractionType()` — criminal interactions, recruit check
- `DesSimulation.ApplyMoralityDrift()` — morality drift calculation
- Interaction conditions (CriminalCondition, etc.)

**Configuration Example:**
```json
{
  "/desperation": {
    "formula": "weighted_sum",
    "components": [
      {
        "property": "hunger",
        "weight": 0.4,
        "invert": false
      },
      {
        "property": "wealth",
        "weight": 0.3,
        "invert": true
      },
      {
        "property": "anger",
        "weight": 0.2,
        "invert": false
      },
      {
        "property": "health",
        "weight": 0.1,
        "invert": true
      }
    ],
    "min": 0.0,
    "max": 1.0,
    "desperateThreshold": 0.6
  }
}
```

---

### 6. Morality Drift Model (MoralityDriftModel)

**Currently Hardcoded:** `DesSimulation.ApplyMoralityDrift()` (line 558-577)

```csharp
// CURRENT:
float drift = 0f;
if (desperation > 0.4f)
    drift -= (desperation - 0.4f) * 0.03f;  // Down: 0.03 per desperation unit
if (desperation < 0.2f)
    drift += 0.003f;  // Up: 0.003 per day when calm
```

**Properties:**
- Downward pressure (desperation threshold, rate)
- Upward recovery (calm threshold, rate)
- Optional: time-decay (how morality recovers over days)
- Optional: property influences (e.g., bad interactions lower morality faster)

**Used In:**
- `DesSimulation.ApplyMoralityDrift()` — applied once per simulation day
- Affects NPC behavior (lower morality → more criminal interactions)

**Configuration Example:**
```json
{
  "/moralityDrift": {
    "downward": {
      "trigger": "desperation",
      "threshold": 0.4,
      "rate": 0.03,
      "scaledBy": "desperation"
    },
    "upward": {
      "trigger": "low_desperation",
      "threshold": 0.2,
      "rate": 0.003,
      "scaledBy": null
    },
    "min": 0.0,
    "max": 1.0
  }
}
```

---

### 7. Location Types (LocationTypeRegistry)

**Currently Semi-Hardcoded:** Referenced in code, but types come from data
- "home", "workplace", "shop", "social_venue", "street_segment"
- Some hardcoded mappings (e.g., "Eat" location subtype → "social_venue")

**Properties:**
- Type name (home, workplace, etc.)
- Display name
- Role accessibility (which roles can use this location)
- Behavior hints (what activities happen here)

**Used In:**
- `StoryletDefinition.ResolveLocationType()` — storylet location selection
- `SpatialModel.FindNearestOfType()` — location queries
- `TalePopulationGenerator.AssignLocationByRole()` — role-location matching
- Location subtype mapping ("Eat" → "social_venue")

**Current Implementation:** Already somewhat data-driven (from Buildings/ShopFronts), but hardcoded type strings in code.

**Configuration Example:**
```json
{
  "/locationTypes": [
    {
      "id": "home",
      "displayName": "Home",
      "roleAccessibility": ["worker", "merchant", "socialite", "drifter", "authority"]
    },
    {
      "id": "workplace",
      "displayName": "Workplace",
      "roleAccessibility": ["worker", "authority"]
    },
    {
      "id": "shop",
      "displayName": "Shop",
      "roleAccessibility": ["merchant"]
    },
    {
      "id": "social_venue",
      "displayName": "Social Venue",
      "roleAccessibility": ["worker", "merchant", "socialite", "drifter", "authority"]
    },
    {
      "id": "street_segment",
      "displayName": "Street",
      "roleAccessibility": ["worker", "merchant", "socialite", "drifter", "authority"]
    }
  ]
}
```

---

### 8. Request Types (RequestTypeRegistry)

**Currently Hardcoded:** `DesSimulation.GetCapableRoles()` (line 535-552)

```csharp
// CURRENT:
"food_delivery" => new HashSet<string> { "merchant", "drifter" },
"restock_supply" => new HashSet<string> { "merchant", "drifter" },
"trade_service" => new HashSet<string> { "merchant", "drifter" },
"greeting" => new HashSet<string> { "worker", "socialite", "merchant", "drifter" },
// ... 9 more types
```

**Properties:**
- Request type name (food_delivery, help_request, threat, etc.)
- Display name for UI
- Capable roles (which roles can fulfill this)
- Urgency default
- Timeout duration default

**Used In:**
- `DesSimulation.GetCapableRoles()` — Tier 3 request fulfillment
- `DesSimulation.CheckAndClaimRequests()` — role matching
- `InteractionPool.EmitRequest()` — request creation
- Storylet `RequestPostcondition`

**Configuration Example:**
```json
{
  "/requestTypes": [
    {
      "id": "food_delivery",
      "displayName": "Food Delivery",
      "capableRoles": ["merchant", "drifter"],
      "urgency": 5,
      "timeoutMinutes": 480
    },
    {
      "id": "help_request",
      "displayName": "Help Needed",
      "capableRoles": ["worker", "socialite"],
      "urgency": 7,
      "timeoutMinutes": 240
    },
    {
      "id": "crime_report",
      "displayName": "Crime Report",
      "capableRoles": ["authority"],
      "urgency": 8,
      "timeoutMinutes": 120
    }
  ]
}
```

---

### 9. NPC Properties/Attributes (PropertyRegistry)

**Currently Semi-Hardcoded:** Property names are strings ("anger", "fear", "trust", etc.)
- Defined implicitly across code (no central list)
- Property ranges per role hardcoded in TalePopulationGenerator
- Defaults scattered across code (0.5f, 0.7f, etc.)

**Properties:**
- Property name (anger, fear, trust, wealth, etc.)
- Display name (for UI)
- Type (float 0.0-1.0)
- Default value
- Min/max valid range
- Description (for tooltips)

**Used In:**
- Every NPC schedule's `Dictionary<string, float> Properties`
- All interaction conditions, desperation calc, morality drift
- Relationship tiers
- Storylet preconditions

**Configuration Example:**
```json
{
  "/properties": [
    {
      "id": "anger",
      "displayName": "Anger",
      "description": "Proneness to conflict and aggression",
      "defaultValue": 0.1,
      "min": 0.0,
      "max": 1.0
    },
    {
      "id": "fear",
      "displayName": "Fear",
      "description": "Anxiety and risk aversion",
      "defaultValue": 0.2,
      "min": 0.0,
      "max": 1.0
    },
    {
      "id": "wealth",
      "displayName": "Wealth",
      "description": "Economic resources and desperation inverse",
      "defaultValue": 0.5,
      "min": 0.0,
      "max": 1.0
    },
    {
      "id": "morality",
      "displayName": "Morality",
      "description": "Ethical boundaries; drifts with desperation",
      "defaultValue": 0.6,
      "min": 0.0,
      "max": 1.0
    }
  ]
}
```

---

### 10. Location Subtype Mappings (LocationSubtypeRegistry)

**Currently Hardcoded:** `SpatialModel.ExtractFrom()` (line 151-158)

```csharp
// CURRENT:
"Drink" => "social_venue",
"Eat" => "social_venue",
// Building.Type → Location.Type mapping
```

**Properties:**
- Subtype (e.g., "Eat", "Drink", "Sleep")
- Maps to location type (e.g., "social_venue")
- Optional: weight/priority if multiple matches

**Used In:**
- `SpatialModel.ExtractFrom()` — building classification
- Probably future: behavior hints

**Configuration Example:**
```json
{
  "/locationSubtypes": {
    "Eat": "social_venue",
    "Drink": "social_venue",
    "Sleep": "home",
    "Work": "workplace",
    "Shop": "shop"
  }
}
```

---

## Abstraction Priority Matrix

| System | Complexity | Impact | Implementation Effort | Priority |
|--------|-----------|--------|----------------------|----------|
| Roles | Medium | Very High | Medium | 🔴 High |
| Interaction Types | Medium | Very High | Medium | 🔴 High |
| Relationship Tiers | Low | Medium | Low | 🟡 Medium |
| Group Types | Medium | Medium | Medium | 🟡 Medium |
| Desperation Model | Low | High | Low | 🟡 Medium |
| Morality Drift | Low | Medium | Low | 🟡 Medium |
| Request Types | Low | Medium | Low | 🟡 Medium |
| Location Types | Low | Low | Medium | 🟢 Low |
| NPC Properties | Medium | Very High | High | 🔴 High |
| Subtype Mappings | Low | Low | Low | 🟢 Low |

---

## Recommended Implementation Order

1. **Phase 1:** Roles + Interactions (already proposed) — foundation for everything else
2. **Phase 2:** Relationship Tiers + Group Types — moderate effort, good ROI
3. **Phase 3:** Desperation Model + Morality Drift — low effort, tuning benefit
4. **Phase 4:** Request Types — low effort, enables Tier 3 request customization
5. **Phase 5:** NPC Properties — high complexity, enables role customization without code
6. **Phase 6:** Location Types + Subtype Mappings — polish/refinement

---

## Key Insight: The "Glue" Layers

Some systems are "glue" that connect other systems:

- **Roles** connect to: properties, locations, interactions
- **Interaction Types** connect to: roles (who can do it), properties (conditions), trust (delta)
- **Relationship Tiers** connect to: interaction types (conditions)
- **Group Types** connect to: roles, interaction types

Implementing Roles + Interactions first gives you a foundation for the others.

---

## Configuration Loading Strategy

All these registries should load via `Loader.WhenLoaded()` hooks:

```csharp
// In TaleModule initialization:
var loader = I.Get<engine.casette.Loader>();

loader.WhenLoaded("/roles", (path, node) =>
    PopulateRegistry(I.Get<RoleRegistry>(), node));

loader.WhenLoaded("/interactions", (path, node) =>
    PopulateRegistry(I.Get<InteractionTypeRegistry>(), node));

loader.WhenLoaded("/relationshipTiers", (path, node) =>
    PopulateRegistry(I.Get<RelationshipTierRegistry>(), node));

// ... and so on
```

This creates a clean, parallel loading pattern for all TALE config.
