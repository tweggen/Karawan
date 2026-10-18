# From the Shipped TALE to Emergent Stories — Roadmap

> **Status:** proposed, for the owner's review · **Written:** 2026-10-18 (KAR-431)
> **Goal:** [../../docs/CONCEPT.md](../../docs/CONCEPT.md) · **Starting point:** [../../docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md)

The concept's goal: NPCs evolve through their interactions, form factions,
and eventually draw the player into storylines — without TALE generating
storylines explicitly. This plan orders the missing pieces (CONCEPT §5) into
stages. Each stage is useful on its own, builds only on what the previous
stages and the shipped code provide, and ends in something measurable.

The stages are called **S1…S6** on purpose: "Phase" letters are already
taken twice (routing Phase D, TALE-SOCIAL Phase D).

Nothing here is decided yet; §8 lists the questions for the owner. Each
stage gets its own plan and tickets once it is accepted.

## S0 — Ground to stand on (prerequisites)

You can't tune a society you can't measure, and the test suite currently
can't tell when social behaviour breaks.

- **KAR-433** — 49 TALE scripts assert nothing; fix them and make an empty
  script fail.
- **KAR-434** — wire `DepopulateCluster`, so lifecycles (and the snapshots
  everything below relies on) are real.
- **Social metrics in the Testbed:** groups per cluster, sizes, churn per
  day, trust-graph density, inter-group trust, share of NPCs who ever met the
  player (S1+). `SimMetrics` today hard-codes two rates to 1.0 (KAR-436).
- **Baseline:** record these metrics for a few clusters over 30/90 simulated
  days before S1 changes anything.

- **Bring the DES-only mechanics into the live game.** Requests and claims,
  conditional postconditions (`postconditions_if`, `target_…`) and
  interrupts run only in `DesSimulation`; the live `TaleManager.AdvanceNpc`
  applies plain postconditions and group verbs. So the escalation chains
  the factions are meant to drive (`demand_protection` → …) never happen in
  the game. Port them to `TaleManager` (sharing code with DES, as
  `GroupActions` does), and decide whether the unloaded
  `models/tale/interactions/` files should ship (KAR-436).

*Done when:* the TALE suite fails on a real regression, a Testbed run
prints the social metrics, and an escalation chain observed in DES also
plays out in the live game.

## S1 — The player in the social graph

Today the player's only relationship is `Trust[-1]` per NPC, raised by 0.02
whenever a conversation ends (`TaleModule.cs:767`).

- **Player interactions** use the same vocabulary as NPCs: a conversation
  choice (or an action: help, threaten, pay, refuse) applies an interaction
  type from `nogame.interactions.json` to `Trust[-1]`, instead of a flat
  +0.02. Narration gets a function to apply one (`tale.interact("help")`).
- **Group standing** = an aggregate of the members' `Trust[-1]` (mean,
  weighted by the member's standing in the group). Exposed to dialogue as
  `npc.group_standing`, so faction members treat the player by the group's
  opinion, not only their own.
- `Trust[-1]` is already snapshotted and saved (E6), so nothing new is
  needed for persistence.

*Done when:* helping one member of a group changes how the group's other
members greet the player, verified in a test and in the game.

## S2 — Consequences travel (gossip)

- During NPC–NPC encounters (`RuntimeSocialEvolver`, and DES for tests),
  opinions of the player diffuse: each side's `Trust[-1]` moves a little
  towards the other's, weighted by their mutual trust. Strong opinions
  travel further than weak ones.
- `player_fact.*` entries (`tale.npc.remember`) spread the same way with a
  probability, so "the player tipped off the patrol" can reach the
  syndicate.
- Bounded and deterministic, on the evolver's own RNG; never through the
  storylet RNG.

*Done when:* an NPC the player never met has a non-neutral opinion after
the player helped or harmed someone in that NPC's circle, measured in DES.

## S3 — NPCs ask the player

The request/claim/signal pool (Phase 3) is NPC-only — and, until S0 brings
it into the live game, DES-only; the player never takes part.

- **The player as a claimer:** a request no NPC claims within a time window,
  whose requester trusts the player enough (or has no one else), is offered
  to the player — first as a conversation option when the player talks to
  the requester, later (S5) as an approach.
- **Fulfilment through the existing quest system:** the claimed request
  becomes a quest (`QuestFactory`, satnav follow). Completion emits the
  request's signal and applies the interaction's trust change; refusing or
  failing applies its own change. Start with one request type end to end
  (`ask_for_help` / `provide_help` or `order_food` / `deliver_food`).
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
  relations, using the existing interrupt and escalation machinery.

*Done when:* DES runs show measurable inter-group tension and its
consequences (members leaving, rivalries, protection), with S0 metrics.

## S5 — Surfacing what matters

TALE still authors no story; S5 decides **which** of the society's
situations are worth showing the player and how.

- **Hooks:** detectors over TALE state for situations involving people the
  player knows — an escalation against a trusted NPC, a rivalry next to the
  player's usual places, an unclaimed request from a friend.
- **Delivery:** an NPC approaches the player (a conversation started by the
  NPC), a rumour line in other NPCs' dialogue, or an S3 request offered
  proactively. Rate-limited, so the city doesn't nag.
- The Phase C conversation system and the followed-quest machinery deliver
  them; nothing new is needed in the renderer.

*Done when:* in a normal play session, unscripted hooks come up at a tuned
rate (target set by the owner), and each one can be traced to TALE state.

## S6 — Beyond one cluster

- Standing per cluster, decaying over time; arriving in a new city starts
  the player as a stranger there (NPC_STORIES "geography as pacing").
- Groups and their opinion of the player persist across visits (snapshots
  already do this for NPCs).

## 7. What this plan deliberately leaves out

- No story graph, arc library or generated plot (CONCEPT §2).
- No new rendering or UI beyond dialogue lines and the existing quest log.
- No change to how the city is generated.

## 8. Questions for the owner

1. Is the order right? S1–S3 make the player part of the society before
   groups get aims (S4); the reverse would make the society richer before
   the player can touch it.
2. Player actions (S1): only through conversation choices at first, or also
   through world actions (paying, hitting, driving someone)?
3. How visible should standing be: only in what NPCs say, or also shown
   (a faction view in the pause menu)?
4. S5 rate: how often may the city approach the player?
5. Should NPC_STORIES' social-capital tiers (anonymous … entangled) come back
   as a reading of group standing, or stay out?
