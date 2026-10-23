# TALE S1 — The Player in the Social Graph (stage plan)

> **Status:** proposed; review GREEN 2026-10-18 (Fable, two rounds) · **Written:** 2026-10-18 · **Ticket:** KAR-438 (stage S1)
> **Roadmap:** [TALE-EMERGENT-STORIES.md](TALE-EMERGENT-STORIES.md) §S1, §8 Q2, §9 · **Needs:** [TALE-S0-GROUNDWORK.md](TALE-S0-GROUNDWORK.md)

S1 makes the player a participant in the society: a defined trust baseline,
conversation choices and world actions (hitting, running over, paying,
giving a lift) that change NPCs' trust, witnesses who notice, group
standing that makes faction members treat the player by the group's
opinion, and the player's standing level per cluster.

S1 assumes S0 is done. It builds on W2 (per-NPC randomness, thread-safe
`TaleManager`), W3 (`NpcSchedule.IsKnownToPlayer`,
`TaleManager.MarkPlayerContact`; the player's NPCs are never forgotten),
W6 (social metrics, inter-group matrix), W7 (overlay) and W8 (panel).
Line references are from `origin/master` at `ee10f446`.

## 1. What S1 found in the code

1. **Only two places touch `Trust[-1]`, both in nogame:** the write at
   conversation end (`TaleModule.cs:766-767`, `GetValueOrDefault(-1, 0.5f)
   + 0.02`, upper clamp only) and the read for dialogue
   (`TaleNarrationBindings.cs:82-86`: `npc.met_player`, `npc.trust_player`
   with default 0.5). Serialised in the deviation record
   (`TaleModule.cs:641-646,726-733`) and snapshots
   (`ClusterSocialState.cs:86-123,152-160`). DES, the bake,
   `CommunityDetector`, `RuntimeSocialEvolver` and `GroupActions` never use
   key −1. `RelationshipTierRegistry` is never used for the player.
2. **An unknown NPC reads as 0.5, which is "friend"** in
   `nogame.relationship-tiers.json` (0.4–0.7).
3. **The role scripts' trust gates are rarely reached.**
   `ResolveScript` (`TaleNarrationBindings.cs:208-259`) tries the
   storylet's tags before its role. Of the 96 shipped storylets (the 14
   role files declared in `models/nogame.resources.json:95-108`; the files
   under `models/tale/interactions/` don't load), 48 have `routine` as
   first tag (so the unconditional `tale.tag.routine.json` wins), 6 have
   other tags (3 of them without a tag script), and 42 are untagged — 32 of
   those are claim/interrupt storylets that run only in DES until S2b. So
   about 13 plain storylets (e.g. `order_food`) reach a role script. The gates in `tale.role.merchant.json:9`
   (`> 0.55`) and `tale.role.authority.json:10-11` (`> 0.65`) are
   therefore rarely seen. A greeting that must be seen in play
   goes into `tale.tag.routine.json` (and `tale.generic.json`).
4. **Hitting your conversation partner raises trust, and the dialogue
   stays open.** `_cancelIfConversing` (`TaleConversationBehavior.cs:130-145`)
   acts only when `!MayConverse()`, which is never the case during a
   conversation (`NarrationManager.cs:506`), so it never cancels. What
   happens instead: the hit makes `TaleEntityStrategy._onHitEvent` switch to
   flee, `StayAtStrategyPart.OnExit` removes the behaviour
   (`StayAtStrategyPart.cs:94-103`), and `TaleConversationBehavior.OnDetach`
   disposes the conversation timeout without ending the conversation
   (`:73-77,152-157`). The dialogue stays open while the NPC runs; when it
   ends later, `_onScriptEnded` (`TaleModule.cs:758-777`) adds the +0.02.
   `ScriptEndedEvent` has no "cancelled" flag (`NarrationEvents.cs:58-69`).
5. **Collision events already arrive on the logical thread.** The physics
   contact callback queues behaviour dispatch to the logical thread
   (`physics/API.cs:62-90`; `QueueMainThreadAction` queues onto the
   logical-thread scheduler, `Engine.cs:467-470`). A pair in contact the
   previous frame is suppressed (`API.cs:143-155`), but a punch or a
   bouncing body can still produce several contact episodes; `CitizenCollisionRouter` pushes to the event
   queue, drained on the logical thread (`Engine.cs:620-629`). The router
   drops the layer bits that say who did it: `PlayerMelee 0x4`,
   `PlayerBullet 0x8` (together `PlayerWeapon`), `PlayerVehicle 0x2`,
   `NpcVehicle 0x20` (`CollisionProperties.cs:25-58`).
   Today every weapon hit on an NPC comes from the player (no NPC melee).
6. **Narration handlers may run after an `await`** (`NarrationManager.cs:140-180`),
   so they must hop to the logical thread before touching schedules.
   Side effects belong in event handlers (`RegisterEventHandler`), not in
   `func.*` functions, which conditions may evaluate repeatedly.
7. **New interaction ids are safe from NPC–NPC use** if their
   `conditionClassName` is null: `InteractionTypeRegistry` never selects
   them automatically (`InteractionTypeRegistry.cs:39`), as with
   `report_crime`, `blackmail`, `arrest`. `GetTrustDelta(id)` (`:57-61`)
   exists. The bake does not read `nogame.interactions.json`.
8. **`GameState.Cash` has no source and no reader.** The taxi passenger is
   a cube marker, not a TALE NPC (`TaxiNpcSpawnerModule.cs:48-51`).
9. **`RuntimeGroup` has no leader or rank** (`ClusterSocialState.cs:14-38`);
   a member's standing in its group must come from mutual trust.
10. **`ClearNpcProps` resets unknown numeric keys to 0.5**
    (`TaleNarrationBindings.cs:152-198`): a new `npc.group_standing` would
    leak 0.5 into the next conversation unless special-cased.
11. **Side finding:** `FleeStrategy` subscribes to `HitEventPath`
    (`FleeStrategy.cs:89`) but unsubscribes from `CrashEventPath` (`:79`),
    a leaked subscription per flee (confirmed in review; filed as
    KAR-453).

## 2. Work packages and order

| W | Package | Ticket | Needs |
|---|---|---|---|
| S1-a | Player trust baseline: one writer, first contact | KAR-454 | S0 W3 |
| S1-b | Player interaction model (engine) | KAR-455 | S1-a |
| S1-c | Conversations: default chat, `tale.interact`, `tale.pay` | KAR-456 | S1-b |
| S1-d | World actions and witnesses | KAR-457 | S1-b |
| S1-e | Group standing and the greeting | KAR-458 | S1-a, S1-b (S1-c/d to show it in play) |
| S1-f | Standing level per cluster | KAR-460 | S1-e, S0 W7/W8 |
| S1-g | Taxi fare (passenger binding optional) | KAR-459 | S1-b |

S1-c, S1-d, S1-e and S1-g can run in parallel after S1-b.

## 3. S1-a — Player trust baseline

> **Status:** implemented (KAR-454), 2026-10-20. Deviation: the two role-script gates were
> moved now (merchant `> 0.205`, authority `> 0.305`) so they open after the same 3 and 8
> conversations as before; S1-c retunes them again for the chat rule. A test pins the counts.

- **No entry means "doesn't know the player".** First contact starts at
  `PlayerTrust.FirstContact = 0.15` (the stranger/acquaintance boundary),
  then applies the interaction's change. Values are clamped to [0, 1] on
  both sides.
- **One writer.** New `JoyceCode/engine/tale/PlayerStanding.cs` with
  `PlayerTrust` (`Knows`, `Get` — entry or first-contact value) and
  `TaleManager.ApplyPlayerTrust(schedule, delta)`: ensures the entry,
  adds, clamps, calls `MarkPlayerContact` (W3), bumps a
  `PlayerTrustVersion` (S1-f's dirty flag), returns the new value. Every
  later writer (S1-b…S1-d, S2, S3) goes through it.
- **Narration** reads `PlayerTrust.Get` instead of the 0.5 default
  (`TaleNarrationBindings.cs:84`); `npc.met_player` keeps meaning "has an
  entry" (S2 will separate "met" from "heard of").
- **Old saves: no migration** (owner, 2026-10-18). Development saves keep
  their old values (0.5 + 0.02·n, i.e. "friend"); new games use the new
  scale. No marker, no load-time shift, no `DbVersion` bump.

*Done when:* no code reads `Trust[-1]` with a 0.5 default; a first
conversation leaves `Trust[-1]` at the first-contact value plus one chat;
tests for baseline, clamping and `MarkPlayerContact` pass.

## 4. S1-b — Player interaction model (engine)

> **Status:** implemented (KAR-455), 2026-10-23. Deviations: `ApplyWitnessed` also takes the
> witness's distance (the strength formula needs it); the cooldown counts from the last full
> application; `pay`/`bribe` use repeat scale 0 (trust once a game day) while the cash always
> reaches the NPC's wealth; `wealthPerCash` is 0.002 until S1-g knows the fares; a known type
> without a player block applies its NPC–NPC `trustDelta`. Targets: `chat`/`help` facts are
> `helped_me` (help only), `hit_me`, `ran_me_over`, `paid_me`, `bribed_me`, `drove_me`;
> witness facts `saw_assault`, `saw_run_over`.

**Data:** an optional `player` block on interaction types in
`models/nogame.interactions.json`, applied only in the NPC→player
direction (the player has no trust store). Fields: `trustDelta`
(overrides the NPC–NPC value for the player), `targetProperties` (e.g.
fear, anger, health, morality), `targetFact` (`player_fact.<name>`),
`cooldownMinutes`, `repeatScale`, `wealthPerCash`, `witness` {`factor`,
`radius`, `maxCount`, `fact`, `properties`, `lineOfSight`}. It
deserialises without loader changes (`TaleModule.cs:421-440`).

New player-only ids, with `conditionClassName: null`:

| id | trust | properties | notes |
|---|---|---|---|
| `assault` | −0.25 | fear +0.25, anger +0.15 | cooldown 3 game min, repeat scale 0; witnesses 25 m, factor 0.35 |
| `run_over` | −0.45 | fear +0.35, anger +0.25, health −0.3 | cooldown 3 game min, repeat scale 0; witnesses 40 m, factor 0.4 |
| `pay` | +0.06 | wealth by cash | cooldown a game day |
| `bribe` | +0.04 | wealth by cash, morality −0.02 | cooldown a game day |
| `drive` | +0.08 | — | taxi passenger (S1-g) |

Existing `chat` gets a player block of +0.02 (today's value), cooldown 60
game minutes, repeat scale 0.25; `help` gets +0.4 with a 12-game-hour
cooldown and repeat scale 0.3: one help is meant to count a lot (owner,
2026-10-18) — from first contact it lifts the helped NPC to ≈ 0.49 and
that NPC's group standing to ≈ +0.2 (S1-e). A
type without `cooldownMinutes` has no rate limit; `repeatScale` defaults
to 1. All numbers are first guesses to tune; tests pin the combinations
the done-when criteria depend on.

Chushi and TestRunner build their own hard-coded registries
(`Chushi/ConsoleMain.cs:582-596`, `TestRunner/TestRunnerMain.cs:62-77`);
the player blocks are absent there, which is fine (they apply only in
the live game). New engine files (`PlayerStanding.cs`,
`PlayerInteractions.cs`, `PlayerInteractionSpec.cs`, `WitnessSelector.cs`,
`PlayerActionClassifier.cs`) go into `JoyceCode.projitems` by hand;
traces use `Trace(_dc, …)` with a `Dc` category.

**Code:** `PlayerInteractionSpec` (on `InteractionTypeDefinition`),
`PlayerInteractions.Apply(npcId, id, gameNow, cashPaid)` and
`ApplyWitnessed(witnessId, victimId, id, gameNow)`, and
`TaleManager.ApplyPlayerInteraction(...)`. `Apply`:
1. looks up the type and schedule (unknown id → `Warning`, no change);
2. applies cooldown and repeats: inside the cooldown the change is scaled
   by `repeatScale^n` (in memory, not saved — anti-farming, not story
   state);
3. computes the change; a positive change is flattened by `(1 − trust)`;
4. writes trust through `ApplyPlayerTrust` (S1-a);
5. adds the target properties (scaled, clamped) and wealth from cash;
6. sets `player_fact.<targetFact> = 1` (0/1 convention of
   `TaleNarrationBindings.cs:126-137`; S2 may write values below 1 for
   "heard");
7. traces the result.

No ECS, no clock, no RNG: positions and times are parameters, so the
result is deterministic and testable.

**Pure helpers:** `WitnessSelector.Select(at, candidates, excludeId,
radius, maxCount, lineOfSight)` (squared-distance filter, sort by distance
then id, line of sight only on the sorted prefix) and
`PlayerActionClassifier.Classify(otherLayerMask)` → none / assault /
run-over.

*Done when:* xUnit tests pass for baseline-then-delta, assault effects,
cooldown scaling and reset, flattening near 1, pay and wealth, unknown id,
player-only ids never selected by `EvaluateConditions`, JSON with and
without `player` blocks, witness selection (radius, cap, order, exclusion,
line-of-sight stub, permutation-invariant), witnesses weaker than victims
and groupmates stronger, and the classifier; NPC–NPC evolver tests are
unchanged.

## 5. S1-c — Conversations: default chat, `tale.interact`, `tale.pay`

- **Conversation session** in `TaleNarrationBindings`: npc id, whether the
  script applied an explicit interaction, whether it was cancelled by
  violence.
- **Violence ends the conversation** (finding 4). `TaleEntityStrategy._onHitEvent`
  and `_onCrashEvent` (logical thread), **if the session's npc id is this
  NPC** (hitting a bystander must not end an unrelated conversation), mark
  the session as cancelled by violence and cancel the conversation (`ConversationTimeout.CancelNow` /
  `Narration.CancelConversation`) **before** switching to flee or recover;
  `_cancelIfConversing` tests "this NPC's conversation is running"
  (`_conversationTimeout != null`) instead of `!MayConverse()`. The
  dialogue then closes on a hit, which it does not today. The flag lives
  in the session, not in the timeout (the timeout is disposed before the
  `ScriptEndedEvent` drains).
- **Match the script:** `_onScriptEnded` applies the default chat only if
  `ScriptEndedEvent.ScriptName` is the session's script (as
  `ConversationTimeout.cs:69-74` does); a quest narration started during a
  conversation replaces the runner without ending it.
- **Events, not functions:** `tale.interact` (`id`) and `tale.pay`
  (`amount`, `id` = pay or bribe), registered next to `tale.npc.remember`
  (`TaleModule.cs:375-389`). Handlers capture the npc id synchronously and
  hop with `QueueMainThreadAction` before touching schedules or
  `GameState`; they await a `TaskCompletionSource` completed by the queued
  action (nothing blocks the logical thread), and the queued action
  refreshes `npc.trust_player` and `player.cash` in Props, so later nodes
  of the same script and `_onScriptEnded` see the new values. `tale.pay` checks and subtracts `GameState.Cash`
  (reached through `AutoSave`, whose getter throws before autosave starts
  — guard it).
- **Default chat:** `_onScriptEnded` (`TaleModule.cs:758-777`) applies
  nothing if the conversation was cancelled by violence, applies `chat`
  if the script applied no explicit interaction, otherwise nothing more.
- **Props:** `player.cash` for choice conditions; `tale.npc.remember` uses
  `MarkPlayerContact`.
- **Scripts:** the merchant's `desperate` node offers pay (if
  `props.player.cash >= 20`), help and leave; a `hostile` branch for
  `player_fact.assaulted_me` in the merchant and generic scripts; a bribe
  choice in the authority script. Because of finding 3, anything that must
  be seen in play also goes into `tale.tag.routine.json`.

- **Retune the two role-script gates** so they still fire after about the
  same number of conversations as today (merchant "returning": 4th,
  authority "trusted": ~9th) under the new chat rule; a test pins the
  count. Thresholds sit off the 0.02 grid.

*Done when:* a natural end applies `chat` through the registry
(rate-limited), and W1's +0.02 mutation check now fails when the chat
interaction is removed; hitting the partner mid-conversation closes the
dialogue, applies no chat and applies an assault; the pay choice appears only with enough cash, subtracts it,
raises trust and wealth; a quest narration ending during a conversation
applies nothing to the NPC.

## 6. S1-d — World actions and witnesses

- **Attribution in the router.** `CitizenCollisionRouter.Dispatch` puts
  `"player"` into the event's `Code` when the other body carries a player
  weapon (`PlayerMelee` or `PlayerBullet`) or player vehicle bit
  (`Event(type, code)`); no Hit/Crash subscriber reads `Code` today.
  Run-over additionally requires the player ship to have been moving
  (≈ 4 m/s) **before** the contact: the callback runs after the physics
  step and NPC bodies are kinematic, so the post-solve velocity may already
  be reduced. `HoverModule` publishes its last-frame speed (it already
  reads `_prefShip.Velocity.Linear`, `HoverModule.cs:128`) and the router
  uses that.
- **Hooks:** `TaleEntityStrategy._onHitEvent` and `_onCrashEvent`
  (`TaleEntityStrategy.cs:62-77`) keep their flee/recover logic and, for
  `Code == "player"`, report `assault` / `run_over` — before
  `_onHitEvent`'s early return while in recover, so a punch during
  recovery still counts — through a nogame
  `PlayerActionReporter` (logical thread, finding 5). Several contact
  episodes per punch or crash are possible; the 3-minute cooldown with
  repeat scale 0 turns them into one.
- **Witnesses:** materialised TALE NPC entities (`TaleNpcId` +
  `Transform3ToWorld`) within the type's radius, nearest first, at most 8,
  excluding the victim; line of sight by one `RayCastSync` per candidate
  from eye height to the event (it takes the simulation lock itself — no
  extra lock; terrain has no collider, so hills don't block). Only hits on
  `StaticEnvironment` / `MovableEnvironment` bodies count as occluders;
  `DefaultRayHitHandler` (`DefaultRayHitHandler.cs:24-52`) also reports
  NPCs and the player, which must be ignored. Strength =
  delta × factor × (1 − 0.5·d/R) × 1.5 for the victim's groupmates.
  Witnesses get `player_fact.saw_<…>` and become known to the player (W3
  keeps them; see the save-size risk).

*Done when:* an xUnit pipeline test (classifier → apply → witness
selection → witnessed apply, no ECS) shows the victim's and a witness's
trust dropping; the nogame half (`HitEventPath` with `Code = "player"` →
`TaleEntityStrategy` → reporter) is covered by a TestRunner script if
TestRunner materialises TALE NPCs with strategies (it references nogame),
otherwise by the in-game check below — the roadmap's "a test raises
`HitEventPath`" is then met by the engine pipeline test plus that check; in the game, a punch lowers the victim's trust once and a
nearby witness's less, an NPC behind a building is unaffected, ramming at
speed counts as run-over while touching a parked ship doesn't, NPC cars
produce nothing, and save/load keeps it all — visible in the S0 debug
views.

## 7. S1-e — Group standing and the greeting

- **Member regard:** map `Trust[-1]` to [−1, 1] around the first-contact
  value B0: `(t − B0)/B0` below, `(t − B0)/(1 − B0)` above. Harm weighs
  more than help per unit of trust near the baseline; with `help` at +0.4
  one help and one hit are of similar size in group standing (owner,
  2026-10-18: one help ≈ +0.2).
- **Weight (standing in the group):** how much the other members trust
  member i, `w_i = 0.25 + mean_j Trust_j[i]`, normalised to mean 1 over
  the roster.
- **Group standing:** over members who know the player only,
  `gs = Σ ŵ_i·r(t_i) / (Σ ŵ_i + K)`, K = 1 (one neutral member's worth of
  prior); 0 when nobody knows the player. Size doesn't dilute it. With
  S1-b's numbers, one help from first contact gives t = 0.15 + 0.4·0.85 =
  0.49, r = 0.4, gs = +0.2 (equal weights); one hit gives t = 0 (clamped),
  r = −1, gs = −0.5. A single chat moves gs by only ≈ 0.01. Computed on demand
  (`PlayerStandingCalculator.ForGroup`, pure; `TaleManager` wrapper on the
  logical thread), no cache.
- **Dialogue props:** `npc.group_standing` and `npc.group_known` (members
  who know the player, excluding the speaker), both reset to 0 in
  `ClearNpcProps` (finding 10).
- **Greeting:** `tale.tag.routine.json` and `tale.generic.json` get an
  entry branch for NPCs who don't know the player personally:
  `group_standing >= 0.05` → a warm line naming the group,
  `<= -0.15` → a cold one, otherwise the existing lines.
- **Test seam:** `NarrationConditionEvaluator` gets an optional prop
  resolver (default `Props.Get`), because `Props` can't be built in xUnit.
  The test evaluates the entry node's `goto` conditions through that seam
  only (`NarrationInterpolator` also reads `Props.Get`,
  `NarrationInterpolator.cs:113`, so running a whole `NarrationRunner`
  would need a second seam).

*Done when:* tests pass for the formula (no known members, one helped
member, central vs peripheral, size independence, harm scaling, missing
schedules, NPC–NPC trust ignored) and for the roadmap criterion: one
help (S1-b's `help`, from first contact) on member A raises the standing B
sees above the warm threshold, and the routine script, evaluated
with B's props, routes B to the warm greeting (cold after a hit); after a
conversation `npc.group_standing` reads 0; in the game another member of
the helped NPC's group says a warm line, and W8 shows the group's
standing.

## 8. S1-f — Standing level per cluster

- **Inputs per cluster:** `known(x, y)` = NPCs with `Trust[-1] ≥ x`
  (liked) **or** `≤ y` (feared) — notoriety counts too (owner,
  2026-10-18);
  `groups(y, k)` = groups with `|gs| ≥ y` and at least k members who know
  the player; S3's favour record and S4's rival groups (0 in S1).
- **First-guess thresholds** (config, tunable), with hysteresis:

  | Level | Enter | Leave |
  |---|---|---|
  | anonymous | default | — |
  | recognised | `known(0.30, 0.05) ≥ 3` | `known(0.26, 0.08) < 2` |
  | connected | `groups(0.15, 2) ≥ 1` (positive or negative) | `groups(0.10, 2) == 0` |
  | entangled | favours in ≥ 2 directions and standing with ≥ 2 rival groups | either below 1 |

  Entangled is unreachable in S1 by construction; a test asserts it.
  With K = 1, n members at regard r give gs = n·r/(n+1): helping two
  members of a group once each gives ≈ +0.27 (positive "connected"), and
  one hit with a witness from the same group reaches negative
  "connected"; tests use those cases. Helping or hitting three NPCs makes
  the player "recognised". The
  level is the highest entered and not left; with no state (after a load,
  first visit) only enter thresholds apply. State is transient, so
  nothing new is saved (roadmap §9.1).
- **Where:** `TaleManager.GetPlayerStanding(clusterIndex)`, cached and
  recomputed when `PlayerTrustVersion` or a group-table version changes;
  refreshed every social tick for the player's cluster, and for the
  conversing NPC's cluster when dialogue starts.
- **The player's cluster:** the last cluster the player was inside
  (`ClusterList.GetClusterAt` per social tick; null between clusters keeps
  the last), also listening to `CLUSTER_CHANGED` when `ClusterWatch` runs.
  Dialogue always uses the NPC's own cluster.
- **Exposure:** props `player.standing` (name) and `player.standing_rank`
  (0–3), set only on change (`Props._set` copies and emits on every set);
  W7 overlay and W8 panel show the level and its inputs.

*Done when:* tests pass for the thresholds, hysteresis, negative standing
counting as connected, per-cluster independence, no-state evaluation and
unreachable entangled; the props change only on transitions; in the game
helping three NPCs (or hitting three) flips the panel to "recognised"
within one social tick,
and another cluster shows "anonymous".

## 9. S1-g — Taxi fare (passenger binding optional)

- **Fare:** on taxi success (`TaxiQuestStrategy.cs:29-33`) add
  `GameState.Cash += 30 + distance/10` — the money source that pay and
  bribe need. Cash is the currency (owner, 2026-10-18).
- **Passenger (optional):** bind the passenger to the nearest materialised
  TALE NPC within 30 m of the pickup marker (ties by id), store
  it in `TaxiQuestData` (`PassengerNpcId` plus a `HasPassengerNpc` flag,
  since id 0 is valid), apply `drive` on success. The passenger stays a
  cube; a TALE NPC hailing a taxi belongs to S3/S5.

*Done when:* a taxi run raises cash; with the passenger binding, that
NPC's trust rises; `TaxiQuestData` from old saves still loads.

## 10. Owner decisions (2026-10-18)

1. **Old saves:** no migration; development saves keep their values, new
   games use the new scale (S1-a).
2. **Help vs harm:** one help should count a lot — about +0.2 group
   standing; `help` is +0.4 trust (S1-b, S1-e).
3. **Notoriety:** being feared counts towards "recognised" (S1-f).
4. **Currency:** `GameState.Cash`, earned by taxi fares (S1-g).

## 11. Risks

- **Save size:** every witness becomes a kept, saved schedule (W3);
  a violent session could reach hundreds of NPCs (~1 MB). The witness cap
  helps; S2's "heard of" vs "met" split may make witnesses a lighter
  class.
- **Regroup churn:** re-detection every 2 game hours can split or merge
  groups, moving group standing and the level without any player action.
  W6's churn metric shows how often.
- **Script resolution:** the routine tag shadows role scripts (finding 3).
  Changing the resolution order is a content decision outside S1.
- **Thresholds are guesses,** tuned against S0's baselines and play
  sessions.
- **Threading:** everything that reads schedules runs on the logical
  thread (social frame, `InjectNpcProps`, collision handlers, the W8
  snapshot builder); narration handlers hop first.

## 12. Docs to update along the way

ARCHITECTURE §6.3 (who sets `HasPlayerDeviation`) and §7 (player
interactions, standing), REFERENCE (`tale.interact`, `tale.pay`, the
player block, props), the conversation docs, and a STATUS-LOG entry per
package.
