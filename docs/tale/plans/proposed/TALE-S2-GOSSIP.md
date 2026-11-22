# TALE S2 — Consequences travel (gossip)

> Stage S2 of [TALE-EMERGENT-STORIES.md](TALE-EMERGENT-STORIES.md) (KAR-439).
> Depends on S1 (done 2026-11-22) and S0 (done); uses S2b's shared rules (`TaleSocialRules`,
> KAR-486..490).
> **Status:** proposed; reviewed GREEN (Fable, 2026-11-22, three passes, §9) · **Written:** 2026-11-22

What the player does to one NPC stays with that NPC and the witnesses who saw it (S1). In
a living society it should travel: the syndicate hears that someone beat up one of its
members, a merchant's neighbours hear that a stranger helped her. This stage lets opinions
of the player, and the facts behind them, spread along NPC encounters. It deliberately
keeps *having heard of* the player apart from *having met* the player.

Line numbers are on master after PR #203.

## 1. What the code does today

### 1.1 The player's trust, and what an entry means

- `Trust[-1]` is the NPC's first-hand trust in the player (S1-a). Its only writer is
  `TaleManager.ApplyPlayerTrust` (`TaleManager.cs:1027`). That writer also calls
  `MarkPlayerContact` (`:1004`), which sets `HasPlayerDeviation`: the NPC is saved and put
  into its cluster's skip mask. `NpcSchedule.IsKnownToPlayer` (`NpcSchedule.cs:70`) is
  true for any `Trust[-1]` or any `player_fact.*` property, and such an NPC is never
  removed on depopulation.
- **The entry itself is "met".** `TaleNarrationBindings.InjectNpcProps` sets
  `npc.met_player` from `PlayerTrust.Knows(schedule)` (`TaleNarrationBindings.cs:175`;
  `RefreshPlayerProps` does the same at `:106`). The
  conversations branch on it: the group greeting (`tale.generic.json:9-10`,
  `tale.tag.routine.json:9-10`) fires only for `met_player == 'false'`, and the merchant's
  `returning` line for `met_player == 'true'`.
- `PlayerStandingCalculator.Regard` (`PlayerStanding.cs:69`) maps trust to a regard in
  [-1, 1] around `FirstContact` (0.15). Group standing (`ForGroup`, `:86`) counts only
  members who `Knows` the player, and the standing level (S1-f) counts known NPCs.
- Witnesses already get a first-hand change (`PlayerInteractions.ApplyWitnessed`,
  `PlayerInteractions.cs:96`): a witness *saw* it, so it knows the player. S2 is about
  NPCs who saw nothing.

**Consequence for S2:** gossip cannot simply write `Trust[-1]`. Every NPC reached would
greet the player as someone it has met, lose the group greeting, be written into the save,
and enter the skip mask. A rumour reaching a few hundred NPCs would make them all
"deviated".

### 1.2 Where NPC encounters happen

| Driver | Where | Runs |
|---|---|---|
| DES (bake, tests) | `DesSimulation.ProcessEncounter` (`DesSimulation.cs:337`) | at node arrivals; trust by `RelationshipTracker`; then `TaleSocialRules.ClaimRequests` and `ApplyEncounterConditionals` (`:347`, `:400`) |
| Live, social tick | `RuntimeSocialEvolver._tryEncounter` (`RuntimeSocialEvolver.cs:194`) | every 10 game minutes per cluster, round robin; trust by `InteractionTypeRegistry`; then `onEncounter` (`:240`) queues the shared rules, drained by `TaleManager.TickLiveSocial` (`TaleManager.cs:1452`) |
| Live, arrival | `TaleManager._runArrival` (`:1519`) | at each live node arrival, the DES's encounter model; claims only (`:1539`) |

### 1.3 What survives depopulation and save

- `ClusterSocialState.SnapshotNpc` (`ClusterSocialState.cs:92`) copies each NPC's whole
  `Trust` dictionary, its group and five social properties into `NpcSocialSnapshot`
  (`:47`); `RestoreNpc` (`:117`) puts them back. It runs at depopulation
  (`TaleManager.cs:481`) and, for populated clusters, at every save (`:811`, called from
  `TaleModule.cs:246`). The snapshots are saved in
  `GameState.TaleSocialState` and overlaid at the next populate.
- Deviated NPCs are saved whole through `NpcScheduleJson` (properties, trust).

So a per-NPC record that the snapshot carries survives depopulation and save/load without
marking the NPC as touched by the player.

## 2. The design

### 2.1 A heard opinion, kept apart from trust

New on `NpcSchedule`: **`PlayerHearsay`**, a small record:
- `Regard` in [-1, 1]: what the NPC has heard of the player, on the same scale as
  `PlayerStandingCalculator.Regard`;
- `Weight` in [0, 1]: how firmly it has heard it (how many and how trusted its sources
  were);
- `Facts`: a small set of heard fact tags (§2.3);
- `UpdatedAt`: game time of the last change, for decay (§2.4).

**Immutable.** A change builds a new record and replaces the reference, only at the two
locked write sites (§2.6). Readers on the logical thread (narration props, the panel)
read whatever reference is current without locks, and never write: decay is computed from
`UpdatedAt` on read and stored only when a write happens anyway (§2.4). `Facts` is an
immutable set for the same reason.

It is **not** `Trust[-1]` and **not** a `player_fact.*` property, so it does not make the
NPC "met", known or deviated. It goes into `NpcSocialSnapshot` (new optional field) and
into `NpcScheduleJson` for NPCs saved anyway. Old saves load without it.

Recommended over two alternatives:
- *Writing `Trust[-1]`* — rejected for the reasons in §1.1.
- *A second key in `Trust` (say -2)* — rejected: hearsay needs a weight, facts and a
  timestamp, not one float. (Readers that iterate `Trust` would cope: `CommunityDetector`
  and `SocialMetrics` skip ids that are not NPCs.)

`RestoreNpc` sets the hearsay from the snapshot, `null` when the snapshot has none (it
clears `Trust` before restoring; the new field gets its own line). `NpcScheduleJson`
carries it for NPCs saved anyway; `FromJson` already marks every loaded NPC as deviated
(`NpcScheduleJson.cs:134`), which is right for those.

### 2.2 Diffusion at an encounter

One pure function, **`PlayerGossip.Exchange(a, b, trustAB, trustBA, time, rng, settings)`**,
called from all three encounter sites of §1.2, with the encounter's time (decay starts
from the weight at that time, §2.4). Both directions in one call, both `told`
values computed from the state **before** the exchange. a's view, as b receives it:

1. **What a has to tell:** `told = Relay × source`, where `source` is a's first-hand regard
   if it knows the player (`Regard(PlayerTrust.Get(a))`), else its hearsay regard.
   `Relay` is 0.5: every telling halves the strength, the first one included, so chains die
   out.
2. **Only strong opinions travel:** nothing is told if |told| < `MinTold` (0.15). Weak
   impressions stay with the NPC that has them.
3. **b listens in proportion to how much it trusts a:** `w = Listen × trustBA`
   (`Listen` 0.5). Then:
   - **confirmation** (b has a record, the same sign, |told| ≤ |Regard|): only
     `Weight ← min(1, Weight + w)`; the regard is kept. Without this rule two NPCs that
     hold the same rumour would retell each other weaker versions and wear it down.
   - **otherwise** b's hearsay moves towards the told regard:
     `Regard ← Regard + w × (told − Regard) / (Weight + w)`, `Weight ← min(1, Weight + w)`.
     A weighted mean of what b has heard: several agreeing sources settle it, and no single
     telling overshoots. An empty record (no hearsay yet) is never a confirmation, since
     |told| ≥ `MinTold` > 0. A telling of the opposite sign may flip b's hearsay when it
     outweighs what b heard before: disagreeing sources compete. §7's "never flips" is
     about a single chain of retellings.
4. **First-hand beats hearsay:** an NPC that knows the player keeps its hearsay but its
   own trust is not changed by gossip. Gossip never writes `Trust[-1]`.

Bounded: |Regard| ≤ 1, Weight ≤ 1, and a told regard is at most half its source's. Worked
example with the defaults: a member who was punched (trust 0 after `assault`'s −0.25 from
0.15, regard −1) tells a friend −0.5. That friend tells its friends −0.25. Their retelling
would be −0.125 < `MinTold`, so the rumour stops after two hops unless more sources add to
it.

**Help travels less far than harm**, and that follows from `Regard`'s shape rather than
from a rule:
- One `help` is +0.4 flattened by `(1 − trust)` (`PlayerInteractions.cs:200-201`): trust
  0.15 → 0.49, regard 0.34 / 0.85 = 0.40.
- Told to a friend at 0.20; that friend would retell 0.10 < `MinTold`, so it stops after
  one hop.
- A repeated help (`repeatScale` 0.3, cooldown 720 min) brings the regard to about 0.47,
  told at 0.235: still one hop.

Harm drops trust to 0 at once (regard −1) and travels two. Whether help should travel as
far is owner question 5 (§6).

### 2.3 Facts

What a heard fact is: a tag on the hearsay, not a `player_fact.*` property. Mapping from
the first-hand facts S1 writes:

| a's first-hand fact | b hears |
|---|---|
| `player_fact.hit_me`, `saw_assault` | `hurt_someone` |
| `player_fact.ran_me_over`, `saw_run_over` | `ran_someone_over` |
| `player_fact.helped_me` | `helped_someone` |
| `player_fact.paid_me` | `pays_people` |
| hearsay tag of a | the same tag, with probability `FactRelay` (0.5) |

A first-hand fact is passed on with probability `FactTell` (0.8). Draws come from the
encounter's own source (§2.5).

### 2.4 Decay

Hearsay fades: `Weight` halves every `HalfLifeDays` (3 game days). Readers compute the
current weight from `UpdatedAt` without storing it; a write (an exchange) starts from the
decayed weight and stores the result. A record whose current weight is below 0.05 counts as
absent and is dropped at its next write. Regard itself does not decay: only how sure the
NPC is.

What that means for the greeting (§3.1, threshold `heard_weight ≥ 0.2`): one telling by a
source the listener trusts at 0.4 gives weight exactly 0.2. That meets the threshold only at
the moment of telling; by the next read it has decayed below it. So the greeting needs a
source trusted above 0.4, or several sources. A single telling by a close friend (trust
0.8, weight 0.4) keeps it for one half-life, about 3 game days; several keep it longer.
That is intended: a friend's word fades unless others repeat it.

### 2.5 Determinism and the bake

- **DES:** NPC–NPC trust lives in `RelationshipTracker`, keyed by the symmetric pair
  (`RelationshipTracker.cs:18,23,43`), not on `NpcSchedule.Trust`. The DES therefore passes
  `_relationships.GetTrust(a, b)` as both `trustAB` and `trustBA`. Gossip uses the
  simulation's `_rng`, but only draws when at least one side has something to tell. The bake
  has no player, so nothing is ever told and **no draw happens: the bake stays
  byte-identical.** Verified by re-baking (`sc-*` byte-identical; the statistics file
  differs only in `generatedAt`) and by a test that counts the draws (§5).
- **Live:** directional trust from `NpcSchedule.Trust`.
  - At the social tick the exchange runs in `TickLiveSocial`'s drain, frames after the
    encounter, so it sees the trust **after** the tick's change
    (`RuntimeSocialEvolver.cs:224-227`). Its source is seeded from what the queued
    encounter carries: the pair key and `time.Ticks` (the tick index is private to the
    evolver and not queued).
  - At the arrival site it uses the per-(NPC, step) source `_runArrival` already has
    (`TaleManager.cs:1525`). Its gossip draws come after `CheckEncounters`' draws
    (`:1532`), so adding gossip does not change which encounters happen.
  - Never the storylet sources.
- **Bake bytes:** the `sc-*` files serialise a `ScenarioNpc` DTO (`ScenarioExporter.cs:38`),
  not `NpcSchedule`, so the new field cannot appear in them either.

### 2.6 Threads and locks

The social-tick encounter already runs the shared rules through `TickLiveSocial`'s queue,
under the cluster lock and `_loSocial` (KAR-488); gossip runs in the same drained call. The
arrival encounter runs under the same two locks (`_runArrival`). Lock order stays: cluster
lock, then `_loSocial`. `PlayerHearsay` is written only there; readers (narration props,
the panel) read it on the logical thread.

## 3. What reads it

1. **Greetings.** `InjectNpcProps` publishes, for **every** NPC, `npc.heard_player`
   (regard, 0 without a record), `npc.heard_weight` (0 without a record) and every
   `npc.heard_<tag>` flag (0 or 1). `ClearNpcProps` resets `npc.heard_*` to 0, as it does
   `npc.player_fact.*`. Its default for unknown numeric props is 0.5
   (`TaleNarrationBindings.cs:291-298`), which would otherwise give the next NPC a false
   "helped out" greeting. The conversations gain, after the group greeting, a branch for
   `met_player == 'false'` with a firm enough hearsay (`heard_weight ≥ 0.2`):
   - `heard_player ≤ −0.2`: "I've heard about you. You hurt one of ours." /
     "People talk. Keep your hands to yourself." (with `heard_hurt_someone`)
   - `heard_player ≥ 0.15`: "You're the one who helped out, aren't you?" A help's first
     hop arrives at 0.20 (§2.2), so this is reachable.
   The group greeting (first-hand standing of the NPC's group) keeps priority when it
   applies; it is the stronger signal.
2. **First contact.** When an NPC with hearsay meets the player the first time, its
   start moves from `FirstContact` by `Prejudice × Regard × Weight(gameNow)`
   (`Prejudice` 0.1). The mechanism:
   - **A new helper, `PlayerTrust.StartFor(npc, gameNow)`:** the NPC's entry if it has
     one, else `FirstContact` shifted by the hearsay's current weight.
   - **`PlayerTrust.Apply(npc, delta, gameNow)`** creates the entry from `StartFor`, so
     `ApplyPlayerTrust` stays the one writer.
   - **`PlayerInteractions._applyTrust`** uses `StartFor` for its `before` and for the
     `(1 − trust)` flattening (`PlayerInteractions.cs:199-201`), so an interaction acts on
     what the NPC actually thinks.
   - **`PlayerTrust.Get`** is unchanged: `FirstContact` without an entry, and so is
     `npc.trust_player`. The hearsay is shown by its own props.
   - **Game time is threaded through:**
     - `ApplyPlayerTrust(schedule, delta, gameNow)` (`TaleManager.cs:1027`) and
       `_applyTrust` (`PlayerInteractions.cs:198`) gain `gameNow`;
     - `PlayerInteractions.Apply`/`ApplyWitnessed` already have it (`:60`, `:96`);
     - the narration's `tale.interact`/`tale.pay` pass the game clock;
     - `PlayerTrustTests` (`:44-69`) are updated.

     Decay needs it, and the alternative (using the stored, undecayed weight) would let
     a week-old rumour colour a first meeting at full strength.

   After that the NPC knows the player and its own experience counts. The hearsay record
   stays (still shown, still retold, first-hand first).
3. **Not read by group standing or the standing level.** Both stay first-hand. Whether
   hearsay should count towards the standing level's "recognised" (notoriety), and with
   it the roadmap's §9.4 ("connected needs group standing and gossip"), is owner question
   2 (§6).
4. **Instruments.**
   - `SocialMetrics` gains the hearsay count and share, split into members of one group
     (a `groupId` parameter: the victim's `GroupId` in S2-d) and the rest, and the
     distribution of |Regard|. `SocialMetricsCalculator` is pure (`SocialMetrics.cs:343-347`),
     so the group id and `gameNow` (for the current weight) are parameters.
   - The panel's per-NPC record (`TaleDebugNpc`, `TaleDebugSnapshotBuilder.cs:185-200`)
     gains the hearsay (regard, current weight, tags, age); the panel shows it and the
     header counts the NPCs with hearsay.

## 4. Work packages

| WP | What | Needs |
|---|---|---|
| **S2-a** | `PlayerHearsay` on `NpcSchedule`, in `NpcSocialSnapshot` and `NpcScheduleJson`, lazy decay; `PlayerGossip.Exchange` with its settings (pure, unit-tested). | — |
| **S2-b** | Wire `Exchange` into the three encounter sites (§1.2); live seeds; DES draws only when there is something to tell. Re-bake: `sc-*` byte-identical. | a |
| **S2-c** | Readers: narration props (and `ClearNpcProps`), the hearsay greetings in the shipped conversations, first-contact prejudice in `PlayerTrust.Apply`, `SocialMetrics` and panel instruments. | a |
| **S2-d** | Measure and tune (§5); the stage's done-when. | b, c |

## 5. Verification

- **Unit (S2-a):** `Exchange` is bounded; a told regard is at most half the source's; a
  weak opinion is not told; a confirmation raises only the weight; first-hand trust is
  never changed; decay halves the weight per half-life, and reading never writes; the same
  inputs and seed give the same result; the worked example of §2.2 holds.
- **DES (S2-b):** seed one NPC's `Trust[-1]` to 0 and `player_fact.hit_me`, run a few days:
  NPCs that never met the player carry a negative hearsay with `hurt_someone`, mostly in
  the victim's group and its friends. Without any player entry the run is identical to
  one without gossip: the same number of `_rng` draws, and the same schedules.
  `_rng` is private (`DesSimulation.cs:38`): `Initialize` gains an optional `Random` so
  the test injects a counting subclass, instead of exposing the field.
- **Live (S2-b):** a `TaleManager` populated from a fixed model: punch one NPC (through
  `ApplyPlayerInteraction`), run the social tick for a game day: an NPC in the victim's
  circle that never met the player gets the hearsay greeting (S2-c) and the panel shows it.
- **Reach (S2-d)**, measured in Ufyo (4095 NPCs) with the shipped content, one punch and
  one help, after one and after three game days: how many NPCs carry hearsay, in and
  outside the victim's group, and the distribution of |Regard|. Tuning target, first guess
  for the owner to judge: within a day most of the victim's group and a handful of
  friends outside it; never most of the city.
- **Save:** hearsay survives depopulation, save and load; an NPC with hearsay only is not
  deviated and not in the skip mask; a record decayed below 0.05 is not restored as
  present.
- **Props:** after talking to an NPC with hearsay, the next NPC without one gets no
  hearsay greeting (`ClearNpcProps`).
- **Performance:** `TickEncounters` p99 stays within its 2 ms budget (KAR-488's
  measurement, re-run).

## 6. Owner questions

1. **Reach:** how far should one punch travel in a day? (§5's first guess.)
2. **Notoriety:** should NPCs who only heard of the player count towards the standing
   level's "recognised"?
3. **Prejudice:** a strong rumour shifts the first contact by up to ±0.1 trust. Stronger,
   weaker, or none?
4. **Which facts travel:** §2.3 includes paying. Should bribes travel (to authorities)?
5. **Help and harm travel differently:** with one shared attenuation, a punch travels two
   hops and a help one (§2.2). Accept that (good deeds spread less than bad ones), or give
   help its own, weaker attenuation?

## 7. Not in S2

- Gossip about NPCs other than the player (NPC reputations).
- Lies or distortion: a retelling only weakens, never flips.
- Hearsay changing behaviour (avoiding the player, calling the police): S4/S4b/S5 read
  it later.
- Gossip across clusters: S6.

## 8. Docs to update

- `docs/tale/docs/ARCHITECTURE.md`: hearsay beside player trust; the gossip step at the
  three encounter sites.
- `docs/tale/docs/CONCEPT.md`: the status table.
- `docs/tale/docs/REFERENCE.md`: the new narration props.
- `TALE-EMERGENT-STORIES.md`, in the S2-a PR:
  - §S2: link to this plan, and rewrite its first bullet. It says "each side's
    `Trust[-1]` moves a little towards the other's"; it becomes hearsay, kept apart from
    `Trust[-1]` (§2.1).
  - §9.4: mark "connected needs … gossip (S2)" as pending owner question 2.
- A status-log fragment per work package.


## 9. Review record

Review 1 (Fable, 2026-11-22): **AMBER**, eight required changes, all applied:
1. **Diffusion (§2.2):** the first-hand telling is attenuated too; both `told` from the
   state before the exchange; the worked example matches the formula.
2. **Hearsay is immutable (§2.1, §2.4):** replaced by reference at the locked write sites;
   reads compute decay without writing.
3. **DES trust (§2.5):** from `RelationshipTracker`, symmetric.
4. **Live seeds (§2.5):** pair key and `time.Ticks` at the drain, the per-(NPC, step)
   source at arrival; the drain sees post-encounter trust.
5. **Props (§3.1):** `ClearNpcProps` resets `npc.heard_*`; every key is published.
6. **Instruments (§3.4, §5):** hearsay in `SocialMetrics` and on `TaleDebugNpc`.
7. **Roadmap (§8):** §S2 rewritten to hearsay, §9.4 tied to owner question 2.
8. **Prejudice (§3.2):** in `PlayerTrust.Apply`; what `before` and `trust_player` report.

Also applied from its suggestions: confirmation without erosion (§2.2), the greeting's
lifetime (§2.4), the accurate reason for rejecting a second `Trust` key, the restore line,
and the line references.

Review 2 (Fable, 2026-11-22): **AMBER**, three required changes, all applied:
1. **Help reachable (§2.2, §3.1):** `MinTold` 0.15, the positive greeting at ≥ 0.15, the
   negative at ≤ −0.2. The help/harm asymmetry is stated and is owner question 5.
2. **One start for first contact (§3.2):** `PlayerTrust.StartFor`, used by `Apply` and by
   `_applyTrust`'s `before`; `Get` unchanged.
3. **Game time for the prejudice (§3.2):** `gameNow` threaded through `ApplyPlayerTrust`
   and `_applyTrust`.

Also applied: empty records and opposite-sign tellings (§2.2), the exact-threshold note
(§2.4), the arrival site's draw order and `ScenarioNpc` (§2.5), and the metrics' parameters
(§3.4).

Review 3 (Fable, 2026-11-22): **GREEN**, no required changes. Its optional points are
applied: `Exchange` takes the encounter time; a misplaced bullet in §2.5; the DES draw count
through an injected `Random`.
