### 2026-11-20 — KAR-481: TALE NPCs no longer walk as one figure (stage 1)

- **Report (playtest):** many walking TALE NPCs occupied exactly the same position and moved in lockstep; talking to one "forked it off".
- **Diagnosis** (measured on Yelukhdidru and Ufyo; full report in the KAR-481 log):
  - most NPCs share one home and few workplaces, so they leave the same door (stage 2, KAR-482);
  - a building location has a single standing point;
  - every NPC walked at exactly 4.5 km/h, 1.5 m off the kerb, on deterministic routes;
  - the warm-up advances every NPC at one game start time, so all NPCs in transit had one `TransitStart` (82–97 of about 100–140 in Yelukhdidru also shared origin and destination) and materialised at the same point of the same route;
  - NPCs that arrived together waited identical activity durations (all over two game hours clamp to 300 s) and left in one frame.
- **Change** — `engine.tale.NpcWalkStyle`, derived from the NPC id alone (SplitMix64; nothing saved, no other random draw moves):
  - distance onto the pavement 1.0–2.0 m (was a fixed 1.5 m), through `RoutingPreferences.SidewalkOffset` → `StreetRouteBuilder` → new `PedestrianRoute.WaypointAt`; `WaypointFor` and every other walker keep 1.5 m;
  - walking speed ±15 %;
  - activity wait ±15 s, added after the 300 s clamp so the clamped ones part too;
  - spawn-in-transit phase ±15 m along the route (`TaleSpawnOperator`).
- **Found on the way:** `builtin.tools.RandomSource(string)` seeds with a weighted character sum and collides heavily: 279 NPC ids gave 208 distinct styles. Not changed (it seeds the whole world); `NpcWalkStyle` hashes the id itself.
- **Tests:**
  - `NpcWalkStyleTests` (7): determinism, ranges, 279 distinct styles, under 5 % of pairs in a 50-NPC group closer than a body, the jitter surviving the clamp, the phase staying on the route, the waypoint offset, and the call sites scanned.
  - `NavJunctionHeightTests` scan repointed to `WaypointAt`.
  - xUnit 2256 pass; the 26 failures are the known KAR-461 drift. TALE 133/133.
- **Not solved here:** NPCs converging on one door from different streets still meet there; NPCs standing at a building still share its single standing point (stage 2 and building entry points); no NPC avoids another standing in its way.
