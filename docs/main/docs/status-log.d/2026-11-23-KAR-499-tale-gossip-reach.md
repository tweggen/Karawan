### 2026-11-23 — KAR-499 (TALE S2-d): gossip reach measured live in Ufyo

- **Setup:** `GossipReachPerfTests` (Perf), run as the game runs it:
  - Ufyo, 4095 NPCs, shipped storylets; a baked `medium` scenario applied (192 NPCs — "large" is disabled in development builds, so only 192 of 4095 NPCs have baked ties ≥ 0.3);
  - the background tick every game minute, the social tick every 10 game minutes, encounters drained.
  - One act on the matched NPC with the most strong ties: an authority in a group of 10, 122 ties ≥ 0.5. The victim is the only first-hand source (witnesses need positions and are left out).
- **Results:**

  | hours | punch: heard (by generation) | help: heard | greeting from hearsay | standing |
  |---|---|---|---|---|
  | 6, 12 | 0 | 0 | 0 | anonymous |
  | 23 | 2 (gen 1: 2) | 2 (gen 1) | 0 | anonymous |
  | 48 | 3 (gen 1: 3) | 3 (gen 1) | 1 | anonymous |
  | 72 | 4 (gen 1: 3, gen 2: 1) | 3 (gen 1) | 3 | recognised |

  Nobody besides the victim came to know the player.
- **The shape meets the owner's target:** generation 1 within the first day; generation 2 later, for harm only.
- **The reach is small.** Each telling needs a live encounter, and the social tick gives about 7300 encounters a day over 4095 NPCs, about 3–4 per NPC and mostly not with its ties. In the DES (KAR-497) home and work mates meet constantly: 11 within a day in the start city.
- **Left for the owner:** whether that is right, or whether close ties should hear without a random encounter. Nothing is tuned yet.
