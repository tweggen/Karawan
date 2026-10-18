# From the Shipped TALE to Emergent Stories — Roadmap

> **Status:** proposed; owner answers in §8; review GREEN on 2026-10-18 (Fable, second round) · **Written:** 2026-10-18 (KAR-431)
> **Goal:** [../../docs/CONCEPT.md](../../docs/CONCEPT.md) · **Starting point:** [../../docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md)

The concept's goal: NPCs evolve through their interactions, form factions,
and eventually draw the player into storylines — without TALE generating
storylines explicitly. This plan orders the missing pieces (CONCEPT §5) into
stages. Each stage is useful on its own, builds only on what the previous
stages and the shipped code provide, and ends in something measurable.

The stages are called **S0…S6** on purpose: "Phase" letters are already
taken twice (routing Phase D, TALE-SOCIAL Phase D).

The owner's answers (§8) are folded into the stages below; §9 spells out
what bringing back the social-capital levels means. Each stage gets its own
plan and tickets once it is accepted.

| Stage | In short | Needs |
|---|---|---|
| S0 | Tests, metrics, debug views; the society keeps living and remembers the player | shipped code |
| S1 | The player in the social graph, including world actions | S0 |
| S2 | Opinions of the player travel | S1 |
| S2b | Requests, escalation and interrupts in the live game | S0 |
| S3 | NPCs ask the player | S1, S2b |
| S4 | Groups with aims, also in the bake | S2b |
| S4b | NPCs confront the player physically | S1, S4 |
| S5 | Surfacing opportunities | S3, S4 |
| S6 | Beyond one cluster | S1 (better after S5) |

## S0 — Ground to stand on (prerequisites)

> Stage plan: [TALE-S0-GROUNDWORK.md](TALE-S0-GROUNDWORK.md) (KAR-437).

You can't tune a society you can't measure or see, the test suite can't
tell when social behaviour breaks, and today the society both stands still
away from the player, and NPCs are marked as despawned by the wrong
fragment.

- **KAR-433** — 49 TALE scripts assert nothing; fix them and make an empty
  script fail.
- **Remember the player, explicitly.** The code has a path that drops an
  NPC the player talked to 30 minutes after the last conversation
  (`TaleSpawnOperator.TerminateCharacters` → `TaleManager.ForgetSchedule`),
  but it never runs: the spawn system counts entities by behaviour type,
  and TALE NPCs never carry the `IdleBehavior` their operator registers,
  so no TALE NPC is ever terminated by distance. They die only when their
  fragment unloads. In-session the player's NPCs therefore survive today,
  but by accident, and dematerialisation is tracked by the schedule's
  fragment instead of the entity's, which can leave an NPC that never
  respawns or create a second entity for one NPC. New rule: an NPC with a
  `Trust[-1]` entry or any `player_fact.*` is never removed (not on
  depopulation either), and dematerialisation follows the entity's death.
  Details: [TALE-S0-GROUNDWORK.md](TALE-S0-GROUNDWORK.md) W3.
- **KAR-434** — wire `DepopulateCluster`, so the snapshots this relies on
  are taken when a cluster is left, not only when saving. (Its comment at
  `TaleSpawnOperator.cs:463-464` claims it already removes schedules.)
- **Keep the society moving away from the player** (§8 Q1). `AdvanceNpc` is
  called only during the warm-up in `PopulateCluster`
  (`TaleManager.cs:150,158`), when a materialised NPC finishes a storylet
  (`TaleEntityStrategy.cs:271`) and at spawn (`TaleSpawnOperator.cs:280`).
  After the warm-up, abstract (Tier-3) schedules stand still, so
  `RuntimeSocialEvolver` makes them meet at stale locations. Add a cheap
  tick that advances Tier-3 schedules whose storylet has ended, without
  entity work, in the social frame; measure its cost at the largest
  population (4095 NPCs per cluster).
- **Social metrics,** computed in one place (`TaleManager` /
  `ClusterSocialState`) and shared by a test, the debug panel and the
  Testbed: groups per cluster, sizes, churn per day, trust-graph density,
  inter-group trust, share of NPCs who ever met the player (S1+), and the
  bake's match ratio per cluster (`ScenarioApplicator` covers at most the
  scenario's NPC count, ≤ 250 with "large" disabled; the ratio is already
  logged, `TaleManager.cs:183-188`). `SimMetrics` hard-codes three rates
  (KAR-436).
- **Baseline:** these metrics for a few clusters, both after 30/90
  simulated DES days (Testbed) and **at the moment the player arrives**
  (after bake, snapshot overlay and warm-up), since that is the society the
  player meets (§8 Q1).
- **TALE debug views** (§8 Q3), for development, not for the player:
  - **Per-NPC overlay:** an `OSDText` on each materialised TALE NPC, the way
    `GeneratePolytopeOperator.cs:140` labels polytopes, showing role, current
    storylet, group, `Trust[-1]` and (from S1) the player's standing level.
    Off by default, switched by a debug setting.
  - **Debugger panel:** a TALE pane in the existing ImGui debugger
    (`JoyceCode/ui/`, toggled by `nogame.modules.debugger.DebuggerToggle`):
    the cluster's groups, members and hangouts, inter-group trust, the
    player's standing per NPC and per group, and the metrics above.
- **Measure NPC density around the player:** how many TALE NPCs are within
  100 m of the player, by time of day, in a small and a medium cluster.
  Materialisation has no indoor/outdoor notion (every NPC in a loaded
  fragment is spawned, standing at a building entry when "at home"), so
  count by location type: on the street or at a venue versus at home or at
  work. S5's target (§8 Q4) is only meaningful against this number.

*Done when:*
- reverting the +0.02 conversation trust or dropping the group verb in
  `AdvanceNpc` makes a named test fail;
- an NPC talked to an hour ago still has its `Trust[-1]` (test);
- over one game day, the location distribution of Tier-3 NPCs changes
  (test), at a measured cost per social frame;
- the debug views show one NPC's and one group's state in the running game;
- the baseline and the density numbers are written into this plan, under
  S0 (they set S4's bake option and S5's target).

## S1 — The player in the social graph

> Stage plan: [TALE-S1-PLAYER-IN-GRAPH.md](TALE-S1-PLAYER-IN-GRAPH.md) (KAR-438).

Today the player's only relationship is `Trust[-1]` per NPC: it starts at
0.5 on first contact and rises by 0.02 whenever a conversation ends
(`TaleModule.cs:766-767`).

- **A defined baseline.** 0.5 is already "friend" in
  `nogame.relationship-tiers.json` (0.4–0.7), so one chat makes a friend.
  S1 sets the player's starting trust explicitly: no entry means "doesn't
  know the player"; first contact starts low (around the stranger /
  acquaintance boundary). The standing levels (§9) get their own thresholds
  rather than reusing the NPC–NPC tier names.
- **Player interactions** reuse the interaction types' trust deltas from
  `nogame.interactions.json` (`GetTrustDelta`), not their conditions: those
  evaluate two NPC schedules and the player is none. New ids are added for
  what has no equivalent among the 12 shipped ones (`assault`, `pay`,
  `bribe`, …). Narration gets a function to apply one
  (`tale.interact("help")`); conversation scripts choose which.
- **World actions count as interactions** (§8 Q2), not only conversation
  choices. Each has a hook in the game already:

  | Action | Where it happens today | Becomes |
  |---|---|---|
  | Hitting an NPC | player punch → `CitizenCollisionRouter` → `HitEventPath` → the NPC flees | an interaction (`assault`) on the victim |
  | Driving into an NPC | vehicle contact → `CrashEventPath` → `RecoverStrategy` | a stronger one on the victim |
  | Paying an NPC | `GameState.Cash` exists but nothing reads it | `pay` / `bribe`, new conversation option |
  | Driving an NPC | the taxi quest | `help`-like, on quest success |

  NPCs who **witness** an action (in sight, within a radius) get a weaker
  change and a `player_fact.*` entry, which S2 lets travel.
- **Standing:** group standing = an aggregate of the members' `Trust[-1]`
  (mean, weighted by the member's standing in the group), exposed to
  dialogue as `npc.group_standing`, so faction members treat the player by
  the group's opinion, not only their own. The player's **standing level**
  in the cluster (§9) is computed from the same data and exposed as
  `player.standing`.
- `Trust[-1]` is already serialised in snapshots and in the deviation
  record; S0 makes its survival in play an explicit rule.

*Done when:* helping one member of a group changes how the group's other
members greet the player, and a hit on an NPC lowers the victim's and a
witness's trust. Both are tested at the event level (the TALE script
harness has no collision path: a test raises `HitEventPath` and checks the
interaction) and are visible in the S0 debug views.

## S2 — Consequences travel (gossip)

- During NPC–NPC encounters (`RuntimeSocialEvolver`, and DES for tests),
  opinions of the player diffuse: each side's `Trust[-1]` moves a little
  towards the other's, weighted by their mutual trust. Strong opinions
  travel further than weak ones. An NPC without an entry doesn't join in
  until it has heard something (S1's baseline).
- `player_fact.*` entries (`tale.npc.remember`, and S1's witness entries)
  spread the same way with a probability, so "the player beat up a
  syndicate member" can reach the syndicate.
- Bounded and deterministic, on the evolver's own RNG; never through the
  storylet RNG. It relies on S0's Tier-3 tick, so encounters happen where
  NPCs actually are.

*Done when:* an NPC the player never met has a non-neutral opinion after
the player helped or harmed someone in that NPC's circle, measured in DES
and in a unit test of the live `RuntimeSocialEvolver` (unless both share
one diffusion function).

## S2b — Requests, escalation and interrupts in the live game

Requests and claims, conditional postconditions (`postconditions_if`,
`target_…`) and interrupts run only in `DesSimulation` (`:373-545`,
`ApplyConditionalPostconditions` called only at `:432,457`). The live
`TaleManager.AdvanceNpc` applies plain postconditions and group verbs. So
the escalation chains the factions are meant to drive
(`demand_protection` → …) never happen in the game, and the `request` /
`claim_trigger` entries in the shipped role files are loaded and ignored
live.

This is the largest single item in the plan. It needs:
- a live request pool, matched through encounters with storylet context;
- interrupts that abort a running `TaleEntityStrategy` mid-walk (Nest,
  Replace), and the same rules for Tier-3 schedules under S0's tick;
- shared code with DES (as `GroupActions` already is), accepting that live
  encounters use a different RNG than DES.

Decide here whether the unloaded `models/tale/interactions/` files ship or
are deleted (KAR-436).

*Done when:* an escalation chain observed in DES (e.g. a protection demand
followed by the merchant's fear and a request for help) also plays out in
the live game, for a materialised NPC and for a Tier-3 one, with a test.

## S3 — NPCs ask the player

The request/claim/signal pool is NPC-only; after S2b it runs live, but the
player never takes part.

- **Two kinds of request.** S5's density target (§8 Q4) cannot be met by
  friends alone, so a stranger must be able to offer something too:
  - **Low-stakes** requests (a delivery, a lift, a small favour) can go to
    any player in reach, including an anonymous one.
  - **Trust-gated** requests (protection, information, taking sides) need a
    minimum standing (§9) with the requester or the requester's group.
- **The player as a claimer:** a request no NPC claims within a time window
  is offered to the player — first as a conversation option when the player
  talks to the requester, later (S5) as an approach.
- **Fulfilment through the existing quest system:** the claimed request
  becomes a quest (`QuestFactory`, satnav follow). Completion emits the
  request's signal and applies the interaction's trust change; refusing or
  failing applies its own change. Start with one request type end to end:
  `order_food` (worker, drifter; request `food_delivery`) answered by the
  player instead of the merchant's `deliver_food`. A `help_request`, which
  `provide_help` (worker, socialite) claims, is emitted only by the unloaded
  `interactions/social/help.json`, so it is a candidate only if S2b ships
  that file.
- **Favours are remembered:** a completed or refused trust-gated request is
  recorded on both sides (owed / owing), which §9's upper levels read.
- NPCs' own claims keep priority, so the society does not start waiting for
  the player.

*Done when:* one request type goes from an NPC's need to a quest and back
into trust, with a test that drives it.

## S4 — Groups with aims

Groups form and gather (E1–E5) but want nothing as groups.

- **Group needs** derived from members' state: a criminal group with low
  wealth looks for targets; a trade group under threat looks for
  protection. Expressed as group-level preconditions that storylets can test
  (`group.wealth_mean`, `group.fear_mean`, `group.size`).
- **Inter-group relations:** aggregate trust between groups; groups sharing
  a hangout or territory accumulate tension or alliance.
- **Group storylets:** conflict and cooperation storylets keyed on those
  relations, using the escalation and interrupt machinery from S2b.
- **In the bake too** (§8 Q1): the scenario bake (`ScenarioCompiler`, 365
  simulated days) runs the same group logic, so a cluster the player enters
  already has rivalries and alliances in place. The bake covers at most the
  scenario's NPC count (≤ 250 today); for the rest of a larger cluster,
  either the "large" category is enabled (minutes more per build) or S0's
  Tier-3 tick lets relations develop after arrival. S0's match ratio
  decides which.

*Done when:* DES runs show measurable inter-group tension and its
consequences (members leaving, rivalries, protection), with S0 metrics, and
a freshly populated cluster starts with non-zero inter-group tension.

## S4b — NPCs confront the player physically

The owner wants NPCs able to engage the player physically (§8 Q2). NPCs
today only flee when hit, and no NPC attacks.

- What exists: the player punches (`WalkController`); the router classifies
  `AnyWeapon` contacts, and its `NpcWeapon` layer bit already exists
  (`CollisionProperties.cs:39`); the player has `GameState.Health`, today
  reduced only by car crashes (`DriveCarCollisionsController.cs:41`).
- What is missing: an NPC attack strategy (approach, strike, break off), an
  NPC weapon collider, player damage from it, and the outcome fed back into
  TALE (trust, fear, `player_fact.*`).
- Triggered by TALE state, never scripted: a group storylet (S4) such as
  enforcing protection, or an NPC whose trust in the player is very low and
  whose anger is high.

This is a gameplay system of its own (animation, balance, what happens when
the player loses) and gets its own plan. It needs S1 (so a fight changes
standing) and S4 (so groups have a reason to fight); it can run in parallel
with S5.

## S5 — Surfacing what matters

TALE still authors no story; S5 decides **which** of the society's
situations are worth showing the player and how.

- **Opportunities:** anything the player can act on right now with a
  consequence in TALE — an NPC with a request (S3), a situation the player
  can step into (an argument, a demand for protection, a confrontation), a
  service job.
- **Density target (§8 Q4):** about **4–5 opportunities within 100 m of the
  player at any time**, as a starting value to tune. Two consequences:
  - It is checked against S0's density measurement. If, say, only ten NPCs
    are usually within 100 m, half of everyone would carry an offer and the
    city would feel staged; then the target needs to count situations
    (several NPCs) rather than single offers, or the radius grows.
  - Most opportunities at low standing are low-stakes (S3); the share of
    trust-gated ones grows with the player's standing level (§9). The
    density stays the same while its texture changes.
- **Hooks:** detectors over TALE state for situations involving people the
  player knows — an escalation against a trusted NPC, a rivalry next to the
  player's usual places, an unclaimed request from a friend.
- **Delivery:** a rumour line in other NPCs' dialogue, an S3 request offered
  proactively, or an NPC approaching the player. The last one is new
  behaviour: today a conversation starts only when the player presses "E to
  talk" (`TaleConversationBehavior`). `Narration.TriggerConversation`
  (`Narration.cs:79`) can start a script from code, but the NPC walking up
  and addressing the player, gated by `MayConverse`, has to be built.
  Quests use the followed-quest machinery as they are.

*Done when:* in a normal play session the opportunity count within 100 m
stays near the tuned target, shown by the S0 debug views, and each
opportunity can be traced to TALE state.

## S6 — Beyond one cluster

- Standing per cluster, decaying over time; arriving in a new city starts
  the player as anonymous there (NPC_STORIES "geography as pacing", §9).
- Groups and their opinion of the player persist across visits (snapshots
  already do this for NPCs; S0 makes them taken on leaving).

## 7. What this plan deliberately leaves out

- No story graph, arc library or generated plot (CONCEPT §2).
- No player-facing standing display for now (§8 Q3); only the S0 debug
  views. Revisit after S5 is playable.
- No change to how the city is generated.

## 8. Owner's answers (2026-10-18)

1. **Order.** The society should evolve at runtime for quite some time before
   the player enters, so the player meets a lively ecosystem. The
   implementation order S1–S4 stays as proposed.
   → S0 keeps abstract schedules moving and baselines the society at the
   moment the player arrives; S4 also goes into the bake.
2. **Player actions.** Paying, hitting and driving someone count as actions,
   not only conversation choices. NPCs might also engage the player
   physically (some kind of fighting).
   → S1 world actions; S4b.
3. **Visibility of standing.** Undecided for the final game. For development,
   NPC and group stats should be visible, either per entity in the game (as
   for the polytopes) or in a dedicated engine debugger view.
   → S0 debug views (both).
4. **S5 rate.** Very unsure; as a start, 4–5 opportunities within about 100 m
   at any time.
   → S5 density target, checked against S0's density measurement; S3's
   low-stakes requests.
5. **Social-capital tiers.** They should come back; the consequences are
   spelled out in §9.

After the review on 2026-10-18, the port of the DES-only mechanics moved
out of S0 into its own stage S2b: S1 and S2 don't need it, S3 and S4 do.

## 9. Bringing back the social-capital levels

NPC_STORIES describes four levels: anonymous, recognised, connected,
entangled. What it means to bring them back:

1. **A reading, not new state.** The level is computed from what exists or
   comes with S1–S3: `Trust[-1]` across the cluster's NPCs, group standing
   (S1) and the favour record (S3). Nothing new to save, and it is per
   cluster by construction, which gives S6's "a new city starts you
   anonymous" for free. It relies on S0's rule that the player's NPCs are
   not forgotten.
2. **Called "levels", not "tiers".** TALE already has simulation Tiers 1–3
   (ARCHITECTURE §6.3); reusing the word would make every sentence
   ambiguous.
3. **They gate what the society offers.** This is their main effect on play:

   | Level | Roughly when | What becomes possible |
   |---|---|---|
   | Anonymous | hardly anyone knows the player | low-stakes requests, service jobs, overhearing |
   | Recognised | a few NPCs trust the player clearly above the S1 baseline | small favours, introductions, rumour lines about people the player knows |
   | Connected | clear positive or negative standing with at least one group | unsolicited problems, trust-gated requests, conflicts arriving uninvited |
   | Entangled | favours owed in several directions, standing with rival groups | dilemmas: two groups asking for opposite things |

   So the levels are how "progression" shows without an XP bar: the same
   density of opportunities (§8 Q4), with a different mix.
4. **They arrive in steps.** Recognised needs S1; connected needs group
   standing (S1) and gossip (S2) to mean anything; entangled needs the
   favour record (S3) and rival groups with aims (S4). The level is defined
   in S1 but only fully meaningful after S4.
5. **They go down as well as up.** Harm, refusal and time lower trust; in
   S6, standing decays while the player is away. Leaving a city is the
   pressure valve NPC_STORIES describes.
6. **The thresholds don't exist yet.** They are the levels' own, not
   `nogame.relationship-tiers.json`'s (under which today's 0.5 start is
   already "friend"). S1 sets a first guess; S0's metrics and play sessions
   tune it.
7. **Hidden by default**, consistent with §8 Q3: NPCs show the level in what
   they say and offer; only the debug views show it as a value.
