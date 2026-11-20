### 2026-11-20 — KAR-484: the intercity groove is centred on its line, and the highway assist aims at its middle

- **Report:** the highway assist's target trajectory (KAR-483) ran on one side of the groove, not in its middle.
- **Measured** over the shipped world's 114 lines, cross-sections every 10 m: the carved groove's centre was off the station axis by 8.6 m median, 18.6 m p90, **35.9 m** worst. The carved flat is about 60 m wide.
- **Cause 1:** `IntercityTrackElevationOperator` placed segment sample `tex` at `extent · tex / nHoriz`. A segment from `elevation.Cache` has `GroundResolution + 1` samples spanning its extent, so the spacing is `extent / (nHoriz − 1)`, as `ClusterConformElevationOperator` already documented. Every sample was taken to be up to one grid step (19 m) short of where it is, restarting at every fragment, so the band was carved in a sawtooth beside the axis.
  - `ElevationSegment` now says where its samples are (`StepX/Z`, `SampleX/Z`).
  - The operator carves by `IntercityCorridor.IsInCarvedBand`: within two grid steps of the station axis, the same rule the corridor assumes.
  - After: 3.4 m median, 8.3 m p90, 12.1 m worst, signed median 0.07 m — the 20 m grid itself.
- **Cause 2:** KAR-483 aimed the assist 20 m right of the axis (keep-right), on the edge of a ~±30 m flat. The corridor carries no traffic, so `IntercityCorridor.LaneOffset` is now 0: the middle of the groove.
- **Not changed:** `ClusterBaseElevationOperator` has the same divisor; it writes a whole city rectangle, so the error is a ragged edge of one sample. Left alone unless measured to matter.
- **Terrain moves:** every intercity groove now lies where its line is. Nothing caches elevation across runs, so no `DbVersion` bump.
- **Tests:**
  - `IntercityGrooveTests` (3): segment sample positions; the shipped grooves measured through the real `ElevationSegment` and the shared band rule (median < 5 m, worst ≤ one grid step, no lean, the axis always inside the groove); the operator scanned.
  - `IntercityCorridorTests` moved to the centred lane; the hands-off closed loop settles on the axis with 0.00 m error.
  - xUnit 2275 pass; the 26 failures are the known KAR-461 drift. TALE 133/133.
