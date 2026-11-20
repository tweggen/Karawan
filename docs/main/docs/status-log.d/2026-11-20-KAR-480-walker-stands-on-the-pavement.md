### 2026-11-20 — KAR-480: the player on foot stands on the pavement

- **Report (Windows playtest):** the walking player visibly floated above the pavement, in many places, while walking NPCs stood on it.
- **Measured, eight shipped terrain cities, mid-pavement points (25 416):** the walker's floor, `Loader.GetWalkingHeightAt` (terrain + street + sidewalk offsets), against the pavement as drawn (`BuildingFooting.PavementHeightAt`, the top of the block floor's collider):
  - median 0.00, p10 −2.51, p90 +2.53 m, worst +10.56 m;
  - above by more than 0.3 m at 42.7 %, by more than 1 m at 28.1 %.
  - Walking NPCs stand on the pavement (`GoToStrategyPart._walkingHeightAt`, nav junction ground). This is the same terrain-vs-built split as the ship before KAR-472.
- **Second cause:** a walker standing still was never lowered. `WalkController`'s floor ray cast looks along the motion and only runs while moving, so a player stepping out of the ship (a metre above the road) stood in mid air until it walked.
- **Fix:**
  - The walker's floor is the built surface under it: `HoverSurfaceProbe.SurfaceBelow` finds the pavement, road or deck collider, and the new `HoverSurfaceProbe.WalkingFloorHeight` falls back to the terrain height only over open ground.
  - A grounded walker above that floor steps down to it at most 20 m/s, the existing step-down rate.
- **Flat city:** a pavement's top is exactly the old walking height. On a road the floor plane is the drawn road, so the walker stands on it rather than 0.15 m above it.
- **Tests:**
  - `WalkingFloorTests` (5): the rule, the flat-city arithmetic, the measurement above as a recorded regression, and a scan of the walker.
  - xUnit 2239 pass; the 26 failures are the known KAR-461 drift. TALE 133/133.
- **Pending in game:** level with walking NPCs in cuttings and on slopes, landing on the pavement after leaving the ship, and jumping still works.
