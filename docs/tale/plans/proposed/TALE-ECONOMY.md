# TALE Economy — money as a flow, not a property (design)

> **Status:** proposed, for owner review · **Written:** 2026-11-21 · **Ticket:** KAR-485
> **Follows:** KAR-477 (PR #191, claim-triggered storylets) · **Roadmap:** [TALE-EMERGENT-STORIES.md](TALE-EMERGENT-STORIES.md) §S2b, §S4 · **Concept:** [../../docs/CONCEPT.md](../../docs/CONCEPT.md) §3 (crime from property pressure)

Every TALE storylet changes only its own NPC's properties, so money is
created and destroyed instead of moved: a merchant earns whether or not a
customer came, a worker's lunch money goes nowhere, and nothing can balance
income against spending. Over a year every role pins at one end of the
`[0, 1]` wealth scale. This document measures that, proposes a closed loop
(wages and stipends in from outside, purchases moving money to shopkeepers,
rent and cost of goods out), works out the mechanism for the DES and the live
game, and estimates the equilibrium with a prototype.

Line references are from branch `fix/tale-claim-storylets` at `586bbda4`.
Numbers come from a probe that replicates `ScenarioCompiler.RunSimulation`
(200 NPCs, 365 days, seeds 20000 and 20001, the "medium" bake's seeds) and
attributes every wealth and morality delta to the storylet that produced it.
The probe lived in `tests/JoyceCode.Tests/scratch/` and is not committed; §6
says what of it should become a test.

---

## 1. Today's money flow, measured

### 1.1 How wealth changes today

- A storylet's `postconditions` add or set properties of **the NPC that ran
  it** (`StoryletSelector.ApplyPostconditions`, `StoryletSelector.cs:205`);
  `RecordDelta` (`:258`) clamps to `[0, 1]`. There is no second party.
- The only two-party effects are `postconditions_if … target_*`
  (`ApplyConditionalPostconditions`, `:293`), evaluated at an encounter and
  **only in `DesSimulation`** (`:464-516`). One shipped storylet uses a target
  effect: `demand_protection` adds `target_fear +0.1`; the money it "collects"
  (`wealth +0.15`) is created, not taken.
- Claim storylets (`deliver_food +0.04`, `supply_goods +0.06`,
  `provide_trade_service +0.03`) credit the claimer; the requester pays nothing
  and `order_food` does not even feed the orderer (no hunger postcondition).
- Morality: four storylets lower it (`organize_criminal_operation −0.2`,
  `form_gang −0.15`, `seek_revenge −0.1`, `steal_food −0.02`), none raises it.
  In practice they are irrelevant: the biggest measured effect is
  `seek_revenge` at −0.001 per drifter-day. What moves morality is
  `DesSimulation.ApplyMoralityDrift` (`:658`), once per day: −0.03·(desperation
  − 0.4) while desperation > 0.4, +0.003 while it is < 0.2. **The live game
  has no drift at all** (`TaleManager._advanceNpc`, `:1200`, applies plain
  postconditions and group verbs only) — morality is frozen live except for
  the four storylets above.
- Desperation (`StoryletSelector.ComputeDesperation`, `:59`) =
  0.4·hunger + 0.3·(1 − wealth) + 0.2·anger + 0.1·(1 − health). Wealth is 30 %
  of it; anger 20 % — and anger has no decay (only `rest −0.02`,
  `socialize −0.03`), which matters below.

### 1.2 Per role, per NPC-day (seed 20000; seed 20001 in brackets where it differs)

"Intended" is the sum of the storylets' postconditions; "applied" is what
survived the clamp. Frequencies are storylet completions per NPC per day.

| Role (n) | Income /day (intended) | Spending /day (intended) | Net | Where the money comes from / goes | Wealth day 1 → 30 → 365 |
|---|---|---|---|---|---|
| worker (67) | +0.082 | −0.060 | **+0.022** | `work_manual` 1.80/day +0.072, `work_office` 0.20/day +0.010 · `eat_at_home` 1.02/day −0.021, `eat_out` 0.66/day −0.020, `sleep` 0.95/day −0.019 | 0.50 → 0.92 → **0.96** [0.98] |
| merchant (29) | +0.157 (applied +0.101) | −0.100 | **+0.057** | `serve_customers` 2.42/day +0.145 (no customer involved), `deliver_food` 0.28/day +0.011 · `restock` 0.73/day −0.037, meals −0.045, `sleep` −0.018 | 0.70 → 0.94 → **0.97** [0.94] |
| drifter (20) | +0.491 (applied +0.180) | −0.177 | **+0.31** | `collect_protection` 2.07/day +0.207, `organize_criminal_operation` 1.09/day +0.218, `deliver_food` 0.69/day +0.028, `scavenge` +0.021, `beg` +0.016 · `profit_sharing` 1.30/day −0.130, meals −0.042 | 0.21 → 0.99 → **0.98** [0.94] |
| hustler (14) | +0.090 | −0.065 | **+0.025** | `hawk_wares` 2.36/day +0.071, `broker_deals` +0.013, `black_market` +0.005 · meals −0.046, `sleep` −0.019 | 0.53 → 0.92 → **0.86** [0.89] |
| nightworker (18) | +0.052 | −0.055 | **−0.003** | `night_shift` 1.29/day · meals −0.047, `sleep` 0.44/day −0.009 | 0.36 → 0.35 → **0.37 ± 0.28** [0.05] |
| authority (18) | **0** | −0.038 | **−0.038** | nothing pays a patrol · meals −0.032, `sleep` −0.006 | 0.43 → 0.00 → **0.00** |
| socialite (27) | **0** | −0.146 | **−0.146** | nothing · `visit_bar` 2.12/day −0.106, meals −0.037 | 0.40 → 0.00 (broke by day 4) |
| reveler (7) | **0** | −0.147 | **−0.147** | nothing · `bar_crawl` 0.65/day −0.039, `club_night` −0.024, `pre_drink` −0.020, meals −0.045, `sleep` −0.013 | 0.25 → 0.00 (broke by day 3) |

Final morality (365 days): worker 1.00 [0.80], merchant 1.00, hustler 0.82
[0.42], nightworker 0.60 [0.18], drifter 0.00, authority 0.00, socialite 0.00,
reveler 0.00. Groups: 13 [15], all `social`; 0 criminal, because the drifters
who have the morality for it have wealth 0.98, and the roles with wealth 0 sit
in mixed groups.

Readings:

1. **Only the nightworker is near balance, by accident** (+0.052 vs −0.055),
   and is therefore the only role whose wealth spreads (σ 0.28). Every other
   role has a constant net drift and pins at an end within 4–30 days. Of the
   roughly **22 wealth-units a day the storylets create** in a city of 200,
   **18 are destroyed by other storylets** and the rest by the ceiling; the
   merchant alone loses 0.056/day of its intended income to the clamp.
2. **Three roles have no income at all.** Authority's patrols pay nothing;
   socialites and revelers only spend. They are broke within days and then
   their desperation (0.54–0.65) drains morality to 0 within ~120 days — the
   police force of a 365-day bake has morality 0.00 in both seeds.
3. **The drifter is the richest role** because two racket storylets print:
   `collect_protection` (+0.1, solo, no victim) and
   `organize_criminal_operation` (+0.2) run 2.1 and 1.1 times a day once a
   drifter is in a group and over wealth 0.6 (`demand_protection`'s gate). The
   drifter crosses 0.6 by day 10 on scavenging, begging and deliveries; from
   then on the racket pays 0.4/day. Their morality still hits 0 — not from
   wealth but from **anger** (0.45–0.59, fed by `scavenge +0.03`, `beg +0.04`
   and never decaying), which keeps desperation above the 0.4 drift
   threshold.
4. **Morality is a shadow of wealth.** With drift the only real force, the
   roles that pin at 1 reach morality 1.00 and the roles that pin at 0 reach
   0.00. "No storylet raises morality" is true but secondary; the real gap is
   that recovery exists only in the DES and only below desperation 0.2.
5. **Clamp losses hide the imbalance.** Looking at applied deltas, every role
   nets about 0 (the table's "applied" column is within ±0.002 for seven of
   eight roles) — because the clamp eats the surplus. Any tuning done on
   applied numbers would conclude the economy is balanced.

### 1.3 Other findings on the way (not economy, but they set the rates above)

- `witness_crime` (worker, socialite) and `initiate_greeting` (socialite,
  worker) run **12 times per worker-day each**: 2–3 minute storylets with no
  time window and the default weight 1.0 against the routine's 0.3–3.0. They
  emit 308,795 `crime_report` and 343,509 `greeting` requests in a year of 200
  NPCs. The money storylets' frequencies in the table are what is left of the
  day around them.
- `prepare_arrest` runs 1.67 times per authority-day: its precondition
  `"tags": ["evidence_found"]` is an array, and `ParseStorylet` keeps only
  object-valued preconditions (`StoryletDefinition.cs:335`), so it always
  passes.
- `seek_trade_service` (wealth ≤ 0.4) is rare today because everyone is rich
  (1,890 requests); in the prototype's poorer city it emitted 68,663–173,867.
  Any request whose fulfilment moves money needs a sane emission rate first.
- The eat and drink venues have no keeper: `Eat`/`Drink` shopfronts become
  `social_venue` locations (`SpatialModel.cs:252-257`), merchants work only
  at `Game2` → `shop` locations (`nogame.roles.json` merchant
  `workplace: ["shop"]`; `ScenarioCompiler.PickLocationByRole`). The
  synthetic bake city has 20 shops for 29 merchants (seed 20000); the
  `factory_worker`/`office_worker`/`service_worker` content that would staff
  venues is never assigned (no code references those roles).
- `models/tale/interactions/**` is loaded by neither path
  (`LoadFromDirectory` is non-recursive; the asset list does not declare it);
  the request/claim storylets that run are the copies in the role files.
- The live drift measurement in TALE-S0-GROUNDWORK §12 ("wealth halves in a
  week", 0.46 → 0.19) predates KAR-477; the ordinary selection of
  `respond_to_blackmail` (−0.05 per 10 minutes) explains most of it. It must
  be re-measured after PR #191 before anything live is tuned on it.

---

## 2. The proposed flow

Owner direction (2026-11-21): workers and others spend at merchants;
merchants are shopkeepers, hustlers the brokers; a purchase is a transfer; a
closed loop needs a source (wages from outside) and a sink (rent, restock to
outside); socialites and revelers need income or an endowment; morality may
need a way back up.

### 2.1 One picture

```
 outside the city ──wages/stipends──▶ NPC pockets ──purchases──▶ shopkeepers ──┐
        ▲                                 │                        │          │
        │                                 ├── rent ────────────────┼──────────┤
        │                                 ├── protection ─▶ racket  │          │
        │                                 └── fees ─▶ claimers      │          │
        └──────────── cost of goods (1 − margin) ◀──────────────────┘          │
        └──────────────────────────── rent ◀───────────────────────────────────┘
```

Three kinds of money movement, and nothing else may change `wealth`:

| Kind | Who | From / to | Expressed as |
|---|---|---|---|
| **Source** | wages (worker, nightworker, authority), stipends (socialite, reveler), street income (hustler `hawk_wares`, drifter `scavenge`/`beg` for now) | outside → NPC | storylet `wealth +x` on the *work* storylet, or per-role `dailyIncome` |
| **Transfer** | purchases, fees for claimed requests, protection money, profit sharing | NPC → NPC | new `transfer` postcondition (§3.1) or `target_wealth` |
| **Sink** | rent, cost of goods (the 1 − margin share of every sale), fines | NPC → outside | per-role `dailyRent`; the margin rule on transfers |

### 2.2 Purchases as transfers

Every meal and drink pays the keeper of the place where it happens.

| Storylet | Price (= today's cost) | Venue | Keeper |
|---|---|---|---|
| `eat_at_home` | 0.02 | home | keeper of the nearest `shop` (groceries) |
| `eat_out`, `lunch_break`, `night_meal` | 0.03 | `nearest_shop_Eat` | keeper of that Eat venue |
| `late_bite` | 0.04 | `nearest_shop_Eat` | " |
| `visit_bar` | 0.05 | `social_venue` | keeper of that Drink venue |
| `bar_crawl` | 0.06 | " | " |
| `club_night` | 0.08 | " | " |
| `pre_drink` | 0.02 | home | grocer |
| **new** `buy_round` (universal, social venue, wealth ≥ 0.4) | 0.04 | `social_venue` | keeper — a paid variant of `socialize`, which stays free so trust formation is not gated on money |

The keeper keeps the **margin** (proposed 0.4); the rest leaves the city as
cost of goods. `restock` keeps its fatigue and loses its `wealth −0.05`: cost
of goods is now paid per sale. `serve_customers` loses its `wealth +0.06`: the
shop earns when someone buys. `open_shop`/`close_shop` unchanged.

**Venue → keeper.** A venue's keepers are the NPCs whose
`WorkplaceLocationId` is that venue and whose role is merchant. Two changes
make every shopfront have one and every merchant have a venue where numbers
allow: merchants' workplace pool becomes all shopfronts (`shop` and the
`Eat`/`Drink` social venues) instead of `shop` only
(`nogame.roles.json`, `TalePopulationGenerator.AssignLocationByRole`,
`ScenarioCompiler.PickLocationByRole`), and assignment prefers a shopfront
with no keeper yet. Several keepers at one venue split its revenue (the
prototype's bimodal merchants, §4.2, were the nine of 29 with no venue). A
venue with no keeper sends its revenue outside (sink), never nowhere.

### 2.3 Sources

| Who | What | Amount | Why |
|---|---|---|---|
| worker | `work_manual`, `work_office` | +0.04 / +0.05 per storylet (unchanged) | ~0.08/day at today's frequency |
| nightworker | `night_shift` | +0.04 (unchanged) until its routine is fixed | it works 0.6–1.3 shifts a day because `wander` (6–11/day) and `rest` win selection; raising the wage to +0.06 did not help (§4.3-5). Fix the schedule, then set the wage so that ~1.5 shifts/day earn a worker's 0.08 |
| authority | `patrol`, `night_patrol` | **+0.015 each** (new) | 3.7 patrols a day → ~0.055/day; +0.02 made them the richest role in the prototype |
| socialite | `dailyIncome` | **+0.08/day** | the role's fiction is money from elsewhere; a daily stipend is deterministic and needs no storylet |
| reveler | `dailyIncome` | **+0.10/day** | spends ~0.10/day at venues; the stipend is what keeps the bars open |
| hustler | `hawk_wares` +0.03, `broker_deals` +0.05, `black_market` +0.08 | unchanged, from outside, for now | the broker's counterparties are not modelled; step 3 makes `hawk_wares` a passer-by transfer |
| drifter | `scavenge` +0.01, `beg` +0.01 | unchanged for now | `beg` becomes a passer-by transfer in step 3 |

### 2.4 Sinks

| Who | What | Amount |
|---|---|---|
| every housed role (all but drifter) | `dailyRent` | **0.02/day** — replaces the hidden `sleep −0.02`, which drifters (sleep_rough) never paid anyway but which anyone who `rest`s instead of sleeping also skipped |
| every sale | cost of goods | (1 − margin) × price, i.e. 60 % |
| merchant | `restock` | fatigue only (money moved into the margin rule) |

### 2.5 The racket and the claims become transfers

- `demand_protection` branch 1: add `"target_wealth": "-0.15"` — the victim
  pays what the drifter gains (DES: `ApplyConditionalPostconditions` already
  supports it; live: waits for S2b).
- `collect_protection`: loses `wealth +0.1` (a solo storylet cannot collect
  from anyone). Protection income is the `demand_protection` transfer; a
  recurring collection becomes a `postconditions_if` with a fearful target.
- `organize_criminal_operation`: `wealth +0.2` → **+0.05** in step 1, and in
  step 2 a `robbery` request/claim pair (`target_wealth −0.1` on the victim
  encountered) so crime proceeds come from someone.
- `profit_sharing −0.1`: distributed to group members (`GroupActions` knows
  the roster), not destroyed — step 2.
- Claim storylets: the requester pays the fee the claimer gets
  (`deliver_food +0.04`, `supply_goods +0.06`, `provide_trade_service +0.03`)
  and `order_food`'s requester gets `hunger −0.5` when the delivery completes.
  Both need the requester as a `target` of the claim storylet's completion
  (§3.1).

### 2.6 Spending that follows wealth — the stabiliser

A fixed income against a fixed spend pins at an end whatever the numbers;
balancing the constants to zero only trades a 30-day pin for a random walk.
The loop needs spending that rises with wealth. Two mechanisms, to be
combined:

1. **Wealth gates on discretionary purchases** (`preconditions.wealth.min`):
   `eat_out`/`visit_bar`/`bar_crawl` ≥ 0.4, `club_night`/`buy_round` ≥ 0.5,
   `lunch_break`/`late_bite`/`night_meal` ≥ 0.15, `pre_drink` ≥ 0.1.
   `eat_at_home` stays ungated. An NPC below the gate eats at home and saves;
   above it, it treats itself and spends more than it earns. Wealth
   oscillates around the gate. Visible in the game (who is in the bar),
   content-only, and what the prototype measured. The gates sit at 0.4–0.5
   on purpose: §4.3 found that below wealth ≈ 0.35 an ordinary, fed NPC is
   already over the morality drift threshold.
2. **A lifestyle term** in the daily settlement: spend `k · wealth` per day
   (k ≈ 0.04), paid to the NPC's social venue's keeper with the same margin
   rule. Continuous, so it gives a smooth fixed point instead of a band at
   the gate; invisible in the game. Measured as the `life0.04` variant in
   §4.3.

### 2.7 Morality and anger

- One **daily drift function shared by DES and live**, in the settlement
  step: −0.03·(desperation − 0.4) above 0.4 (as today), **+0.005/day below
  desperation 0.3** (today +0.003 below 0.2, DES only). Slow on purpose: the
  prototype's +0.01 took every comfortable hustler to morality 1.0 and
  switched off `broker_deals`/`black_market` (gated ≤ 0.5 / ≤ 0.4) — a
  comfortable hustler going legit is a story, every hustler doing so in 90
  days is a tuning fault.
- **Anger decays** by 0.02/day in the same step. Without it anger alone keeps
  drifters' desperation above 0.4 for ever (§1.2 reading 3).
- Storylet nudges so that doing right is visible: `provide_help +0.01`,
  `report_crime +0.01`, `leave_gang +0.05`. Small, like the negatives.

---

## 3. The mechanism

### 3.1 Content schema

One new postcondition key, parsed by `StoryletLibrary.ParseStorylet` next to
`request` and `group`:

```json
"postconditions": {
  "hunger": "-0.55",
  "transfer": { "wealth": 0.03, "to": "venue_keeper" }
}
```

- `to`: `venue_keeper` (keepers of the storylet's resolved location, split
  equally; a home resolves to the nearest `shop`), `requester` (the request
  this claim storylet answered — only for claim storylets),
  `group` (equal split over the NPC's group), `outside` (a pure sink, for
  fines).
- `margin` (optional, default from `/tale/economy/margin` = 0.4): the share
  the receiver keeps; the rest is a sink. Only meaningful for `venue_keeper`.
- The payer pays `min(amount, wealth)`; a payer with nothing pays nothing and
  the storylet still happens (the hungry still eat; whether they should is an
  owner question, §7). The transfer's wealth is **not** also listed as a
  plain `wealth` postcondition — one or the other, checked at load.

Per-role settlement in `nogame.roles.json`:

```json
"economy": { "dailyIncome": 0.08, "dailyRent": 0.02 }
```

and the shared constants under `/tale/economy` in `nogame.json`:
`margin`, `lifestylePerDay`, `moralityRecoveryPerDay`,
`moralityRecoveryBelowDesperation`, `angerDecayPerDay`. Chushi must watch
`nogame.roles.json` as a bake input (it watches nogame.json, the scenarios,
the storylets and the group types today, KAR-477).

### 3.2 Engine: the settlement step and the ledger

A new `engine.tale.Economy` (static, pure functions over `NpcSchedule`s),
used by both engines:

- `Settle(npc, gameDay, roleDef, constants)` — applies `dailyIncome`,
  `dailyRent`, the lifestyle spend, morality drift and anger decay for every
  day since `npc.LastSettledDay` (new field, persisted with the schedule;
  catch-up is a loop, bounded by the live tick's own catch-up cap). This
  replaces `DesSimulation.ApplyMoralityDrift`, which the DES calls at its day
  boundary for all NPCs (`:133`) — same numbers, same function, so the DES
  behaves as today where nothing else changed.
- `Transfer(payer, amount, receivers, margin)` — debits the payer, **credits
  a ledger** instead of writing to the receivers' `Properties`:
  `NpcSchedule.PendingCredit` (float, persisted). The credit is applied to
  the receiver's wealth at its own next advance or settlement.

Why a ledger: `TaleManager.AdvanceNpc` runs concurrently on pool threads
during spawn (`TaleManager.cs:25,86`), and today it writes only to the NPC it
advances; `GroupActions` is the one cross-NPC write and takes `_loSocial`.
Crediting another NPC's `Properties` from a pool thread would need that lock
on every storylet end. A per-NPC pending credit written under a cheap
`Interlocked`-style accumulate (or a `ConcurrentDictionary<int,float>` on
`TaleManager`) keeps the invariant that an NPC's `Properties` are written
only by its own advance. In the DES (single-threaded) the ledger is applied
immediately at the end of `ProcessNodeArrival`, so DES results are exact.

Venue keepers: `SpatialModel.Location.KeeperNpcIds` (list), filled by
`TalePopulationGenerator` (live) and `ScenarioCompiler.BuildSyntheticNpcs`
(bake) from the merchants' workplaces, after the assignment change of §2.2.
A storylet's venue is its resolved destination (`ResolveLocation`), already
known when it ends (`npc.CurrentLocationId`); `home` → `FindNearestOfType(home,
"shop")`.

### 3.3 What is DES-only, what works live before S2b

| Piece | DES (bake, Testbed, TestRunner) | Live game |
|---|---|---|
| purchases → `venue_keeper`, margin sink | yes | **yes** — a one-party storylet completion whose receiver is known from the venue; no encounter needed |
| `dailyIncome`, `dailyRent`, lifestyle, drift, anger decay | yes (day boundary) | **yes** — settlement in `AdvanceNpc` and the S0 tick |
| wealth gates | yes | **yes** — `PassesPreconditions` is shared |
| `target_wealth` (protection), requester-paid claims, `requester` hunger relief, `group` profit sharing, passer-by transfers | yes | **waits for S2b** (no encounters-with-storylet-context, no requests, no claims live); until then the live game has the racket storylets' *solo* effects only, which after §2.5 are small |

The live society starts from the bake (`ScenarioApplicator` overwrites
wealth and morality), so the bake's equilibrium is what the player meets;
the live settlement and purchases then keep it there instead of letting it
drift as §1.3's last point describes.

### 3.4 Determinism and independence

- **Same seed, same behaviour**: the DES is single-threaded and the ledger is
  applied in event order; nothing new draws randomness. Live, a credit's
  *amount* does not depend on thread order and it is applied at the
  receiver's next advance, so the receiver's trajectory is the same up to one
  storylet's timing — the same tolerance trust already has
  (`RuntimeSocialEvolver` seeds per cluster/day/tick).
- **NPC independence at generation** (seed hierarchy `cluster-…-npc-i`) is
  untouched: roles, locations, initial properties and keeper assignment are
  per-NPC functions of the seed (keeper assignment is a deterministic pass
  over the generated population, like today's `SocialVenueIds`). Evolution
  already couples NPCs through trust; wealth now couples them the same way.
  The saved state grows by `LastSettledDay` and `PendingCredit` per NPC
  (`ClusterSocialState.SnapshotProperties` must include them, or a snapshot
  will drop credit).

---

## 4. Expected equilibrium

### 4.1 The rough model

Per role: outside income `I`, fixed spend `F` (rent + groceries ≈ 0.02 +
1.2 × 0.02 = 0.044), discretionary spend `D` switched on above the gate `g`.
If `I − F > 0` and `I − F − D < 0`, wealth oscillates in a band above `g`;
otherwise it pins. Merchants have no `I`: their income is their venue's
revenue × margin, so it depends on where their shop is and how many
customers can afford to buy — a *circumstance*, which is the point.

| Role | I /day | F | D (when open) | Expected band |
|---|---|---|---|---|
| worker | 0.08 | 0.044 | eat_out 0.02 + buy_round 0.04–0.07 | 0.3–0.55 |
| nightworker | 0.05 today (0.6–1.3 shifts); a worker's 0.08 once the routine is fixed | 0.044 | eat_out, night_meal ~0.03 | 0.3–0.6 after the schedule fix; pinned at 0 before it |
| authority | 0.055 | 0.044 | eat_out 0.01 | 0.15–0.35 — a modest force |
| socialite | 0.08 | 0.044 | visit_bar 2.1/day × 0.05 = 0.10 | at the 0.3 gate (measured 0.28 ± 0.04) |
| reveler | 0.10 | 0.044 | 0.10–0.15 | at the gate (measured 0.31 ± 0.05) |
| hustler | 0.07–0.12 | 0.044 | eat_out, buy_round | 0.4–0.8; the rich hustler is the role's fiction |
| merchant | revenue × 0.4 | 0.044 | eat_out, buy_round | 0.3–0.8, wide; keeperless → 0 |
| drifter | 0.02–0.06 (scavenge, beg, deliveries) | groceries 0.03, no rent | none affordable | 0.05–0.3, wide; a delivery drifter climbs, a lone one starves |

With these, desperation ≈ 0.4·hunger + 0.3·(1 − w) + 0.2·anger (health ≈ 1)
sits at 0.25–0.4 for the housed roles (hunger averages ~0.4 between meals)
and 0.45–0.6 for poor drifters and keeperless merchants. Morality then
drifts down only for the latter, and `form_gang` (morality ≤ 0.4, wealth
≤ 0.3, not in a group) and `steal_food` (wealth ≤ 0.1, hunger ≥ 0.8) become
reachable for *whoever is poor*: a merchant whose quarter stopped buying, a
worker who lost 0.15 to a protection demand, not "drifters". Target for a
200-NPC, 365-day bake: 1–3 `criminal` groups of 4–15 (group type: mean
wealth ≤ 0.3 and morality ≤ 0.4), with members from at least two roles; no
role with more than 25 % of its members at either end of the wealth scale;
authority morality ≥ 0.6.

### 4.2 What the prototype showed (labelled: a probe, not the implementation)

The probe patched the loaded `StoryletLibrary` in memory and used the DES
logger hooks (which receive each NPC's live `Properties`) to move money,
pay stipends and rent at the day boundary, and assign keepers. It is the
mechanism of §3 without the ledger (the single-threaded DES needs none),
two seeds, 200 NPCs, 365 days.

| Variant | What changed | Result (seed 20000; 20001) |
|---|---|---|
| **v1** | gates on eat_out/bars at 0.25–0.35; `serve_customers` stripped; rent 0.03; stipends 0.08/0.10; claims paid by requester; no groceries, no margin, no keeper fix | **demand collapse**: everyone eats at home (money to nobody), merchants earn only claim fees → worker 0.11, merchant 0.14 (66 % at floor), socialite 0.13, reveler 0.10, authority 0.03; city 0.21, morality 0.07. Rent 0.03 + meals 0.04 exceeded a worker's 0.062 of applied wages. |
| **v2** | + groceries as purchases, margin 0.4, rent 0.02 and `sleep` stripped, patrol +0.02, protection `target_wealth −0.15`, `collect_protection` stripped, crime +0.1 | socialite **0.28 ± 0.04**, reveler **0.31 ± 0.05** (the gate band, as predicted); worker 0.74 ± 0.36 (45 % at ceiling — nothing discretionary to buy); merchant **0.46 ± 0.46**, bimodal: 41 % at floor = the 9 of 29 merchants without a venue, 34 % at ceiling; nightworker 0.02 (0.8 shifts/day); authority 0.96 (patrol pay too high); drifter 0.92 (deliveries and `provide_trade_service` 1.9/day at +0.03 paid by poorer requesters); city 0.59, morality 0.52 [0.32]. Money/year: 127,961 purchases, 1,180 to keepers (0.11 per merchant-day), 1,770 left as cost of goods, 1,044 wages and stipends in, 1,205 rent out, 478 in claim fees. |
| **v2 + recovery** | + morality +0.01/day below desperation 0.35 | morality: authority 1.00, socialite 1.00, hustler **1.00** (shady storylets never eligible again), worker 0.78, reveler 0.78, merchant 0.50, drifter **0.00** (anger 0.48, no decay) → recovery must be slower and anger must decay |
| **v3** | + every merchant given a venue (surplus merchants moved to keeperless shopfronts), `socialize` as a 0.03 purchase ≥ 0.4, `night_shift` +0.06, patrol +0.015 | §4.3: workers fall to 0.26 (a fee spiral), merchants still bimodal (footfall) |
| **v3 + lifestyle 0.04·w** | + continuous spend to the social venue's keeper | §4.3: smoother merchants (floor 34 % → 24 %), otherwise the same |

### 4.3 v3 results — and what they teach

| Role | v3 (20000; 20001) | v3 + lifestyle 0.04·w (20000; 20001) |
|---|---|---|
| authority | 0.44 ± 0.11; 0.21 ± 0.20 (35 % floor) | 0.36 ± 0.05; 0.13 (50 % floor) |
| drifter | 0.88; 0.86 | 0.77; 0.82 |
| hustler | 0.52 ± 0.18; 0.68 | 0.61 ± 0.14; 0.71 |
| merchant | 0.52 ± 0.45 (34 % floor, 34 % ceiling); 0.46 ± 0.47 | 0.58 ± 0.42 (24 % / 31 %); 0.55 |
| nightworker | 0.04 (83 % floor); 0.02 | 0.02; 0.02 |
| reveler | 0.23 ± 0.05; 0.21 | 0.16 ± 0.03; 0.15 |
| socialite | 0.29 ± 0.04; 0.31 | 0.28 ± 0.03; 0.28 |
| worker | **0.26 ± 0.16**; 0.17 (34 % floor) | 0.22; 0.11 (45 % floor) |
| city wealth / morality | 0.38 / 0.24; 0.35 / 0.16 | 0.36 / 0.22; 0.33 / 0.16 |
| criminal groups | 0 of 12; 0 of 8 | 0 of 12; 0 of 12 |

v3 is worse than v2 for workers and no better for merchants, and the reasons
are the useful part:

1. **A poverty trap from a mis-rated request.** `seek_trade_service` (wealth
   ≤ 0.4, 5 minutes, no time window, weight 1) ran **12.3 times per
   worker-day** once workers dipped under 0.4; drifters claimed 28,275 of its
   330,316 requests and, with requester-paid fees, took 0.03 each — about
   0.024 per worker-day, more than the worker's surplus. Being poor made
   them poorer. Requester-paid claims (§2.5) must not ship before the
   emission rates of §1.3 are bounded: **step 4 before step 2** in §5.
2. **Footfall is the merchant's circumstance.** Every merchant owned a venue
   in v3 and the distribution is still bimodal, because `nearest_shop_Eat`
   and the home → nearest `shop` rule send all custom to the few venues that
   win "nearest"; the rest earn nothing. In the real city this is geography
   (a shop in the quarter where the workers live). That is what we want from
   "circumstance, not role" — but it means a keeperless-or-footfall-less
   merchant is the first candidate for desperation, and the group-type
   thresholds should expect it. A mild `lifestyle` spend (paid to the NPC's
   *own* social venue) spreads revenue a little (floor 34 % → 24 %).
3. **The morality erosion line sits at wealth ≈ 0.35.** With hunger
   averaging 0.4–0.5 between meals, desperation = 0.4·hunger + 0.3·(1 −
   wealth) + … crosses the drift threshold 0.4 at wealth ≈ 0.35 even with no
   anger. v3's workers at 0.26 therefore lost morality (0.70 → 0.06 in a
   year) although they were fed and housed. Either ordinary roles' bands
   must sit above ~0.4 (gates at 0.4–0.5, not 0.3), or the threshold moves
   to ~0.5, or hunger's weight drops. This is the single most important
   constant for "crime by circumstance" and is an owner question (§7).
4. **Paid socialising at 0.4 was the push under the line.** `socialize` as a
   0.03 purchase took 0.012/worker-day and, with the fees, moved workers
   from v2's 0.74 to 0.26. Keep `socialize` free; `buy_round` gated at 0.5.
5. **The nightworker is a schedule bug, not an economy one**: 0.58 shifts
   and 10.9 `wander`s a day (`wander` weight 0.3 but always eligible; `rest`
   3.3/day). No wage fixes a worker who does not go to work. Fix the
   nightworker's routine (KAR-436) before tuning its wage.
6. **What held in every variant:** the stipend roles sit in the gate band
   (socialite 0.28–0.31 ± 0.04 across six runs, reveler 0.15–0.31 depending
   on gates); nobody pins at 1.0 except drifters (racket + services) and the
   merchants with footfall; the clamp loss is small. The stabiliser works;
   the constants need the owner's answers.

**Recommended first configuration** (v2 with the lessons applied): §2.2
purchases with margin 0.4, `buy_round` ≥ 0.5 instead of a paid `socialize`,
gates for `eat_out`/bars at 0.4, rent 0.02, stipends 0.08/0.10, patrol
+0.015, `night_shift` left at +0.04 until its routine is fixed, no
requester-paid claims. Expected: worker 0.5–0.7, authority 0.3–0.5, stipend
roles ~0.4, merchants wide and footfall-dependent, drifters 0.1–0.9 by
service income; criminal groups appear once `organize_criminal_operation`
stops paying (step 2) and the racket's victims exist live (S2b).

---

## 5. Changes required, staged

**Step 1 — the loop (content + engine, DES and live).** Smallest useful
step; everything in it works live before S2b.
- Content: `transfer` on the nine purchase storylets (§2.2) and `buy_round`;
  strip `wealth` from `serve_customers`, `restock`, `sleep`,
  `collect_protection`; `organize_criminal_operation` +0.05;
  `patrol`/`night_patrol` +0.015; wealth gates (§2.6-1);
  `demand_protection` `target_wealth −0.15`; `economy` block per role and
  `/tale/economy` constants; merchants' workplace pool = all shopfronts.
- Code: `ParseStorylet` (`transfer`, load-time check), `Economy.Settle` and
  `Economy.Transfer`, `NpcSchedule.LastSettledDay`/`PendingCredit` (+
  snapshot and save), `Location.KeeperNpcIds` and its assignment in both
  population builders, calls from `DesSimulation.ProcessNodeArrival`/day
  boundary and `TaleManager._advanceNpc`/S0 tick, `ApplyMoralityDrift`
  replaced by `Settle`, Chushi watching `nogame.roles.json`, `DbVersion`
  bump if the schedule schema changes what a save holds.
- Docs: REFERENCE.md storylet keys, ARCHITECTURE.md §3.4 and a new §
  "Economy", status-log fragment.
- Re-bake; the group-type thresholds (`criminal` wealth ≤ 0.3) are
  re-checked against the new distribution.

**Step 2 — rates.** The frequency anomalies of §1.3 (`witness_crime`,
`initiate_greeting`, `prepare_arrest`, `seek_trade_service`, the
nightworker's `wander`) — KAR-436's territory, but §4.3 showed that money
attached to a mis-rated request is a poverty trap, so this comes before any
requester-paid fee. Give request storylets a time window, a cooldown or a
weight below the routine's, and measure the per-NPC-day rates again.

**Step 3 — two-party money (DES now, live with S2b).** Requester-paid claims
and requester hunger relief (`to: requester`), `profit_sharing` to the group
(`to: group`), `robbery` request/claim replacing `organize_criminal_operation`'s
free income, `collect_protection` as a conditional on a fearful target.

**Step 4 — passers-by.** `beg` and `hawk_wares` as encounter-conditional
transfers from the partner (`postconditions_if` with `target_wealth`); the
hustler's `broker_deals`/`black_market` decided by the owner (§7).

---

## 6. Verification

**Bake and Testbed metrics** (add to `ScenarioStatistics`/`SocialMetrics`,
printed by `Testbed --days 365` and written to `scenario-statistics.json`):
- per role: wealth mean, σ, share ≤ 0.05, share ≥ 0.95; morality mean;
  desperation mean (today's `PropertyStats` is per property only);
- money per day: created (sources), destroyed (sinks), transferred, lost to
  the clamp — the probe's "intended vs applied" view, so a hidden imbalance
  cannot pass again;
- groups by type with role mix per `criminal` group.

**Acceptance numbers for the 365-day medium bake** (both seeds): no role
with > 25 % at either end; socialite, reveler, authority mean wealth
0.15–0.5; authority morality ≥ 0.6; 1–3 criminal groups with ≥ 2 roles
represented; money created ≈ money destroyed ± 5 % with clamp loss < 5 % of
created.

**xUnit** (`tests/JoyceCode.Tests/engine/tale/`, not under `navigation/`):
- `TransferPostconditionTests`: payer debited, keepers credited by equal
  share × margin, remainder leaves; payer with less than the price pays what
  it has; `transfer` and plain `wealth` together rejected at load; `to:
  requester` without a claim trigger rejected.
- `VenueKeeperAssignmentTests`: with ≥ merchants shopfronts every merchant
  keeps a distinct venue; with fewer, every shopfront has ≥ 1 keeper and the
  surplus merchants share; bake and live builders agree for the same inputs.
- `EconomySettlementTests`: income/rent/lifestyle/drift/anger for one day and
  for a 7-day catch-up equal 7 single days; `LastSettledDay` persists through
  `ClusterSocialState` snapshot and `NpcScheduleJson`.
- `MoralityDriftSharedTests`: DES and `TaleManager` call the same function
  (source-scanned like `NavJunction.At`), and the old DES numbers reproduce
  for an NPC with desperation 0.65.
- `EconomyEquilibriumTests` `[Trait("Category","Perf")]` first, gating once
  stable: 100 NPCs, 90 days, asserts the acceptance ratios above at looser
  bounds; logs the per-role table.
- `ScenarioCompilerTests` determinism keeps passing (the ledger must not add
  randomness).

**TALE scripts:** a `phase-economy` directory with scripts that assert
`wealth_transfer` events (new `IEventLogger.LogTransfer`) occur between a
worker and a merchant within the first simulated day, and that a `criminal`
group in a 120-day run has members of two roles. Scripts must assert what the
system emits (TestRunner rule).

**Live:** the S0 `TaleMetrics` log gains the per-role wealth means so a play
session can be compared with the bake it started from.

---

## 7. Open questions for the owner

1. **Scale.** Keep `wealth` in `[0, 1]` (a saturating "money in the pocket"),
   or make it open-ended cash with the social readings (desperation, group
   type) taken from a squashed value? The clamp is what turned a +0.02/day
   imbalance into a pin; with a closed loop it matters less, but a rich
   merchant will still hit 1.0.
2. **Who keeps the bars?** Merchants spread over all shopfronts (this
   document), or revive the unassigned `service_worker` as the barkeeper
   with its own income (a wage from the keeper's revenue)?
3. **Does company cost money?** `buy_round` as a paid storylet beside a free
   `socialize`, or leave socialising free and rely on bars for the stabiliser?
4. **Stipend fiction** for socialites and revelers: fixed daily money from
   outside, or a finite endowment that runs down (and then what)?
5. **Hustlers' money**: `broker_deals`/`black_market` from outside (the
   criminal economy beyond the city) or from victims/customers inside?
6. **Can the penniless eat?** The prototype let a payer with no money eat
   anyway (no purchase, hunger still relieved). The alternative — no money,
   no meal — makes `steal_food` reachable sooner and starvation real.
7. **Should a comfortable hustler go legit?** Morality recovery above the
   `broker_deals` gate (0.5) turns the role off over months.
8. **The player's cash.** `pay`/`bribe` already add `wealth` at 0.002 per
   cash (30-cash base fare ≈ 0.06 ≈ a worker's day). Should an NPC taxi
   passenger pay from its wealth, and should player payments to a shopkeeper
   enter the same ledger?
9. **Frequency anomalies** (§1.3): fix in this ticket or hand to KAR-436?
10. **Merchant/venue ratio.** Accept that a city with more merchants than
    shopfronts has co-owners sharing revenue, or cap the merchant share of a
    population by its shopfront count?
11. **Where is the erosion line?** Today a fed, housed NPC starts losing
    morality below wealth ≈ 0.35 (§4.3-3). Keep the desperation threshold at
    0.4 and hold ordinary roles above it with the gates, or move the
    threshold to ~0.5 so that only the genuinely poor erode? The answer sets
    how many criminal groups a bake grows.
12. **Footfall.** `nearest_shop_Eat` concentrates custom on a few venues, so
    some shopkeepers will starve wherever they are placed (§4.3-2). Accept
    as circumstance, or let NPCs pick among the nearest few venues?
