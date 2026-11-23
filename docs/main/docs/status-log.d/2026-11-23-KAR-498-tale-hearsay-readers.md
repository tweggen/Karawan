### 2026-11-23 — KAR-498 (TALE S2-c): what reads hearsay

- **Greetings:**
  - `InjectNpcProps` publishes `npc.heard_player`, `npc.heard_weight` (faded to the game time) and `npc.heard_<fact>` for every NPC, 0 without hearsay. `ClearNpcProps` resets `npc.heard_*` to 0; its 0.5 default would have given the next NPC a false "helped out".
  - `tale.generic.json` and `tale.tag.routine.json` greet an NPC who never met the player from firm hearsay (weight ≥ 0.2), after the group greeting:
    - "I've heard about you. You hurt one of ours." (≤ −0.2 with `hurt_someone`);
    - "I've heard things about you." (≤ −0.2);
    - "You're the one who helped out, aren't you?" (≥ 0.15).
- **First-contact prejudice (owner: ±0.2):**
  - `PlayerTrust.StartFor(npc, gameNow)` = 0.15 + 0.2 × regard × current weight, clamped.
  - `PlayerTrust.Apply` creates an entry from it, and `PlayerInteractions._applyTrust` uses it for `before` and the `(1 − trust)` flattening. `gameNow` is threaded through `ApplyPlayerTrust` as an optional parameter; without one there is no prejudice.
  - `PlayerTrust.Get` is unchanged.
- **"Recognised" counts hearing the name (owner):**
  - An NPC without first-hand trust whose hearsay has weight and |regard| ≥ 0.2 counts towards both of the standing level's counts.
  - `GetPlayerStanding(cluster, gameNow)` caches per game hour, because hearsay changes without a trust write and fades with time.
- **Instruments:**
  - `HearsayMetrics.Measure(npcs, gameNow, groupId)`: holders, in and outside a group, by generation, |regard| quartiles, first-hand count. It is a calculator of its own rather than `SocialMetrics` fields, because it needs the clock.
  - The TALE panel's header shows the holders per generation; the selected NPC shows its hearsay.
- **Docs:** REFERENCE (props), ARCHITECTURE (hearsay), CONCEPT (status table).
- **Tests:** `HearsayReadersTests` (8):
  - prejudice incl. the help's `before`/flattening, its fading and clamping;
  - recognised from hearsay and fading out;
  - metrics; panel snapshot;
  - the conversations' branch order and conditions;
  - the props, pinned by source.

  xUnit 2327 pass; the 26 failures are the known KAR-461 drift. TALE 133/133; TestRunner entity tests pass.
