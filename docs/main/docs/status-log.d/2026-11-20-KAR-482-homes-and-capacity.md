### 2026-11-20 — KAR-482: a city's NPCs live in many buildings (stage 2 of the stacking fix)

- **Root of the largest NPC stacks** (from the KAR-481 analysis): `SpatialModel` gave a building with a shop no other use, gave every building one use, and gave a building location one standing point. Assignment ignored `Location.Capacity`, which nothing read. Yelukhdidru (28 buildings, 11 with shops after KAR-479) had **one home for 279 NPCs** (268 lived there, up to 156 on one exact spot); Ufyo had 9 homes for 4095 (up to 452 in one).
- **Change:**
  - **Uses survive shops:** a building's home/office/warehouse locations no longer depend on whether it has shops; shops take the ground floor.
  - **One location per use:** a residential+office building is a home and an office.
  - **Mixed use:** an office building of 3+ storeys also houses people (`SpatialModel.MixedUseStoreys`). The residential role is otherwise only given near the centre of the Living field.
  - **Capacity in people:** each use gets its share of the building's storeys at 4 (home), 8 (office) or 2 (warehouse) people per storey; shops and venues 8.
  - **Capacity-weighted assignment:** `TalePopulationGenerator` picks a candidate with probability proportional to its capacity. Still one draw of the NPC's own seed, so NPCs stay independent (a skipped index changes nobody else) and nothing drawn after it moves. Occupancy follows capacity on average; it is not a hard cap.
  - **Standing points:** building uses stand at the building's corners and wall middles (`EntryCandidates`), as junctions already stood at their corners.
  - **Test seam:** `SpatialModel.ExtractFrom` gained an internal overload taking the stores, so the shipped cities can be measured.
- **Measured, after** (before in brackets):
  - Yelukhdidru: 24 homes [1], at most 42 NPCs in one [268], 91 workplaces in use [15], largest exact stack 8 [108–156].
  - Ufyo: 329 homes [9], at most 100 in one [452], largest stack 19.
- **Not measured:** `SocialMetrics` before and after. The bake match ratio is unaffected by construction (synthetic bake, pairing by count).
- **Saves:** location ids shift, so a saved deviated NPC may name a different building after loading. No migration, per the TALE no-migration rule.
- **Tests:**
  - `TalePopulationDistributionTests` (6): homes, NPCs per home and largest stack on Yelukhdidru and Ufyo; assignment following capacity; building standing points; skipping NPCs changing nobody else.
  - xUnit 2255 pass; the 26 failures are the known KAR-461 drift. TALE 133/133.
