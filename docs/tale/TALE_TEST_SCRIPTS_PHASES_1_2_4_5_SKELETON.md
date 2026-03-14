# TALE Phase 1, 2, 4, 5 Test Scripts — SKELETON

This document outlines test categories and script structure for remaining phases. Each phase follows the same pattern:
- **20+ JSON test scripts** in `models/tests/tale/phaseX-name/`
- Same metadata, step types, and execution model as Phase 0 & 3
- Organized by feature category

---

## Phase 1: Storylets (JSON-Driven Story Selection)

**Location**: `models/tests/tale/phase1-storylets/`

### Test Categories (20+ scripts)

#### Library Loading (3 tests)
| # | Name | Validates | Priority |
|---|------|-----------|----------|
| 01 | library-loading | LoadFromDirectory() loads all .json files | Critical |
| 02 | library-indexing | GetCandidates() returns role-specific + universal | High |
| 03 | fallback-selection | GetFallback() returns rest/wander at night/day | High |

#### Precondition Matching (4 tests)
| # | Name | Validates | Priority |
|---|------|-----------|----------|
| 04 | property-precondition | hunger: {min: 0.6} matches NPC with hunger=0.8 | Critical |
| 05 | property-mismatch | Storylet skipped when preconditions fail | High |
| 06 | time-of-day-match | Storylet only available during time_of_day window | High |
| 07 | role-filter | Non-matching roles excluded from candidates | High |

#### Selection & Duration (3 tests)
| # | Name | Validates | Priority |
|---|------|-----------|----------|
| 08 | weighted-selection | Higher weight = higher selection probability | High |
| 09 | duration-randomness | Duration varies within [min, max] | High |
| 10 | location-resolution | location: "workplace" resolves to NPC.WorkplaceLocationId | High |

#### Postconditions (3 tests)
| # | Name | Validates | Priority |
|---|------|-----------|----------|
| 11 | simple-postcondition | "wealth: +0.05" increases by 0.05 | High |
| 12 | multiple-postconditions | All postconditions applied atomically | High |
| 13 | clamped-values | Properties clamped to [0, 1] after postcondition | High |

#### Complex Scenarios (4 tests)
| # | Name | Validates | Priority |
|---|------|-----------|----------|
| 14 | desperation-gating | Desperate NPCs (desperation > 0.4) access crime storylets | Medium |
| 15 | nearest-venue | location: "nearest_shop_Eat" finds closest social_venue | Medium |
| 16 | role-specific-weight | Role-specific storylet weight > universal weight | Medium |
| 17 | universal-fallback | Universal storylets available to all roles when role-specific empty | Medium |
| 18 | combined-candidates | GetCandidates() merges universal + role-specific | Medium |
| 19 | json-parse-error | Invalid JSON handled gracefully (logs error, skips file) | Low |
| 20 | empty-library | Empty library returns empty candidates, uses fallbacks | Low |

### Implementation Notes
- Test 04: Set NPC hunger property, load storylets, select, verify precondition checked
- Test 08: Select 100 times, histogram weights, verify statistical distribution
- Test 18: Verify combined list has no duplicates, correct ordering

---

## Phase 2: Strategies (Multi-Phase Quest Composition)

**Location**: `models/tests/tale/phase2-strategies/`

### Test Categories (20+ scripts)

#### Strategy Data Structure (3 tests)
| # | Name | Validates | Priority |
|---|------|-----------|----------|
| 01 | strategy-creation | AOneOfStrategy created with phases array | Critical |
| 02 | initial-phase | Strategy starts at phase 0 | High |
| 03 | phase-access | CurrentPhaseIndex, CurrentPhase properties accessible | High |

#### Phase Transitions (4 tests)
| # | Name | Validates | Priority |
|---|------|-----------|----------|
| 04 | explicit-transition | Transition(newPhase) changes CurrentPhaseIndex | Critical |
| 05 | sequential-phases | Phase 0 → 1 → 2 (taxi: pickup → drive → dropoff) | Critical |
| 06 | transition-precondition | Transition only if precondition met | High |
| 07 | strategy-completion | IsDone returns true at final phase | High |

#### Phase-Specific Behavior (3 tests)
| # | Name | Validates | Priority |
|---|------|-----------|----------|
| 08 | phase-storylets | Different phases have different candidate storylets | High |
| 09 | phase-lockdown | Non-current-phase storylets filtered out | High |
| 10 | phase-timeout | Phase auto-advances if timeout expires | High |

#### DES Integration (4 tests)
| # | Name | Validates | Priority |
|---|------|-----------|----------|
| 11 | strategy-as-storylet | Strategy selected/runs like a storylet | High |
| 12 | multi-phase-taxi | Taxi: passenger waits (P0), driver arrives (P1), drives (P2) | High |
| 13 | strategy-interrupt | Active strategy interrupted by high-priority quest | Medium |
| 14 | strategy-resume | After interrupt, strategy resumes from saved phase | Medium |

#### Failure & Advanced (3 tests)
| # | Name | Validates | Priority |
|---|------|-----------|----------|
| 15 | strategy-failure-path | Strategy can transition to failure phase | Medium |
| 16 | strategy-timeout-fallback | Timeout triggers fallback storylet | Medium |
| 17 | strategy-persistence | Strategy state persists across save/load | Medium |
| 18 | multi-npc-coordination | Multiple NPCs coordinate phases (e.g., taxi driver waits for passenger) | Low |
| 19 | strategy-nesting | Strategy can contain nested sub-strategies | Low |
| 20 | strategy-failure-recovery | Failed strategy allows NPC to retry or move on | Low |

### Implementation Notes
- Test 05: Track taxi strategy phases: Phase 0 = passenger_waiting, Phase 1 = driver_en_route, Phase 2 = passenger_delivered
- Test 11: Verify strategy returned from GetCandidates() and executed like storylet
- Test 18: Verify both NPCs reach same location before phase 1 → 2 transition

---

## Phase 4: Player Integration (Quests & Navigation)

**Location**: `models/tests/tale/phase4-player/`

### Test Categories (20+ scripts)

#### Quest System Basics (3 tests)
| # | Name | Validates | Priority |
|---|------|-----------|----------|
| 01 | player-quest-trigger | Player meets NPC with active request → quest triggered | Critical |
| 02 | quest-data-structure | Quest has ID, title, description, phase, reward | High |
| 03 | quest-registry | QuestFactory maintains active quest list | High |

#### Quest Phases (3 tests)
| # | Name | Validates | Priority |
|---|------|-----------|----------|
| 04 | phase-0-wait | Phase 0: player waits for event/signal | High |
| 05 | phase-1-navigate | Phase 1: player navigates to location | High |
| 06 | phase-2-interact | Phase 2: player interacts with NPC, receives reward | High |

#### Satnav Integration (3 tests)
| # | Name | Validates | Priority |
|---|------|-----------|----------|
| 07 | satnav-route-creation | Active quest creates route marker + satnav path | High |
| 08 | satnav-progress | Satnav shows distance, updates on player movement | High |
| 09 | satnav-arrival | Satnav clears when player reaches destination | High |

#### Quest Log UI (3 tests)
| # | Name | Validates | Priority |
|---|------|-----------|----------|
| 10 | quest-log-display | Quest Log shows active, completed, failed quests | High |
| 11 | quest-follow-unfollow | Player can follow/unfollow (max 1 followed) | High |
| 12 | quest-auto-advance | On completion, next quest auto-follows | High |

#### Multiple Quests (3 tests)
| # | Name | Validates | Priority |
|---|------|-----------|----------|
| 13 | multiple-active | Player can have multiple active quests | High |
| 14 | quest-priority | High-priority quest interrupts lower-priority | Medium |
| 15 | quest-abandonment | Player can abandon active quest | Medium |

#### Feedback & Rewards (2 tests)
| # | Name | Validates | Priority |
|---|------|-----------|----------|
| 16 | quest-triggered-toast | Toast shown when quest triggered | Medium |
| 17 | quest-completion-toast | Toast shown on completion with reward | Medium |
| 18 | quest-reward-application | Inventory/gold updated on completion | Medium |
| 19 | quest-failure-conditions | Quest can fail (timeout, dialogue choice) | Medium |
| 20 | npc-dialogue-integration | Quest triggered via dialogue, not just encounters | Low |

### Implementation Notes
- Test 01: Player character reaches location with active NPC request, verify quest triggers
- Test 07: Verify route marker at destination, satnav path calculated
- Test 11: Verify UI shows follow button, click follow, verify quest highlighted

---

## Phase 5: Escalation (Crime Waves, Gang Formation)

**Location**: `models/tests/tale/phase5-escalation/`

### Test Categories (20+ scripts)

#### Crime Detection (3 tests)
| # | Name | Validates | Priority |
|---|------|-----------|----------|
| 01 | crime-detection | Crime events (pickpocket, theft) detected and logged | Critical |
| 02 | crime-wave-threshold | N crimes in time window triggers crime wave alert | Critical |
| 03 | crime-location-clustering | Crimes in same location = higher severity | High |

#### Authority Behavior (3 tests)
| # | Name | Validates | Priority |
|---|------|-----------|----------|
| 04 | authority-patrol | Authority NPCs patrol high-crime areas | High |
| 05 | authority-encounter | Authority encounters criminal with warrant | High |
| 06 | authority-arrest | Authority attempts arrest; criminal flees or fights | High |

#### Gang Formation (3 tests)
| # | Name | Validates | Priority |
|---|------|-----------|----------|
| 07 | group-formation | GroupDetector identifies criminals forming group | High |
| 08 | group-solidarity | Group members aid each other in encounters | High |
| 09 | group-territory | Group claims territory, defends it | Medium |

#### Economic Crime (3 tests)
| # | Name | Validates | Priority |
|---|------|-----------|----------|
| 10 | blackmail-chain | Blackmailer extracts wealth repeatedly | Medium |
| 11 | fence-stolen-goods | Criminal fences goods to merchant | Medium |
| 12 | robbery-escalation | Isolated robbery → organized theft ring | Medium |

#### Conflict & Escalation (3 tests)
| # | Name | Validates | Priority |
|---|------|-----------|----------|
| 13 | trust-violation | Betrayal causes internal group conflict | Medium |
| 14 | revenge-cycle | Victim seeks revenge, triggers counter-action | Medium |
| 15 | authority-vs-gang | Authority raids gang hideout | Medium |

#### Wave Lifecycle (2 tests)
| # | Name | Validates | Priority |
|---|------|-----------|----------|
| 16 | wave-intensification | Crime wave increases in frequency over days | Low |
| 17 | wave-de-escalation | Successful arrests reduce wave over time | Low |
| 18 | wave-resolution | Crime wave ends when perpetrators arrested/flee | Low |

#### Cascading Effects (2 tests)
| # | Name | Validates | Priority |
|---|------|-----------|----------|
| 19 | wave-economy-impact | Crime reduces merchant activity, increases prices | Low |
| 20 | wave-narrative-integration | Crime wave affects player quest availability | Low |

### Implementation Notes
- Test 01: Track "pickpocket" request emissions; verify logged as crime events
- Test 02: Emit 5+ crimes in 30-min window, verify crime wave status changes
- Test 07: Run 30-day simulation, verify GroupDetector identifies crime groups
- Test 10: Blackmailer emits blackmail request daily, victim loses 5% wealth each day

---

## Cross-Phase Test Patterns

All tests use:
```json
{
  "name": "descriptive-name",
  "description": "What this validates",
  "phase": "phase-N",
  "category": "feature-category",
  "priority": "critical|high|medium|low",
  "globalTimeout": 60,
  "steps": [
    {"expect": {"type": "event.type", "code": "optional"}, "timeout": 30, "comment": "..."},
    {"inject": {"type": "event.type", "code": "value"}, "comment": "..."},
    {"sleep": 1000, "comment": "..."},
    {"action": "quit", "result": "pass|fail"}
  ]
}
```

---

## Summary

| Phase | Scripts | Categories | Est. Lines |
|-------|---------|-----------|-----------|
| 0 | 20 | 6 | ~500 |
| 1 | 20 | 5 | ~500 |
| 2 | 20 | 5 | ~500 |
| 3 | 22 | 6 | ~600 (detailed in TALE_TEST_SCRIPTS_PHASE_3.md) |
| 4 | 20 | 6 | ~500 |
| 5 | 20 | 6 | ~500 |
| **Total** | **122** | **~34** | **~3,600** |

---

## Implementation Roadmap

1. **Phase 3** (DONE): Detailed specs + 22 test scripts
2. **Phase 0**: Core DES engine tests (foundation)
3. **Phase 1**: Storylet JSON loading & selection
4. **Phase 2**: Multi-phase strategy composition
5. **Phase 4**: Player quest integration
6. **Phase 5**: Escalation & emergent conflict

Each phase depends on previous phases' core functionality passing.
