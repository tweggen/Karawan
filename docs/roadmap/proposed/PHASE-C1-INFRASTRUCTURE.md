# Plan: Phase C1 - NPC Conversation Infrastructure

**Status:** 📋 Ready for Implementation
**Created:** 2026-04-07
**Estimated Effort:** 2-3 hours
**Complexity:** Low-Medium

---

## Objectives

1. ✅ Any outdoor TALE NPC can be approached and spoken to
2. ✅ Conversations reflect NPC's most extreme property (hunger, anger, fatigue)
3. ✅ Role-specific dialogue fallbacks (worker, merchant, drifter, etc.)
4. ✅ Infrastructure foundation for C2-C4 (property injection, script resolution)
5. ✅ All 8 C1 test cases pass (behavior, property branching, role fallback, indoor exclusion)

---

## Success Criteria

- [ ] Player can press E near outdoor NPC, see "E to Talk" prompt at 12m
- [ ] Dialogue script runs and NPC speaks a contextual line
- [ ] Hungry NPC (hunger > 0.7) triggers food-related dialogue
- [ ] Angry NPC (anger > 0.7) triggers dismissive dialogue
- [ ] Tired NPC (fatigue > 0.7) triggers exhaustion dialogue
- [ ] Balanced NPC triggers neutral default dialogue
- [ ] Worker/Merchant fallback dialogues work correctly
- [ ] Indoor NPCs do NOT have conversation behavior attached
- [ ] All 8 test cases pass: `./run_tests.sh phaseC1`
- [ ] Smoke tests still pass: `./run_tests.sh smoke`
- [ ] Standard regression tests still pass: `./run_tests.sh standard`

---

## Files to Create

### 1. Core Code Files

#### `nogameCode/nogame/characters/citizen/TaleConversationBehavior.cs`
- Extends `ANearbyBehavior` (see `niceday` NPC as reference)
- Constructor: takes distance (12m), prompt text ("E to Talk")
- `OnAction()` method:
  1. Get NPC ID from entity (via some identifier)
  2. Call `TaleManager.GetSchedule(npcId)` to get NpcSchedule
  3. Call `TaleNarrationBindings.InjectNpcProps(schedule)` to inject props
  4. Call `TaleNarrationBindings.ResolveScript()` with storylet info to get script name
  5. Call narration manager to trigger conversation
  6. (Cleanup happens in ScriptEndedEvent handler)
- Properties: `_taleManager`, `_narrationManager` (injected or cached)

#### `nogameCode/nogame/modules/tale/TaleNarrationBindings.cs`
- Static helper class (like `NarrationBindings.cs`)
- Methods:
  - `static void Register(INarrationManager manager)` — Called once at module startup
    - Subscribe to `ScriptEndedEvent`
    - Register future functions (C3: npcMood, npcRole, npcWealthLabel)
  - `static void InjectNpcProps(NpcSchedule schedule, Props props)` — Write props
    - Inject: hunger, anger, fatigue, health, wealth, happiness, reputation, morality, fear
    - Inject: role, storylet_id, group_id
    - Later phases (C4): met_player, trust_player
  - `static void ClearNpcProps(Props props)` — Remove all `npc.*` keys
  - `static string ResolveScript(StoryletDefinition storylet, string role)` — 5-level fallback
    - Level 1: Check storylet.ConversationScript (if C2 implemented)
    - Level 2: Try `tale.{storyletId}`
    - Level 3: Try first matching `tale.tag.{tag}` from storylet.Tags
    - Level 4: Try `tale.role.{role}`
    - Level 5: Fallback to `tale.generic`
- State:
  - `static Props _cachedProps` or pass as parameter
  - Event handler reference for cleanup

### 2. Conversation Script JSON Files

All in `models/tale/conversations/` directory (create if not exists):

#### `tale.generic.json`
- Entry node that branches on property conditions
- 4 branches: hungry, angry, tired, default
- Each branch has 1-2 simple lines
- See PHASE_C.md for exact spec

#### `tale.role.worker.json`
- Generic worker lines: "Just trying to get through the day", etc.
- Single entry node with texts

#### `tale.role.merchant.json`
- Business-focused lines: "Business has been good", etc.
- Single entry node with texts

#### `tale.role.drifter.json`
- Survival-focused lines: "Taking it day by day", etc.
- Single entry node with texts

#### `tale.role.socialite.json`
- Social lines: "Always something happening", etc.
- Single entry node with texts

#### `tale.role.authority.json`
- Authority lines: "Keep moving", etc.
- Single entry node with texts (C4 will expand with trust gates)

---

## Files to Modify

### 1. `TaleEntityStrategy.cs`
**Location:** `nogameCode/nogame/characters/citizen/TaleEntityStrategy.cs`

**In `_setupActivity()` method:**
- After setting `StayAtStrategyPart` and checking IsIndoorActivity == false
- If outdoor, instantiate and attach `TaleConversationBehavior` to entity.Behavior
- Pseudocode:
  ```csharp
  if (!stayAt.IsIndoorActivity)
  {
      var behavior = new TaleConversationBehavior(distance: 12, prompt: "E to Talk");
      entity.Set(behavior);  // Set as Behavior component
  }
  ```

**In `_advanceAndTravel()` method:**
- When transitioning out of activity phase
- Remove/clear the Behavior component (set to null or remove)
- Pseudocode:
  ```csharp
  if (entity.Has<ANearbyBehavior>())
  {
      entity.Remove<ANearbyBehavior>();  // or entity.Set<Behavior>(null)
  }
  ```

### 2. `TaleModule.cs`
**Location:** `nogameCode/nogame/modules/tale/TaleModule.cs`

**In `OnModuleActivate()` method:**
- After NarrationManager is available/initialized
- Call `TaleNarrationBindings.Register(narrationManager)`
- Pseudocode:
  ```csharp
  public override void OnModuleActivate(IModule module, GameState state)
  {
      // ... existing code ...
      if (module is INarrationManager narration)
      {
          TaleNarrationBindings.Register(narration);
      }
  }
  ```

### 3. `TaleManager.cs`
**Location:** `JoyceCode/engine/tale/TaleManager.cs`

**Add public getter:**
- Expose `public NpcSchedule? GetSchedule(int npcId)` method
- Simple accessor: `return _npcs.TryGetValue(npcId, out var schedule) ? schedule : null;`
- Or use existing internal method, just make public

### 4. `models/nogame.narration.json`
**Location:** `models/nogame.narration.json` (or equivalent narration config)

**Add include directive:**
- Add `"__include__": ["models/tale/conversations/"]` or similar
- Ensures all JSON files in conversations/ are loaded as narration scripts
- May already have include mechanism; verify existing pattern

### 5. `nogameCode.projitems` (if using .shproj)
**Location:** `nogameCode/nogameCode.projitems` or similar

**Add file references:**
- Include `TaleConversationBehavior.cs` and `TaleNarrationBindings.cs` in project
- May be automatic if in correct directory; verify

---

## Implementation Steps

### Step 1: Create TaleConversationBehavior.cs
- [ ] Read `niceday` NPC behavior implementation as reference
- [ ] Create class extending `ANearbyBehavior`
- [ ] Implement `OnAction()` calling narration trigger
- [ ] Add TaleManager and narration manager dependencies
- [ ] Test compilation

### Step 2: Create TaleNarrationBindings.cs
- [ ] Create static helper class
- [ ] Implement `InjectNpcProps()` — property mapping from NpcSchedule to Props
- [ ] Implement `ClearNpcProps()` — cleanup after script
- [ ] Implement `ResolveScript()` — 5-level fallback with null checks
- [ ] Implement `Register()` — hook into ScriptEndedEvent
- [ ] Test compilation

### Step 3: Modify TaleEntityStrategy.cs
- [ ] Read `_setupActivity()` to understand context
- [ ] Add behavior attachment in activity setup
- [ ] Add behavior removal in `_advanceAndTravel()`
- [ ] Test compilation

### Step 4: Modify TaleModule.cs
- [ ] Find `OnModuleActivate()` hook
- [ ] Add `TaleNarrationBindings.Register()` call
- [ ] Verify narration manager is available
- [ ] Test compilation

### Step 5: Expose TaleManager.GetSchedule()
- [ ] Locate internal schedule accessor
- [ ] Make public (or create public wrapper)
- [ ] Test compilation

### Step 6: Update nogame.narration.json
- [ ] Add `__include__` for conversations directory
- [ ] Verify syntax matches existing includes
- [ ] Test JSON loading (build step)

### Step 7: Create Conversation Scripts
- [ ] Create `models/tale/conversations/` directory
- [ ] Create `tale.generic.json` with property branching
- [ ] Create 5 role-specific scripts (worker, merchant, drifter, socialite, authority)
- [ ] Verify JSON formatting
- [ ] Test JSON loading (build step)

### Step 8: Test & Validate
- [ ] `dotnet build` (full solution, Release mode)
- [ ] `./run_tests.sh phaseC1` (8 tests should pass)
- [ ] `./run_tests.sh smoke` (smoke tests should still pass)
- [ ] `./run_tests.sh standard` (all 171 regression tests should still pass)

---

## Key Integration Points

### TaleManager Access
- `TaleConversationBehavior.OnAction()` needs to get NPC schedule
- May need to cache reference or look up via `ServiceLocator`
- Check how other TALE code accesses TaleManager

### Narration Manager Integration
- `TaleNarrationBindings.Register()` needs NarrationManager reference
- Called from `TaleModule.OnModuleActivate()` after narration is ready
- Verify module initialization order

### Props Injection Timing
- Props must be injected BEFORE narration script starts
- Props must be cleared AFTER script ends (via ScriptEndedEvent)
- SerialExecution assumption: only one active conversation at a time

### StoryletDefinition Access
- `ResolveScript()` receives StoryletDefinition (via TaleManager.GetCurrentStorylet?)
- Need to extract role and tags from storylet
- Verify StoryletDefinition has these fields

---

## Testing Strategy

### Unit Tests (Implicit via C1 test scripts)

**C1-01: Behavior Attachment**
- Spawn outdoor NPC, verify Behavior component is set
- Spawn indoor NPC, verify Behavior component is NOT set

**C1-02 to C1-05: Property Branching**
- Spawn NPC with specific property values (hunger=0.8, etc.)
- Trigger conversation, verify correct dialogue branch
- Check game logs for injected props

**C1-06 to C1-07: Role Fallback**
- Spawn worker/merchant at generic storylet (no conversation_script)
- Trigger conversation, verify role-specific fallback script used
- Check narration output

**C1-08: Indoor Exclusion**
- Spawn NPC with IsIndoorActivity=true
- Verify no E-prompt appears, behavior not attached

### Regression Tests
- Run `./run_tests.sh standard` (all 171 tests)
- Verify no regressions in existing phases
- Check test timing (should be minimal impact)

---

## Documentation Changes

### 1. Update CLAUDE.md
- [ ] Add Phase C to project status
- [ ] Note TaleConversationBehavior, TaleNarrationBindings in Architecture section
- [ ] Update testing tier recommendation (smoke tests now have C1 tests)

### 2. Update docs/TESTING.md
- [ ] Add Phase C to test phase table
- [ ] Document C1-C4 test categories (8+6+6+9 = 29 tests)
- [ ] Update total test count (now 171 + 29 = 200)
- [ ] Add example: `./run_tests.sh phaseC1`

### 3. Update docs/tale/PHASE_C.md
- [ ] Already exists; verify it matches implementation
- [ ] Add implementation date
- [ ] Note any deviations from spec

### 4. Create docs/tale/TALE_TEST_SCRIPTS_PHASE_C.md reference
- [ ] Already created; just verify C1 section is accurate

### 5. Commit message
```
Implement Phase C1: NPC Conversation Infrastructure

- Created TaleConversationBehavior (ANearbyBehavior subclass)
- Created TaleNarrationBindings (property injection, script resolution)
- Implemented 5-level script fallback (explicit > id > tag > role > generic)
- Created 6 conversation scripts (generic + 5 roles)
- Modified TaleEntityStrategy to attach/detach behavior
- Modified TaleModule to register bindings
- Exposed TaleManager.GetSchedule() for conversation access

Features:
- Outdoor NPCs have "E to Talk" prompt at 12m
- Dialogue reflects NPC's most extreme property
- Role-specific fallback for generic storylets
- Indoor NPCs excluded from conversations

All 8 Phase C1 tests passing
All 171 regression tests still passing
Smoke tests still passing

Updated docs/TESTING.md, docs/tale/PHASE_C.md
```

---

## Risk Factors & Mitigation

| Risk | Impact | Mitigation |
|------|--------|-----------|
| **TaleManager not accessible** | Can't get NPC schedule | Verify access pattern with existing TALE code; may need ServiceLocator or DI |
| **Narration scripts not loading** | Conversations don't play | Test JSON syntax; verify __include__ directive works |
| **Behavior attachment interferes** | Crashes or state conflicts | Test indoor/outdoor logic carefully; ensure cleanup in _advanceAndTravel |
| **Props collision** | Narration uses wrong values | Use `npc.` prefix consistently; verify no key name collisions |
| **Event ordering issue** | Props cleared before script uses them | Verify ScriptEndedEvent fires AFTER script execution |

---

## Blockers & Dependencies

- ✅ NarrationManager must be initialized
- ✅ TaleManager must be available and populated
- ✅ ANearbyBehavior base class must exist
- ✅ ScriptEndedEvent must be fired by narration system
- ✅ StoryletDefinition must be loaded with Tags field

All appear to be satisfied by existing code.

---

## Rollback Plan

If implementation encounters critical issues:
1. Revert TaleEntityStrategy changes (behavior attachment)
2. Remove TaleConversationBehavior and TaleNarrationBindings
3. Remove conversation scripts
4. Revert TaleModule.cs changes
5. Run `./run_tests.sh standard` to verify no regressions

---

## Future Extensions (C2-C4)

**Phase C2** will:
- Add `StoryletDefinition.ConversationScript` field
- Update `ResolveScript()` to check this field first
- Create tag-based scripts (routine, eating, economic, rest)

**Phase C3** will:
- Add `func.npcMood()`, `func.npcWealthLabel()`, `func.npcRole()` functions
- Register in `TaleNarrationBindings.Register()`
- Expand existing scripts with property-reactive branches

**Phase C4** will:
- Add trust tracking in `schedule.Trust[-1]`
- Inject `npc.met_player` and `npc.trust_player` in `InjectNpcProps()`
- Add `tale.npc.remember` event handler
- Extend authority/merchant scripts with trust gates

---

## Questions Before Implementation

1. **NPC ID tracking** — How does entity know its NPC ID? Via component, convention, lookup?
2. **Behavior attachment pattern** — Confirm ANearbyBehavior pattern with existing code
3. **Narration manager caching** — Cache in TaleNarrationBindings or pass per-call?
4. **Error handling** — Silent fallback if schedule not found, or log error?
5. **Test expectations** — How are logs verified in test scripts? Event-based or text parsing?
