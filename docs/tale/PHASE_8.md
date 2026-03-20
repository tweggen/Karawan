# TALE Phase 8: Occupation-Based Character Model Assignment

**Status:** ✅ Implementation Complete (2026-03-20)

## Overview

Phase 8 adds configuration-driven character model assignment based on NPC occupation (role). Instead of all NPCs selecting randomly from the full 12-model pool, each occupation now has a curated set of visually appropriate models.

For example:
- **Police officers** always use police models (`man_police_Rig.fbx`, `woman_police_Rig.fbx`)
- **Factory workers** use blue-collar models (`man_casual_Rig.fbx`, `man_coat_winter_Rig.fbx`, `woman_carpenter_Rig.fbx`, `woman_mechanic_Rig.fbx`)
- **Office workers** use professional models (`man_business_Rig.fbx`, `man_scientist_Rig.fbx`, `woman_doctor_Rig.fbx`, `woman_actionhero_Rig.fbx`)
- **Legacy roles** (drifter, worker) fall back to the full random pool

## Architecture

### RoleDefinition.cs
- **New field:** `public List<string> Models { get; set; }`
- Optional list of FBX model filenames
- If null or empty, defaults to the full 12-model pool

### Configuration: nogame.roles.json
Each role entry now includes a `"models"` array:

```json
{
  "id": "authority",
  "displayName": "Police Officer",
  "models": ["man_police_Rig.fbx", "woman_police_Rig.fbx"],
  // ... other fields
}
```

Roles without a `"models"` field (or with `"models": null`) fall back to the default behavior.

### CharacterModelDescriptionFactory.cs
**New overload:**
```csharp
public static CharacterModelDescription CreateCitizen(RandomSource rnd, IList<string> models = null)
```

- If `models` is provided and non-empty, selects from that pool
- Otherwise uses the default 12-model array `_arrModels`
- Deterministic: uses the same NPC random seed, so model reassignment is stable

### TaleSpawnOperator.cs
When spawning a TALE NPC:
1. Retrieves the NPC's role from `schedule.Role`
2. Looks up `RoleRegistry.Get(role)?.Models`
3. Passes the model pool to `CharacterModelDescriptionFactory.CreateCitizen(npcRnd, roleModels)`

## Role → Model Mappings

| Role(s) | Model Pool |
|---------|-----------|
| `factory_worker`, `factory_worker_eve`, `factory_worker_night`, `service_worker` | `man_casual_Rig.fbx`, `man_coat_winter_Rig.fbx`, `woman_carpenter_Rig.fbx`, `woman_mechanic_Rig.fbx` |
| `merchant` | `man_business_Rig.fbx`, `woman_large_Rig.fbx` |
| `office_worker` | `man_business_Rig.fbx`, `man_scientist_Rig.fbx`, `woman_doctor_Rig.fbx`, `woman_actionhero_Rig.fbx` |
| `authority` | `man_police_Rig.fbx`, `woman_police_Rig.fbx` |
| `socialite` | `man_casual_Rig.fbx`, `man_business_Rig.fbx`, `woman_actionhero_Rig.fbx`, `woman_race_car_driver_Rig.fbx` |
| `drifter`, `worker` (legacy) | *(no `models` field → falls back to full pool)* |

## Build & Test Status

✅ **Build:** `dotnet build nogame/nogame.csproj` succeeds without errors
✅ **Tests:** All 171 regression tests pass (Phase 0-7 all passing, no behavior changes)
✅ **Determinism:** NPCs get stable models across save/load cycles (same seed = same model)

## Files Modified

1. **JoyceCode/engine/tale/RoleDefinition.cs** — Added `Models` property
2. **models/nogame.roles.json** — Added `"models"` arrays to all 9 role definitions
3. **nogameCode/nogame/characters/citizen/CharacterModelDescriptionFactory.cs** — Added optional `models` parameter to `CreateCitizen()`
4. **nogameCode/nogame/characters/citizen/TaleSpawnOperator.cs** — Look up role's model pool in line 166-169
5. **CLAUDE.md** — Updated Phase 8 status

## Visual Behavior

When running the game:
- **Police NPCs** now appear with police uniforms only
- **Factory/service workers** appear in casual/worker outfits (no business or police attire)
- **Office workers** appear in professional/scientist/doctor/action outfits
- **Merchants** appear in business or large-frame outfits
- **Socialites** appear in mixed casual/business/action/driver outfits
- **Drifters** and legacy workers use the full random pool (no curation)

This provides immediate visual feedback about an NPC's occupation without reading UI labels.

## Future Refinements

- Add more role-specific models (construction workers, security, kitchen staff, etc.)
- Expand model pool beyond 12 total models
- Add gender-aware model selection (e.g., force-male roles to use male models)
- Add model prevalence tuning per role (e.g., 80% businessmen, 20% scientists for office workers)
