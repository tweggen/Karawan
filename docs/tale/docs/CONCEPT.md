# TALE Concept — Stories That Emerge From a Living Society

> **Status:** concept (the goal TALE is built towards) · **Last updated:** 2026-10-18 (KAR-431)
> **Shipped design:** [ARCHITECTURE.md](ARCHITECTURE.md) · **Road from here to there:** [../plans/proposed/TALE-EMERGENT-STORIES.md](../plans/proposed/TALE-EMERGENT-STORIES.md)
> **Earlier concept (March 2026, not built):** [../archive/CONCEPT_2026-03.md](../archive/CONCEPT_2026-03.md)

This document states what TALE is for. It does not describe the code; for
that, read ARCHITECTURE.md. The roadmap plan says how to get from the code to
this goal.

## 1. The goal

The cities of Silicon Desert are full of people who live their own lives.
They work, eat, go out and meet each other. Over days and weeks, trust and
grudges build up between them. Out of those relationships, groups form: a
crew of drifters, a circle of traders, the patrol that works the same
streets. Groups want things and get in each other's way.

**Stories are what this society does when the player looks at it.** A gang
leaning on a shopkeeper, a trader who needs someone to carry goods past that
gang, a patrol officer who would like to know who did it: none of these is
written as a storyline. Each follows from who the people are, whom they
trust, and what they lack.

The player starts as a stranger. By talking, helping, refusing and taking
sides, the player becomes part of the web of relationships. Eventually the
society starts to involve the player on its own: people come with requests,
groups take notice, and the consequences of earlier choices come back.

## 2. What TALE deliberately does not do

The March 2026 concept generated stories explicitly: a world state graph,
a library of emotional arcs, motifs, rarity tiers, graph and arc rewriting,
fork and join of parallel arcs. **TALE does not generate storylines.** There
is no story graph, no arc to complete and no author in the loop at runtime.

What TALE authors are small, local pieces of behaviour — **storylets**: "go
to work", "demand protection money", "ask for help". Each has preconditions
on one NPC's state and postconditions that change it. Every larger shape the
player perceives as a story arises from many such pieces meeting in one
society.

## 3. Principles

1. **Society first, story second.** NPC state (needs, morality, fear,
   wealth), relationships (trust) and groups are the substance. A story is a
   pattern in that substance, not a separate object.
2. **Local rules, global consequences.** Storylets and interactions only see
   one or two NPCs. Factions, feuds and dependencies come from aggregation
   (community detection, runtime evolution), never from a script that names
   them.
3. **The player is a member, not a protagonist.** The player has
   relationships like any NPC (`Trust[-1]` today). Involvement grows from
   those relationships; nobody hands the player a main quest.
4. **Involvement comes from need.** An NPC turns to the player when the
   player is the best available means to an end: trusted enough, able to do
   what others won't, or already involved.
5. **Consequences persist and travel.** What the player does changes trust,
   group membership and NPC state, survives saves and leaving the cluster,
   and reaches people the player never met — through their relationships.
6. **Everything stays deterministic and cheap.** Seeds decide populations;
   social evolution has its own RNG; most NPCs are abstract schedules that
   should advance cheaply in the background, and only those near the player
   are elaborated. (Today an abstract schedule advances only during the
   warm-up at population; the roadmap's S0 adds the background tick.)

## 4. How a story emerges — one example

1. Encounters at a bar raise trust among a few drifters; re-detection turns
   them into a group, "The Rusted Syndicate", with the bar as hangout.
2. A desperate member's storylets escalate: `demand_protection` against a
   merchant raises the merchant's fear.
3. The merchant needs help. Among the merchant's contacts, the player is the
   one with enough trust (or the only one who isn't afraid), so the merchant
   asks the player.
4. The player helps, refuses, or tells the patrol. Each choice changes trust
   with the merchant, the syndicate and the patrol, and each group's members
   treat the player accordingly.
5. Days later, a syndicate member the player has never met is cold, while a
   patrol officer offers a favour. Nobody wrote this sequence; it followed
   from the relationships.

## 5. Where TALE stands (October 2026)

| Ingredient | State |
|---|---|
| NPCs with needs, roles and daily routines | **shipped** — schedules and storylets per role (Phases 0–1). The five Phase 8 occupations have content but are never assigned (KAR-436) |
| NPC–NPC encounters changing trust | **shipped**, live and in DES — data-driven interaction types (Phases 1, 3, E3) |
| Factions forming and evolving | **shipped**, live — community detection, runtime evolver, group verbs, save-game (Phases D, E) |
| Player talks to NPCs; dialogue reflects state and faction | **shipped** — Phase C conversations; faction-flavoured lines through the `npc.group_*` props (E5) |
| Requests between NPCs, escalation chains, interrupts | **DES only** — request/claim/signal pool, conditional postconditions and Nest/Replace run in `DesSimulation` (bake, tests, Testbed); the live game's `TaleManager` applies plain postconditions and group verbs only |
| Player relationships | **minimal** — `Trust[-1]` per NPC, starting at 0.5 and +0.02 per conversation; no group-level standing. It survives in play only because the code path meant to forget such NPCs never runs (roadmap S0) |
| Society away from the player | **trust only** — encounters and re-detection run for the whole cluster, but abstract schedules don't advance after the warm-up, so they meet at stale locations |
| NPCs involving the player | **missing** — the player never joins the interaction pool; NPCs never ask |
| Groups acting as groups (goals, conflicts between groups) | **missing** — groups exist and gather, but have no aims |
| Consequences travelling through relationships | **missing** — no gossip, no reputation within a group or cluster |
| Recognising and surfacing an emerging story | **missing** — no quest arises from TALE state |

The roadmap plan takes the DES-only and missing rows in order.

## 6. Relation to other documents

- [ARCHITECTURE.md](ARCHITECTURE.md) — what is built, class by class.
- [REFERENCE.md](REFERENCE.md) — storylet format, roles, tiers, persistence rules.
- [design/NPC_STORIES.md](design/NPC_STORIES.md) — the earlier player-facing
  vision (social capital, player verbs, geography as pacing). Its player
  verbs (overhear, inform, assist, mediate, introduce, refuse) remain good
  candidates for what the player can do; its story-generation parts are
  superseded by §2.
- [phases/PHASE_E_SOCIAL.md](phases/PHASE_E_SOCIAL.md) — the living factions
  this concept builds on.
