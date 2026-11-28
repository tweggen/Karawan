# TALE S3 — NPCs ask the player (stage plan)

> Stage S3 of [TALE-EMERGENT-STORIES.md](TALE-EMERGENT-STORIES.md) (KAR-441).
> Depends on S1 and S2b (both done; S2b's live request pool is what S3 builds on). Folds in
> KAR-495 (the player's "help" is almost unreachable in game).
> **Status:** proposed; **review GREEN** (Fable, round 3, 2026-11-28; rounds 1–2 YELLOW, all findings worked in, §10) · **Written:**
> 2026-11-28 · Line references are from `origin/master` at `078ed3b0` (still valid at `cab71a9e`).

Until now only NPCs answer each other's requests. S3 lets the player answer them: an NPC
who needs something asks the player in conversation, the player's "yes" becomes a quest, and
doing it changes how the NPC — and through S1's group standing and S2's gossip, its circle —
regards the player. The owner's rule for what this costs (2026-11-28): **helping costs
playing time, plus possibly the good delivered or the way to deliver it; it carries no
separate cash price.** So the large trust gain of `help` is earned by doing something, not by
picking a line in a dialogue.

## 1. What S3 found in the code

### 1.1 The live request pool (S2b)

1. **One pool per populated cluster,** `TaleManager.LiveSocial.Pool` (`TaleManager.cs:106`),
   created after the populate warm-up (`:414-419`), dropped on depopulate (`:506`), never
   saved (`:95-97`).
2. **Requests come only from a storylet's `postconditions.request`,** emitted when the
   storylet starts (`TaleManager._runArrival` → `TaleSocialRules.EmitRequest`,
   `TaleManager.cs:1606`, `TaleSocialRules.cs:135-147`). Fields (`InteractionRequest.cs`):
   requester, type, location (the NPC's destination; the JSON `location` is parsed and
   ignored), urgency, timeout, emitting storylet, `ClaimerId`, `EmittedAt`.
   **`EmittedAt` is wrong:** always `timeout − 1 min` (`InteractionPool.cs:40`).
3. **Claims happen only at encounters** — arrival encounters (`TaleManager.cs:1608-1619`) and
   the social tick's (`:1440`, `:1474-1487`). There is no radius or location check: any NPC
   with any encounter can claim any open request in the cluster whose type its role can
   answer (`TaleSocialRules.ClaimRequests`, `:154-217`). Ufyo claims 87 % live (KAR-488).
4. **A claim emits `request_fulfilled` at once** (`TaleSocialRules.cs:212`), before the
   claiming storylet has run. **Nothing in the game reads signals** (`HasSignal` only in
   `LiveRequestClaimTests.cs:217`); `wait_edge` is parsed and never read.
5. **Unclaimed requests** are purged when they time out (`TaleManager.cs:1502`); at a date
   change, `ResolveDaily` fulfils still-open ones abstractly but leaves `ClaimerId` unset
   (`TaleSocialRules.cs:336-364`).

### 1.2 The first request type

6. **`order_food`** (`models/tale/worker.json:41-58`): workers and drifters, `hunger ≥ 0.6`,
   at home, 2 game minutes, request `food_delivery` with a 60-minute timeout. It has **no
   hunger postcondition** — ordering food never feeds anyone (also noted in TALE-ECONOMY.md).
   Hunger rises 0.04 per waking hour (`StoryletSelector.cs:26`, `:228-232`). The eating
   storylets start lower — `lunch_break` at 0.4 (−0.55, `worker.json:30-34`), `eat_at_home` /
   `eat_out` at 0.5 (`universal.json:32,43`) — so an NPC reaches 0.6 only when it keeps losing
   the weighted draw, and `order_food` itself runs at home, indoors, where the player cannot
   talk to it.
7. **`deliver_food`** is defined twice with identical content (`merchant.json:49-61`,
   `drifter.json:38-50`): claimed by merchants and drifters, 30 minutes at the workplace,
   wealth +0.04 to the deliverer.
8. **The clock:** a game day lasts 30 real minutes (`daynite/Controller.cs:25`), so one game
   minute is 1.25 real seconds. **`order_food`'s request lives 75 real seconds,** and most of
   them are claimed by an NPC well before. A player who must meet the requester and talk to it
   inside that window would almost never see the offer.

### 1.3 Quests, conversations, interactions, saving

9. **`QuestFactory.TriggerQuest(id)`** allows one entity per quest id (`QuestFactory.cs:35`);
   `DeactivateQuest(e, isSuccess)` toasts "Finished"/"Missed" (`QuestCompletionModule.cs:16-38`)
   — **no caller passes `isSuccess: false` yet.** The taxi quest is the model for a multi-phase
   quest with saved data (`TaxiQuestData`, `TaxiQuestCreator`, `TaxiQuestStrategy`, `ToLocation`
   targets with `OwnerQuestEntity`); satnav follows it automatically when nothing else is followed (`SatnavService.cs:116-127`).
   `ToSomewhere` can follow a moving entity (`ParentEntity`, `ToSomewhere.cs:384-389`), but a
   TALE NPC has an entity only while materialised, and there is no id → entity index.
10. **Conversation props** come from `TaleNarrationBindings.InjectNpcProps`
    (`TaleNarrationBindings.cs:143-270`) and are reset by `ClearNpcProps` (`:277-327`); choices
    reach code through events (`tale.interact`, `tale.pay`, `TaleModule.cs:394-447`), which hop
    to the logical thread. Script resolution (`ConversationScriptResolver.cs:23-48`): explicit →
    `tale.{storylet}` → first tag → role → generic. **No prop says "this NPC has an open
    request".**
11. **`help`** (`nogame.interactions.json:112-125`): player trust +0.4, cooldown 720 game
    minutes, repeat scale 0.3, fact `helped_me`. Its only route in play is the desperate
    merchant's "I'll put in a word for you around here." (`tale.role.merchant.json:113-126`) —
    KAR-495.
12. **Favours:** `PlayerStandingLevel.Measure` hard-codes `FavourDirections = 0`
    (`PlayerStandingLevel.cs:121`), so "entangled" is unreachable.
13. **Saving:** an NPC the player knows (`IsKnownToPlayer`: a `Trust[-1]` entry or a
    `player_fact.*`) keeps its schedule through depopulation (`TaleManager.cs:458-466`), and
    `NpcScheduleJson` saves its **Properties**, Trust and Hearsay (`ArcStack` and
    `NextForcedStorylet` are not saved). The request pool is not saved.
14. **Locations have no names.** `SpatialModel.Location` carries id, type, positions, capacity,
    `ShopType` ("Eat", "Drink", "Game2" or null), quarter and estate (`SpatialModel.cs:10-31`).
    `FindNearestOfType(fromId, type, shopType)` exists (`:127`) but filters by location type
    first, and **food and drink shops are typed `social_venue`**, only "Game2" is `shop`
    (`SpatialModel.cs:254-260`). A merchant's workplace is a shop of any type, so "a merchant's
    workplace" would also pick bars and arcades.
15. **There is no "give up" for quests:** `QuestLuaBindings` exports only `getQuestList`,
    `followQuest`, `unfollowQuest`.
15b. **Only NPCs on a street can be talked to.** An NPC at any location that is not a
    `street_segment` is hidden as indoor (`TaleEntityStrategy.cs:505-516`,
    `StayAtStrategyPart.cs:12-34`); an NPC in transit is visible.
16. **The player's −1 is taken elsewhere:** `ResolveDaily` uses source −1 for "abstract"
    (`TaleSocialRules.cs:359-361`); the debug snapshot maps an unclaimed request to claimer −1
    (`TaleManager.cs:155`) and the panel shows `ClaimerId >= 0` as claimed (`ui/Tale.cs:167`).
17. **`StoryletSelector.PassesPreconditions` is `private static`** (`StoryletSelector.cs:120-160`)
    and checks more than properties (time of day, desperation, morality, group gates, home
    feasibility).
18. **`TriggerQuest(id, activate)` takes no payload** (`QuestFactory.cs:35`); the taxi hands its
    data over through module state before triggering (`TaxiNpcSpawnerModule.cs:168`).
19. **Goods and money:** `GameState.Cash` is written by the taxi fare and `tale.pay`. The
    inventory (`Pickable`) has no food item and no "carry to an NPC" path.

## 2. Approach

**An offer comes from the NPC's need, not only from the pool.** Finding 8 rules out "talk to
the requester while its pool entry is open" as the only route. So for each request type the
player can take, S3 names the **need** behind it, with its own roles and thresholds — for
`food_delivery`, a worker or drifter with `hunger ≥ 0.45` (above `lunch_break`'s 0.4, below
`eat_at_home`/`eat_out`'s 0.5 and `order_food`'s 0.6, so a hungry NPC on the street asks; the
gate in §6 decides whether that is common enough; finding 6). When the player
talks to an NPC:

- If the NPC has an **open, unclaimed request** of that type in the pool, the player is offered
  that request; accepting claims it **for the player** (`ClaimerId = InteractionPool.PlayerClaimerId`,
  **−2** — −1 already means "abstract" and "unclaimed", finding 16), so no NPC claims it
  afterwards and `ResolveDaily` leaves it alone.
- Else, if the NPC **has the need** and **no NPC has claimed a request of that type from it**
  in the pool, the player is offered the same thing; accepting emits the request with the player
  as claimer.
- Else nothing is offered. An NPC's claim always wins (roadmap: "NPC claims keep priority"):
  the player only ever gets what no NPC has taken.

**No extra waiting window** before the player may take a request (the roadmap mentions one):
the player sees only unclaimed requests or unmet needs, and must be standing at the NPC to
see them, so it cannot snipe a request from across the city. Decision D3.

**Fulfilment is a quest.** Accepting starts `nogame.quests.Delivery` (one at a time — §5 D4):
pick up at the nearest food shop (`ShopType` "Eat"), deliver to the requester's home. Completing it applies
the reward and the effect of the good; failing (deadline) or giving up applies its own change;
the quest data is saved, so it survives depopulation and save/load. The request pool entry is
not needed after acceptance: the quest carries everything.

**What "help" means after S3** (owner, 2026-11-28, and KAR-495): fulfilling a request applies
`help` (+0.4, the owner's "one help counts a lot"); the conversation-only line "I'll put in a
word for you" becomes a small `encourage` (about a chat's worth). Help becomes reachable
wherever an NPC needs something, and costs what the owner said it should: time, the trip, the
good.

**Player-answerable request types are data,** in a new `models/nogame.player-requests.json`
keyed by request type, so S5 can add types without code:

```json
{
  "food_delivery": {
    "kind": "low_stakes",
    "need": { "roles": ["worker", "drifter"], "properties": { "hunger": { "min": 0.45 } } },
    "quest": "delivery",
    "pickup": { "location": "social_venue", "shop_type": "Eat", "phrase": "the diner" },
    "deliver_to": "home",
    "deadline_game_minutes": 180,
    "on_success": { "interaction": "help", "requester_properties": { "hunger": -0.5 },
                    "favour": true },
    "on_fail":    { "interaction": "let_down" },
    "on_decline": null
  }
}
```

The need is checked by a small property check of its own, not by
`StoryletSelector.PassesPreconditions` (finding 17): the player meets NPCs anywhere and at any
hour, so the storylet's time, location and home-feasibility gates do not apply. The registry's
thresholds are tuned by the S3-a measurement. Trust-gated types add `"kind": "trust_gated"` and
a `"gate"` (§4.4).

## 3. Work packages and order

| WP | Content | Depends on |
|---|---|---|
| **S3-a** | Engine: `PlayerRequestRegistry` (the JSON above), `TaleManager.FindPlayerOffer(npcId, now)` / `AcceptPlayerOffer` / `CompletePlayerRequest` / `FailPlayerRequest`, `InteractionPool.PlayerClaimerId = −2`, the favour record and `FavourDirections` (§4.5), interaction ids `let_down` and `encourage` (§4.6), `EmittedAt` set at emission. Pure and testable without ECS. **Measurement gate** (§6): S3-b and S3-c start only when it passes. | — |
| **S3-b** | Conversation: props `npc.offer.type`, `npc.offer.pickup`, `player.has_delivery`; events `tale.request.accept` / `tale.request.decline`; the offer branch (§4.3). Desperate merchant: "put in a word" → `encourage`. | a (gate passed) |
| **S3-c** | Quest: `nogame.quests.Delivery` — `DeliveryQuestModule` holding the pending `PlayerRequest` (§4.2), `DeliveryQuestData` (persistable), creator, strategy with phases pickup → deliver (`ToLocation`, `OwnerQuestEntity`), deadline → `DeactivateQuest(isSuccess: false)` (first use of the failure path), **a new "give up" in the quest log** (Lua binding `giveUpQuest(id)` + an `<option>` next to Follow/Unfollow in `models/menu/menu.xml:104-112`, finding 15), save/load. | a (gate passed) |
| **S3-d** | Trust-gated requests: the gate (§4.4), the first trust-gated type, interaction id `refused_me`. | a, b, c, owner D2 |
| **S3-e** | Acceptance (§6), TALE panel and debug snapshot (`TaleManager.cs:155`, `TaleDebugSnapshot.cs:28`, `ui/Tale.cs:167,171`: −1 unclaimed, −2 claimed by the player, else an NPC id; open offers; the player's accepted request; favours), docs. | b, c (d for the trust-gated part) |

S3-b and S3-c can run in parallel after S3-a. New files go into `JoyceCode/JoyceCode.projitems`
(S3-a) and `nogameCode/nogameCode.projitems` (S3-b, S3-c) by hand; traces use `Dc.TaleSocial`.

## 4. Details

### 4.1 Finding an offer (S3-a)

- `FindPlayerOffer(npcId, now)` returns at most one offer: the first registry type, in file
  order, whose rule in §2 holds for this NPC. It runs on the logical thread under the cluster's
  pool lock, then `_loSocial` (S2b's lock order).
- "Has the need" = the NPC's role is in `need.roles` and its properties meet `need.properties`.
  An NPC **currently in a storylet that satisfies the need** (eating: a storylet whose
  postconditions lower hunger) does not offer — it is eating already. Checked through
  `TaleManager.GetCurrentStorylet(npcId)`; a postcondition lowers hunger if it is a negative
  delta (`"-0.55"`) or an assignment (`"=x"`) below the current value, parsed as
  `StoryletSelector.ApplyPostconditions` does (`StoryletSelector.cs:214-226`).
- "No NPC has claimed one from it" = no pool request of that type with this requester and a
  claimer ≠ player. A request claimed by an NPC within its timeout means a merchant is on the
  way.
- **Pickup:** `FindNearestOfType(home, "social_venue", "Eat")` on the requester's cluster's
  model (`GetSpatialModel(schedule.ClusterIndex)`), from its home (finding 14). Ties: list order
  (deterministic). Entries not reachable on foot are accepted — the player arrives by car. If
  none exists, no offer. The dialogue names it by the registry's `phrase` ("the diner");
  locations have no names.
- **Deliver to:** the requester's home (`HomeLocationId`). For a drifter that may be the street
  corner it sleeps at (`TalePopulationGenerator.cs:241-271` assigns drifters a `home` or a
  `street_segment`, and falls back to location 0); a test covers a street-corner home. Without a
  spatial model (`HomeLocationId < 0`) nothing is offered.
  Both positions are **captured at accept** (entry positions), because the spatial model is
  dropped with the cluster (`TaleManager.cs:507`); the quest targets static positions and does
  not chase a moving NPC (finding 9).

### 4.2 Accepting, completing, failing (S3-a, S3-c)

- **Accept** (logical thread): claim or emit the request with `ClaimerId = PlayerClaimerId`;
  `MarkPlayerContact(requester)` so its schedule survives depopulation (finding 13); return a
  `PlayerRequest` record (type, requester id, cluster, pickup location id and position, deliver
  location id and position, accepted at, deadline) that the quest stores.
- **Save and load:** the `DeliveryQuestModule`'s pending slot is not saved (the quest factory
  consumes it before its first `await`, so no autosave can slip in between). After a load, the creator rebuilds the
  `PlayerRequest` from `DeliveryQuestData` (requester id, request type, cluster, both
  positions, deadline, phase); completion and failure use only that.
- **Hand-off to the quest** (finding 18): the accept handler stores the `PlayerRequest` as the
  `DeliveryQuestModule`'s pending request on the logical thread, then awaits
  `TriggerQuest("nogame.quests.Delivery", true)` in a try/catch (no `async void` escape,
  CLAUDE.md); the quest factory takes the pending request and clears it.
- **Complete** (quest reaches the delivery point after the pickup): apply `on_success` —
  `ApplyPlayerInteraction(requester, "help")` (cooldown and repeat scale as S1, so a second
  delivery to the same NPC within 12 game hours counts less), the requester's property changes
  (hunger −0.5, clamped), the favour record; nobody's wealth changes (D5; no specific merchant
  is involved, and an unknown merchant's schedule is regenerated on depopulation anyway);
  emit `request_fulfilled` with source `PlayerClaimerId` (−2; −1 stays "abstract") if the pool
  entry still exists, so `GetPendingRequests` stops listing it.
- **Fail** (deadline): `ApplyPlayerInteraction(requester, "let_down")` (−0.05, fact
  `let_me_down`), no favour. **Give up** from the quest log (new, S3-c) counts as fail.
- **The requester need not be loaded.** Its schedule is kept by `MarkPlayerContact` (finding 13);
  if its cluster is unpopulated at completion, the effects go to the kept schedule and travel
  with it into the next populate and the save (`HasPlayerDeviation`, `TaleManager.cs:1015-1020`).
- **Decline** in conversation: `on_decline` — nothing for a low-stakes request (a stranger
  saying no is normal); the trust-gated rule is §4.4. A declined offer is not offered again by
  that NPC for `DeclineCooldownGameMinutes` (120), kept in memory like S1's cooldowns — a reload
  resets it, accepted as in S1.
- The requester's **effects apply whether it is materialised or not**: everything goes through
  its schedule.

### 4.3 The conversation (S3-b)

- `InjectNpcProps` calls `FindPlayerOffer` once and sets `npc.offer.type` (empty when none),
  `npc.offer.pickup` (the registry's phrase) and `player.has_delivery`; `ClearNpcProps` resets
  them.
- **Where the branch goes** (finding 8): in `tale.generic.json`, `tale.tag.routine.json` and
  `tale.role.merchant.json` right after `hostile`; in `tale.role.worker.json` and
  `tale.role.drifter.json`, which have no `hostile` node, as the first branch of `entry`. Not in
  `tale.lunch_break` (the NPC is eating). A test per script pins the routing.
- The line: "I'm starving and can't get away. Could you bring me something from {pickup}?" →
  **"I'll get it."**
  (`tale.request.accept`, then the quest starts) / **"Not now."** (`tale.request.decline`).
  Hidden while `player.has_delivery` (one delivery at a time) — which also covers the NPC the
  player is delivering to: it does not ask a second time.
- The handlers capture the npc id synchronously and hop with `QueueMainThreadAction`, as
  `tale.interact` does (S1-c), then call `AcceptPlayerOffer` and `TriggerQuest`.

### 4.4 Trust-gated requests (S3-d)

- **Gate:** `"gate": { "trust_min": 0.3 }` (the requester's `Trust[-1]`, i.e. "recognised"
  territory) **or** `"group_standing_min": 0.15` with the requester's group. A gated offer is
  hidden below the gate — the NPC does not ask someone it does not trust.
- **Refusal is remembered:** declining a trust-gated offer applies `refused_me` (−0.08, a new
  id in S3-d) and sets `player_fact.favour_refused` (§4.5) — the roadmap's "a completed or
  refused trust-gated request is recorded".
- **First type: owner decision D2.** The candidate is `help_request` (`ask_for_help`,
  `worker.json:90-107`: desperate workers and drifters, on the street). What the player does for
  it decides the quest kind.

### 4.5 The favour record (S3-a)

- **On the NPC, as facts** (saved with its Properties, finding 13; no new store):
  `player_fact.favour_done` (the player fulfilled a request for it, count),
  `player_fact.favour_refused` (the player refused a trust-gated request, count).
  `player_fact.favour_owed_player` — the player owes the NPC — is defined but has no writer in
  S3; S4/S5 add NPCs doing the player favours.
- **"Directions"** for the standing level: the number of distinct groups (an ungrouped NPC
  counts as its own) in the cluster holding `favour_done` or `favour_owed_player` — favours
  *owed*, as the roadmap's table says. Refusals act through trust only.
  `PlayerStandingLevel.Measure` fills `FavourDirections` from that (finding 12). "Entangled"
  still needs S4's rival groups.
- **Both sides as one record:** the player's side is the same facts read the other way (who
  owes whom); no separate ledger in `GameState` while every favour has an NPC end. This deviates
  from the roadmap's wording "recorded on both sides"; the roadmap update (§9) says so.

### 4.6 New interaction ids

`encourage` (+0.03, cooldown 60, repeat 0.25 — D1's values), `let_down` (−0.05, fact `let_me_down`) in S3-a;
`refused_me` (−0.08) in S3-d. Like `pay`, `bribe` and `drive` (`nogame.interactions.json:190-233`)
they have `conditionClassName: null` and only a `player` block, so NPC–NPC selection never picks
them and the bake does not read them; the bake-identity check pins it.

## 5. Decisions

- **D1 — Reward.** A fulfilled request applies `help` (+0.4, cooldown 720, repeat 0.3) —
  recommended, matching the owner's "one help counts a lot" and the rule that help costs time.
  "Put in a word" drops to `encourage` (+0.03, cooldown 60, repeat 0.25). *Owner to confirm.*
- **D2 — The first trust-gated request.** Candidates: (a) `help_request` answered by **giving
  the NPC a lift** to a location of its choice (time; reuses the taxi's driving phase, the
  passenger as today a cube), (b) `help_request` answered by **lending cash** (costs cash —
  against the owner's rule unless repaid), (c) defer trust-gated requests to S4, where group
  aims supply better ones (protection, taking sides). Recommended: (a). *Owner to choose.*
- **D3 — No waiting window** before the player may take a request (§2). Recommended.
- **D4 — One player request at a time** (`QuestFactory`'s one entity per quest id; a second
  offer is hidden while one runs). Recommended for S3; S5 may lift it.
- **D5 — Money.** Recommended: **none in S3** — the player neither pays for the food nor is
  paid, and no NPC's wealth changes. Paying the player belongs to the
  economy plan (KAR-485). *Owner to confirm*; the alternative is the requester paying a tip on
  delivery (`GameState.Cash`).
- **D6 — The NPC side is unchanged.** NPC claims still emit `request_fulfilled` at claim time
  (finding 4); S3 does not touch `TaleSocialRules`' NPC path, so the bake stays byte-identical.
  Only the `EmittedAt` fix touches shared code: `EmittedAt` is read only by the debug snapshot
  and the panel (`TaleManager.cs:155`, `ui/Tale.cs:169`), not logged and not touched by
  `bake/`, so the bake is unchanged; the bake-identity check pins it.

## 6. Verification

- **S3-a xUnit (no ECS):** offer from an open request; offer from a need without a request; no
  offer when an NPC claimed it, below the need, without a pickup location; accept claims or emits
  with the player as claimer and `ResolveDaily` skips it; complete applies `help`, hunger and
  `favour_done`, emits the signal with source −2, and a second completion within the cooldown
  counts less; the pickup is a `social_venue` of shop type "Eat", never a bar; a street-corner
  home; a requester whose cluster is not populated at completion;
  fail applies `let_down`; decline cooldown; favour directions by group; `EmittedAt` equals the
  emission time; **the bake reproduces byte for byte** (`ScenarioCompilerTests`, TALE suite).
- **Measurement gate (S3-a, Perf):** in Ufyo (`LiveSocialPerfTests._ufyo()` builds the
  cluster, model and population; `OneGameDayInUfyo` advances a day), at each daytime game hour
  (08–20), the share of NPCs that would offer the player something, and for 100 street points
  (`street_segment` locations, seeded draw) the number of **visible** offering NPCs within 100 m
  — in transit or at a `street_segment` (finding 15b); NPCs indoors cannot be talked to.
  **Gate: at least one visible offering NPC within 100 m of a street point in 80 % of the
  samples.** It is a `Perf`-tagged test (CI excludes Perf), run by hand; its numbers go into the
  status-log fragment. If it fails, tune `need.properties` (or add a second
  low-stakes type) before S3-b and S3-c start. The roadmap's 4–5 opportunities within 100 m
  (§8 Q4) is S5's target, not S3's; the number tells S5 how far it is.
- **S3-b:** the offer branch through `NarrationConditionEvaluator`'s prop seam (S1-e): offer
  props route to the offer node, `has_delivery` hides it, `hostile` wins over it.
- **S3-c:** TestRunner or xUnit for the quest data round trip (save → load → same phase,
  deadline, positions) and the deadline failure.
- **S3-e acceptance (the roadmap's done-when):** a **TestRunner script** (the path spans ECS
  and quests; xUnit cannot drive it) takes one `food_delivery` from the NPC's need to a quest
  and back into trust — need → offer → accept → pickup → delivery → the requester's trust,
  hunger and favour — with the engine part also covered in xUnit. CI does not run the TALE
  suite, so it runs locally before each PR. Plus the owner's in-game check: a hungry worker
  asks, the satnav leads to the diner and back (follow the delivery explicitly if a taxi is
  followed — satnav auto-follows only when nothing is, `SatnavService.cs:116-127`), the toast
  says "Finished", and the requester's group greets warmly afterwards (S1-e; S2's gossip
  spreads `helped_me` as hearsay).
- Gates: xUnit (CI filter), TALE suite, a bake byte-identical.

## 7. Risks

- **Reachability may still be low.** If few NPCs are hungry when the player is near, the offer
  stays rare; the S3-a measurement says so early, and the registry lets S3-e add a second
  low-stakes type (a lift, an errand) without code.
- **The routine tag shadows role scripts** (S1 finding 3): the offer branch goes into five
  scripts (generic, routine, merchant, worker, drifter); a test pins that each routes to it.
- **Save growth:** every requester becomes a known, saved NPC — one per accepted request, a
  small number.
- **Quest one-at-a-time** may feel restrictive next to the taxi; the taxi is a separate quest id,
  so a delivery and a taxi run can coexist.
- **Pickup at a shop with nobody there:** the pickup is a location, not a merchant in person;
  the story reads as "picked it up at the diner". Accepted.

## 8. Not in S3

NPCs approaching the player unasked (S5); the player owing NPCs (S4/S5); paying the player
(economy, KAR-485); goods as inventory items; requests between clusters (S6); changing how
NPCs claim requests from each other (D6).

## 9. Docs to update along the way

ARCHITECTURE (§7 player interactions: requests, favours), REFERENCE (`nogame.player-requests.json`,
`tale.request.*` events, the offer props), the conversation docs, QUEST_SYSTEM (the delivery
quest, first use of `isSuccess: false`, the give-up button), the roadmap's §S3 lines on the
waiting window (D3) and on "recorded on both sides" (one record read both ways, §4.5), and a
status-log fragment per package.

## 10. Review record

**Round 1 (Fable, 2026-11-28): YELLOW**, 12 findings, all worked in:
1. The player's claimer −1 collided with "abstract" and "unclaimed" → `PlayerClaimerId = −2` (finding 16).
2. "Has the need" bound to `order_food`'s hunger 0.6 was rarely reachable → own thresholds
   (0.45) and a measurement **gate** before S3-b/c (§6).
3. `{pickup_name}` had no source → a phrase in the registry (finding 14).
4. "A merchant's workplace" picked bars and arcades; the merchant's wealth could not be applied
   reliably → nearest "Eat" shop; no wealth effect (D5).
5. Give-up from the quest log did not exist → scoped in S3-c.
6. `refused_me`, `let_down` undefined; why new ids leave the bake alone → §4.6.
7. `PassesPreconditions` is private and checks more → the need has its own check (§2).
8. Worker and drifter scripts have no `hostile` node; `tale.lunch_break` → insertion points (§4.3).
9. Refusals counted as favour "directions" contradicted the roadmap → only owed favours (§4.5).
10. The quest hand-off channel was unnamed → `DeliveryQuestModule`'s pending request (§4.2).
11. The done-when test spans ECS → TestRunner script; satnav follow in the owner check (§6).
12. projitems (both), `Dc` category, in-memory cooldowns → §3, §4.2.

**Round 2 (Fable, 2026-11-28): YELLOW** — all 12 round-1 findings confirmed resolved in the
text; 7 new findings, all worked in:
1. Food shops are typed `social_venue`, so `FindNearestOfType(home, "shop", "Eat")` found
   nothing → `"social_venue"`; ties and unreachable entries stated (§4.1, finding 14).
2. The gate counted NPCs indoors, who cannot be talked to → visible NPCs only (§6, finding 15b).
3. "In a storylet that lowers hunger" is checkable through `GetCurrentStorylet` and its
   postconditions; parse deltas and `=` as `ApplyPostconditions` does (implementation note).
4. A drifter's home may be a street corner; there is always a home with a model → §4.1, test.
5. −2 checked against every reader of `ClaimerId`; the snapshot and panel change in S3-e; the
   completion signal uses −2 (§4.2).
6. Save/load rebuilds the `PlayerRequest` from `DeliveryQuestData`; the pending slot is not
   saved (§4.2).
7. Text: the 0.45 placement, `encourage` values aligned, the requester does not ask twice,
   stale "pickup NPC's wealth" and "four scripts" removed.

**Round 3 (Fable, 2026-11-28): GREEN** — all seven round-2 findings resolved in the text and
correct against the code; no contradiction with the code, the owner rules or the roadmap. Four
nits worked in: satnav follows only when nothing else is followed; the quest factory consumes the
pending slot before its first `await`; findings renumbered (15b); the give-up option's place in
`models/menu/menu.xml:104-112`.
