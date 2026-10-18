# Phase 7 Test Scripts — Spatial Grounding & Navigation

> **Status:** placeholders, not executable · **Verified:** 2026-10-18 (regenerated from the directory, KAR-431)

**Test location:** `models/tests/tale/phase7-spatial/` — 102 scripts.
**Design doc:** `../phases/PHASE_7.md`.

## What these scripts are

The 102 files are **outlines, not tests**. Each has `title`, `description`,
`category`, `expectedResult` and three `steps` of the form
`{"step": "setup" | "populate" | "verify", "action": "<prose>"}`. The
ExpectEngine (`ExpectEngine/ScriptRunner.cs`) treats `action` as a command
and knows only `quit` (`_executeAction`, `:153-164`), so every script would
end at step 0 with **Error "Unknown action: …"**. That is why phase 7 is not
in any tier of `run_tests.sh`: `all`, `standard` and `full` list the other
eleven phases, and there is no `phase7` filter. The listing below is what
each outline intends to check.

Phase 7 behaviour that **is** tested runs in xUnit, e.g.
`tests/JoyceCode.Tests/engine/tale/StreetEntryPositionTests.cs` (junction
entry candidates). Turning the outlines into real scripts (or dropping them)
belongs with KAR-433, the ticket for the TALE scripts that assert nothing.

## Running one anyway

`run_tests.sh` accepts a file name and finds it under `models/tests/tale/`:

```bash
./run_tests.sh 01-spatial-model-basic.json   # currently reports an error, see above
```

## Scripts by category


### SpatialModel Extraction (01–15)

| # | File | Intends to check |
|---|---|---|
| 1 | `01-spatial-model-basic.json` | Extract SpatialModel from cluster with single building |
| 2 | `02-spatial-model-multiple-buildings.json` | Extract SpatialModel with multiple buildings |
| 3 | `03-spatial-model-shops.json` | Extract shops into social_venue locations |
| 4 | `04-spatial-model-street-points.json` | Include street points as street_segment locations |
| 5 | `05-spatial-model-empty-cluster.json` | Handle cluster with no buildings gracefully |
| 6 | `06-spatial-model-index-building.json` | SpatialModel builds location index correctly |
| 7 | `07-spatial-model-get-location.json` | GetLocation resolves location by ID |
| 8 | `08-spatial-model-get-invalid-location.json` | GetLocation returns null for invalid ID |
| 9 | `09-spatial-model-building-count.json` | BuildingCount tracks non-shop buildings |
| 10 | `10-spatial-model-shop-count.json` | ShopCount counts shop locations |
| 11 | `11-spatial-model-street-point-count.json` | StreetPointCount tracks street segments |
| 12 | `12-spatial-model-mixed-shops.json` | Extract different shop types (Eat, Drink, Game2) |
| 13 | `13-spatial-model-capacity-calculation.json` | Location capacity computed from building height |
| 14 | `14-spatial-model-quarter-index.json` | Locations track quarter index |
| 15 | `15-spatial-model-estate-index.json` | Locations track estate index |

### Location Type Assignment (16–30)

| # | File | Intends to check |
|---|---|---|
| 16 | `16-assign-merchant-to-shop.json` | Merchant NPC assigned to shop location |
| 17 | `17-assign-merchant-to-social-venue.json` | Merchant NPC assigned to social venue |
| 18 | `18-assign-worker-to-workplace.json` | Worker NPC assigned to workplace |
| 19 | `19-assign-worker-to-home.json` | Worker NPC assigned to home |
| 20 | `20-assign-authority-to-workplace.json` | Authority NPC assigned to workplace |
| 21 | `21-assign-socialite-to-social-venue.json` | Socialite NPC assigned to social venue |
| 22 | `22-assign-drifter-to-street.json` | Drifter NPC can be assigned to street segment |
| 23 | `23-location-assignment-deterministic.json` | Location assignment is deterministic per NPC seed |
| 24 | `24-location-assignment-different-npcs.json` | Different NPCs get different location assignments |
| 25 | `25-location-fallback-empty-type.json` | Fallback to any location if type bucket empty |
| 26 | `26-role-weights-merchant-heavy.json` | Merchant role has appropriate weights |
| 27 | `27-role-weights-worker-heavy.json` | Worker role has appropriate weights |
| 28 | `28-role-weights-drifter-light.json` | Drifter role has lower population weight |
| 29 | `29-npc-home-location.json` | NPC HomeLocationId set from SpatialModel |
| 30 | `30-npc-workplace-location.json` | NPC WorkplaceLocationId set from SpatialModel |

### Entry Position Computation (31–45)

| # | File | Intends to check |
|---|---|---|
| 31 | `31-entry-position-shop-front.json` | Shop entry position calculated from shop front |
| 32 | `32-entry-position-midpoint.json` | Shop entry position uses midpoint of front points |
| 33 | `33-entry-position-street-height.json` | Entry position projected to street height |
| 34 | `34-entry-position-building-center.json` | Building entry position at center X/Z |
| 35 | `35-entry-position-non-zero.json` | EntryPosition is non-zero for buildings |
| 36 | `36-entry-position-street-segment.json` | Street segment entry position equals position |
| 37 | `37-entry-position-y-coordinate.json` | EntryPosition Y matches street height |
| 38 | `38-entry-position-shop-no-points.json` | Handle shop with no front points |
| 39 | `39-entry-position-single-point-shop.json` | Shop with single point uses that point |
| 40 | `40-entry-position-z-offset.json` | Building entry Z-offset for quarter geometry |
| 41 | `41-entry-position-cluster-offset.json` | Entry position includes cluster offset |
| 42 | `42-location-position-vs-entry.json` | Location.Position vs EntryPosition distinction |
| 43 | `43-entry-position-drink-shop.json` | Entry position for Drink venue |
| 44 | `44-entry-position-eat-shop.json` | Entry position for Eat venue |
| 45 | `45-entry-position-game-shop.json` | Entry position for Game2 shop |

### Travel Time Calculations (46–60)

| # | File | Intends to check |
|---|---|---|
| 46 | `46-travel-time-same-location.json` | Travel time zero for same location |
| 47 | `47-travel-time-euclidean.json` | Travel time uses Euclidean distance |
| 48 | `48-travel-time-walking-speed.json` | Travel time based on 75 m/min walking speed |
| 49 | `49-travel-time-bidirectional.json` | Travel time same in both directions |
| 50 | `50-travel-time-short-distance.json` | Short distance yields short travel time |
| 51 | `51-travel-time-long-distance.json` | Long distance yields long travel time |
| 52 | `52-find-nearest-shop.json` | FindNearestOfType locates closest shop location |
| 53 | `53-find-nearest-social-venue.json` | FindNearestOfType locates closest social venue |
| 54 | `54-find-nearest-street-segment.json` | FindNearestOfType locates closest street point |
| 55 | `55-find-nearest-no-match.json` | FindNearestOfType returns -1 if no match |
| 56 | `56-find-nearest-exact-type.json` | FindNearestOfType respects shop type filter |
| 57 | `57-find-nearest-eat-venue.json` | FindNearestOfType filters Eat venues |
| 58 | `58-find-nearest-drink-venue.json` | FindNearestOfType filters Drink venues |
| 59 | `59-travel-time-null-location.json` | Travel time returns 0 for null location |
| 60 | `60-travel-time-mixed-types.json` | Travel time works across different location types |

### Building Occupancy & Indoor Activities (61–75)

| # | File | Intends to check |
|---|---|---|
| 61 | `61-indoor-activity-home.json` | Home location triggers IsIndoorActivity |
| 62 | `62-indoor-activity-workplace.json` | Workplace location triggers IsIndoorActivity |
| 63 | `63-indoor-activity-shop.json` | Shop location triggers IsIndoorActivity |
| 64 | `64-indoor-activity-social-venue.json` | Social venue location triggers IsIndoorActivity |
| 65 | `65-outdoor-activity-street.json` | Street segment keeps IsIndoorActivity false |
| 66 | `66-indoor-skip-idle-behavior.json` | Indoor activity skips idle behavior setup |
| 67 | `67-indoor-timer-active.json` | Timer still active during indoor activity |
| 68 | `68-indoor-behavior-removed.json` | Behavior component not set for indoor activity |
| 69 | `69-outdoor-idle-animation.json` | Outdoor activity shows idle animation |
| 70 | `70-indoor-give-up-signal.json` | GiveUpStrategy fires even for indoor activity |
| 71 | `71-indoor-timer-fires.json` | Timer fires correctly for indoor activity |
| 72 | `72-indoor-duration-respected.json` | Stay duration respected for indoor activities |
| 73 | `73-indoor-repeated.json` | Multiple indoor activities handled correctly |
| 74 | `74-indoor-outdoor-transition.json` | Transition from indoor to outdoor activity |
| 75 | `75-outdoor-indoor-transition.json` | Transition from outdoor to indoor activity |

### Fragment-Accurate Position Tracking (76–90)

| # | File | Intends to check |
|---|---|---|
| 76 | `76-current-world-position-zero.json` | CurrentWorldPosition initialized to zero |
| 77 | `77-current-world-position-set.json` | CurrentWorldPosition set on NPC advancement |
| 78 | `78-current-world-position-from-entry.json` | CurrentWorldPosition uses location entry position |
| 79 | `79-get-npc-in-fragment-home.json` | GetNpcsInFragment includes NPCs in home fragment |
| 80 | `80-get-npc-in-fragment-current.json` | GetNpcsInFragment uses current position not home |
| 81 | `81-get-npc-in-fragment-workplace.json` | NPC in workplace fragment appears there |
| 82 | `82-get-npc-in-fragment-office.json` | Worker in office appears in office fragment |
| 83 | `83-get-npc-in-fragment-shop.json` | Merchant in shop appears in shop fragment |
| 84 | `84-get-npc-in-fragment-fallback.json` | GetNpcsInFragment falls back to home if current is zero |
| 85 | `85-current-position-persists.json` | CurrentWorldPosition persists across updates |
| 86 | `86-current-position-serialized.json` | CurrentWorldPosition saved to deviated NPC data |
| 87 | `87-current-position-deserialized.json` | CurrentWorldPosition restored from save data |
| 88 | `88-current-position-zero-not-saved.json` | Zero position not persisted unnecessarily |
| 89 | `89-fragment-index-accuracy.json` | Fragment index correctly computed from current position |
| 90 | `90-multiple-npcs-same-fragment.json` | Multiple NPCs can be in same fragment |

### Integration & Edge Cases (91–102)

| # | File | Intends to check |
|---|---|---|
| 91 | `91-phase7-full-workflow.json` | Complete workflow: extract spatial model, assign NPCs, set positions |
| 92 | `92-phase7-npc-spawn-location.json` | NPC spawns at correct building entry point |
| 93 | `93-phase7-deviated-npc-persisted.json` | Deviated NPC position preserved across save/load |
| 94 | `94-phase7-multiple-clusters.json` | SpatialModel correctly tracks multiple clusters |
| 95 | `95-phase7-cluster-depopulation.json` | SpatialModel removed when cluster depopulated |
| 96 | `96-phase7-npc-advancement.json` | NPC advancement updates CurrentWorldPosition |
| 97 | `97-phase7-entry-vs-position.json` | EntryPosition used for appearance, Position for queries |
| 98 | `98-phase7-street-height-consistency.json` | Street height consistent across all locations |
| 99 | `99-phase7-location-id-collision.json` | No ID collisions across location types |
| 100 | `100-phase7-role-distribution.json` | NPCs distributed among roles by weights |
| 101 | `101-phase7-large-cluster.json` | System handles large cluster with many buildings |
| 102 | `102-phase7-edge-case-tiny-cluster.json` | Handle cluster with minimal buildings |
