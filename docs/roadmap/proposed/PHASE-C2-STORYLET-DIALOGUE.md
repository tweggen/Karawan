# Plan: Phase C2 - Storylet-Specific Dialogue

**Status:** 📋 Ready for Implementation
**Created:** 2026-04-07
**Estimated Effort:** 1-2 hours
**Complexity:** Low
**Dependency:** Phase C1 (must be complete and building)

---

## Objectives

1. ✅ Storylets can declare their own conversation script via `conversation_script` field
2. ✅ Script resolution fallback recognizes explicit override (takes precedence over role)
3. ✅ Tag-based conversation scripts available for common activity types
4. ✅ Example: Workers on lunch break use food-related dialogue
5. ✅ All 6 C2 test cases pass (tag fallback, script override, precedence)

---

## Success Criteria

- [ ] StoryletDefinition has `public string? ConversationScript` field
- [ ] StoryletLibrary.LoadFrom() parses `"conversation_script"` from JSON
- [ ] TaleNarrationBindings.ResolveScript() checks ConversationScript first (Level 1)
- [ ] Level 2 (by ID), Level 3 (by tag), Level 4 (by role), Level 5 (generic) all work
- [ ] 4 tag-based scripts created: routine, eating, rest, economic
- [ ] Example: tale.lunch_break.json with wealth-gated branches
- [ ] All 6 C2 test cases pass: `./run_tests.sh phaseC2`
- [ ] C1 tests still passing: `./run_tests.sh phaseC1`
- [ ] Smoke/standard regression tests still pass
- [ ] No new compilation errors

---

## Files to Create

### 1. Conversation Scripts (4 tag-based + 1 storylet-specific)

#### `models/tale/conversations/tale.tag.routine.json`
- Generic routine/work activity lines
- Used by storylets tagged with "routine"
- Simple entry node, no branching yet
- Example: "Just getting through the day", "Same as always"

#### `models/tale/conversations/tale.tag.eating.json`
- Food/meal-focused lines
- Used by storylets tagged with "eating"
- Can branch on wealth (C3 feature, but skeleton ready)
- Example: "Something to eat", "Lunch time"

#### `models/tale/conversations/tale.tag.rest.json`
- Rest/sleep-focused lines
- Used by storylets tagged with "rest"
- Example: "Need some rest", "Getting some sleep"

#### `models/tale/conversations/tale.tag.economic.json`
- Work/money-focused lines
- Used by storylets tagged with "economic"
- Example: "Work's been good", "Making a living"

#### `models/tale/conversations/tale.lunch_break.json`
- **Storylet-specific** conversation script
- Explicitly referenced via `conversation_script` field in lunch_break storylet
- Wealth-gated branches:
  - wealth < 0.2 → poor_lunch (expensive, bring own food)
  - wealth ≥ 0.2 → normal_lunch (casual eating)
- Example lines:
  - Poor: "I bring my own food now. Lunch costs too much."
  - Normal: "Just grabbing something to eat. Break's almost over."

---

## Files to Modify

### 1. `JoyceCode/engine/tale/StoryletDefinition.cs`

**Add field** (after existing fields like `LocationRef`, `Tags`, etc.):
```csharp
/// <summary>
/// Optional explicit conversation script name.
/// If set, overrides the 4-level fallback resolution.
/// Example: "tale.lunch_break" to use a specific storylet's dialogue
/// </summary>
public string? ConversationScript { get; set; }
```

**Update parser** (in `StoryletLibrary.LoadFrom()` or similar JSON deserialization):
- Add case-insensitive parsing for `"conversation_script"` field
- Must handle `null` (optional field)
- Store in `ConversationScript` property

### 2. `nogameCode/nogame/modules/tale/TaleNarrationBindings.cs`

**Update ResolveScript() method**:
- Change Level 1 from checking storylet ID to checking `ConversationScript` field
- If `storylet.ConversationScript != null && !string.IsNullOrEmpty()`, return it immediately
- Keep rest of fallback (ID, tag, role, generic) as Levels 2-5

**Updated 5-level fallback**:
```
Level 1: storylet.ConversationScript (explicit override)         ← NEW
Level 2: tale.{storyletId} (auto-named by id)
Level 3: tale.tag.{firstMatchingTag} (from tags array)
Level 4: tale.role.{role}
Level 5: tale.generic
```

### 3. `models/tale/*.json` (Existing Storylet Files)

**Add `conversation_script` field to relevant storylets**:

Examples (not exhaustive):
- `worker.json` → lunch_break storylet: `"conversation_script": "tale.lunch_break"`
- `drifter.json` → beg storylet: (optional, can use tag fallback)
- Any storylet with tags=[`"routine"`] → will fallback to `tale.tag.routine`

**Pattern**:
```json
{
  "id": "lunch_break",
  "name": "Lunch Break",
  "roles": ["worker"],
  "tags": ["eating", "routine"],
  "conversation_script": "tale.lunch_break",
  ... rest of fields ...
}
```

**Search strategy**:
- Use `grep -r "lunch_break\|beg\|sleep" models/tale/*.json` to find relevant storylets
- Add `"conversation_script"` field to 3-5 representative storylets
- Leave most storylets without explicit script (test tag/role fallback)

---

## Implementation Steps

### Step 1: Add StoryletDefinition Field
- [ ] Read StoryletDefinition.cs to understand structure
- [ ] Add `public string? ConversationScript { get; set; }` property
- [ ] Find JSON deserialization code (likely in StoryletLibrary.LoadFrom())
- [ ] Add parsing for `"conversation_script"` field
- [ ] Handle null/empty gracefully
- [ ] Test compilation

### Step 2: Update TaleNarrationBindings
- [ ] Read current ResolveScript() implementation (from C1)
- [ ] Add check for `storylet.ConversationScript` as Level 1
- [ ] Verify Level 2-5 still work correctly
- [ ] Add trace log for Level 1 match
- [ ] Test compilation

### Step 3: Create Tag-Based Scripts
- [ ] Create `models/tale/conversations/tale.tag.routine.json` (generic work lines)
- [ ] Create `models/tale/conversations/tale.tag.eating.json` (food lines)
- [ ] Create `models/tale/conversations/tale.tag.rest.json` (sleep lines)
- [ ] Create `models/tale/conversations/tale.tag.economic.json` (work/money lines)
- [ ] Verify JSON syntax (no syntax errors)

### Step 4: Create Storylet-Specific Script
- [ ] Create `models/tale/conversations/tale.lunch_break.json`
- [ ] Entry node → branches on wealth (< 0.2 vs ≥ 0.2)
- [ ] poor_lunch node: 2 lines about expensive food
- [ ] normal_lunch node: 2 lines about casual eating
- [ ] Verify JSON syntax

### Step 5: Add conversation_script to Storylets
- [ ] Find lunch_break storylet in `models/tale/worker.json`
- [ ] Add `"conversation_script": "tale.lunch_break"`
- [ ] Find 2-3 other storylets with meaningful tags
- [ ] Add `"conversation_script"` to 1-2 of them (optional, to test precedence)
- [ ] Leave most without explicit script (test tag fallback)

### Step 6: Test & Validate
- [ ] `dotnet build Karawan.sln -c Release` (should compile)
- [ ] `./run_tests.sh phaseC2` (6 C2 tests should pass)
- [ ] `./run_tests.sh phaseC1` (8 C1 tests should still pass)
- [ ] `./run_tests.sh smoke` (smoke tests should still pass)
- [ ] `./run_tests.sh standard` (all 171 regression tests should still pass)

### Step 7: Documentation
- [ ] Update CLAUDE.md if needed (Phase C status)
- [ ] Update docs/TESTING.md (Phase C test count)
- [ ] Create commit with clear message

---

## Key Design Decisions

### 1. Precedence Order (5-level fallback)
- **Level 1 (Explicit)**: Gives storylets full control over dialogue
- **Level 2 (By ID)**: Auto-discovery for story-specific scripts (future)
- **Level 3 (By Tag)**: Semantic grouping (routine, eating, rest, economic)
- **Level 4 (By Role)**: Fallback to role defaults
- **Level 5 (Generic)**: Always has a catch-all

### 2. Tag-Based Scripts
- Generic enough to work for any storylet with that tag
- Don't branch heavily in C2 (minimal logic, mostly just lines)
- C3 will add mood/tone branches later

### 3. Storylet Updates
- **Minimal touching**: Only add `conversation_script` to a few representative storylets
- **Test both paths**: Some with explicit script, some relying on tag/role fallback
- Most existing storylets unchanged (they'll use tag or role fallback)

### 4. JSON Parsing
- Case-insensitive: `"conversation_script"`, `"ConversationScript"` both valid
- Optional field: Null/missing is fine, just means use fallback
- No validation needed: Invalid script names handled gracefully by fallback

---

## Testing Strategy

### Unit Tests (via C2 test scripts)

**C2-01: Storylet Script Override**
- Storylet with explicit `conversation_script` field
- Expect that script used, not tag/role fallback

**C2-02 to C2-04: Tag Fallback**
- Storylet with tags but NO explicit script
- Expect tag-based script used (routine, eating, economic)

**C2-05: Precedence**
- Storylet with both `conversation_script` AND tags
- Expect explicit script takes precedence

**C2-06: Lunch Break Integration**
- Worker at lunch_break storylet
- Wealth-gated dialogue (poor vs normal)

### Regression Tests
- All 171 existing tests should still pass
- No changes to test framework, just new scripts loaded

---

## Risk Factors & Mitigation

| Risk | Impact | Mitigation |
|------|--------|-----------|
| **JSON parsing fails** | Tests fail, script fallback broken | Verify JSON syntax before testing; add logging |
| **Precedence wrong** | Wrong script played | Test each level independently |
| **Tags don't exist** | Tag script never selected | Verify tags on storylets before adding scripts |
| **StoryletDefinition parse issue** | Null reference, crashes | Ensure parser handles null gracefully |

---

## Blockers & Dependencies

✅ **Phase C1 must be complete and building**
- TaleNarrationBindings.ResolveScript() exists
- TaleConversationBehavior works
- Conversation scripts can be loaded via __include__

---

## Rollback Plan

If critical issues encountered:
1. Revert `StoryletDefinition.cs` changes
2. Remove `conversation_script` from storylets
3. Remove Level 1 check from TaleNarrationBindings.ResolveScript()
4. Verify C1 tests still pass
5. Return to working Phase C1 state

---

## Future Extensions (C3-C4)

**Phase C3** will:
- Add mood/tone branches to tag scripts
- Use `func.npcMood()`, `func.npcWealthLabel()` in dialogue
- Expand wealth-gated branches in lunch_break and others

**Phase C4** will:
- Add trust-gated branches to role scripts
- Expand authority script with first-meeting/trusted variants
- Add memory fact handling in scripts

---

## Questions Before Implementation

1. **Storylet Selection**: Should we update specific storylets (lunch_break, beg, sleep) or all of them?
   - **Recommendation**: Just lunch_break gets explicit `conversation_script`; others use tag/role fallback

2. **Tag Coverage**: Do the 4 tags (routine, eating, rest, economic) cover most storylets?
   - **Check**: Run `grep -h '"tags"' models/tale/*.json` to see what tags exist

3. **JSON Case Sensitivity**: Should parser be strict (conversation_script) or lenient (case-insensitive)?
   - **Recommendation**: Case-insensitive to match rest of codebase (see TaleModule.cs)

4. **Wealth Threshold for Lunch**: Is < 0.2 the right cutoff for "can't afford lunch"?
   - **Recommendation**: Yes, matches desperation threshold used elsewhere in Phase 1

---

## Estimated Timeline

- **Reading/Understanding**: 10 min
- **StoryletDefinition changes**: 10 min
- **TaleNarrationBindings update**: 10 min
- **Create 5 JSON scripts**: 15 min
- **Update storylets**: 10 min
- **Build & test**: 15 min
- **Documentation**: 5 min

**Total: 75 minutes (1.25 hours)**

---

## Success Looks Like

✅ All 6 C2 tests pass
✅ All 8 C1 tests still pass
✅ All 171 regression tests still pass
✅ Lunch break worker talks about food/eating
✅ Generic workers talk about routine
✅ Explicit `conversation_script` overrides tag/role fallback
✅ Clean build, no warnings in new code
