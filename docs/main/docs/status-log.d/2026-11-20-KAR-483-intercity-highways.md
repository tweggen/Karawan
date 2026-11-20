### 2026-11-20 — KAR-483: the intercity corridors are highways

- **Owner wish:** in the grooves between the cities the player's ship should be three times as fast, by supporting its acceleration, and should help the player stay in lane.
- **What a corridor is:** the band `IntercityTrackElevationOperator` flattens to one track height, within `2 · FragmentSize / GroundResolution` = 40 m either side of the station axis. Nothing is drawn there and there are no lanes; what an intercity line IS stays ledger item (e).
- **Measured first:** the ship accelerates at `LinearThrust · frontMotion/256 − LinearDamping · v`, so at full throttle it settles at 15 · 255/256 / 0.6 ≈ 24.9 m/s (90 km/h); the 220 m/s limiter is never reached. So the boost is thrust: three times the acceleration and three times the cruising speed, ~75 m/s (270 km/h), with damping and the limiter unchanged.
- **Change** — `engine.world.IntercityCorridor` (pure, engine-only):
  - `TryLocate`: between the stations, within the band, no more than 15 m above the track; the nearest axis wins.
  - `TargetBoost`: up to 3× over 150 m from each station. `NextBoost` rises with that ramp and runs out at 0.5/s once the ship leaves (3× → 1× in 4 s), so leaving does not brake like a wall.
  - `AssistTurn`: steers toward a point ahead on the lane centre, 20 m right of the axis in the direction of travel. At most 0.6 rad/s (the player turns ~2.1). It scales with how little the player steers and does nothing below 5 m/s or across the corridor. It steers rather than pushes sideways, because `HoverController` removes velocity off the ship's nose.
  - `HoverController` multiplies `LinearThrust` by the boost and adds the assist turn. `PlayerVehicleSpeed.Boost` publishes the boost, and the speed display turns blue with a gauge of how much boost is on.
- **Tests:**
  - `IntercityCorridorTests` (10): band width; membership; the shipped world's 114 corridors (each line's middle in its own corridor, no city centre in any); the boost ramp; the 4 s run-out; assist direction, yielding and bound; a closed loop where a hands-off ship released in the wrong half at 75 m/s settles to 0.00 m lane error without leaving the band; and the controller wiring scanned.
  - xUnit 2272 pass; the 26 failures are the known KAR-461 drift. TALE 133/133.
- **Not run in game** (no display here). To check:
  - entering a corridor pulls to ~270 km/h;
  - leaving eases off over a few seconds;
  - hands-off the ship holds the right-hand lane, and steering out works;
  - the camera and fragment loading keep up at 75 m/s.
- **Out of scope:** highway visuals (markings, a drawn road) until item (e) is decided.
