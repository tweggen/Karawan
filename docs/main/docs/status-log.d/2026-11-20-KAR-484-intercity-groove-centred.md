### 2026-11-20 — KAR-484: the intercity groove is centred on its line, and the highway assist aims at its middle

- **Report:** the highway assist's target trajectory (KAR-483) ran on one side of the groove, not in its middle.
- **Measured** over the shipped world's 114 lines, cross-sections every 10 m: the carved groove's centre was off the station axis by 8.6 m median, 18.6 m p90, **35.9 m** worst. The carved flat is about 60 m wide.
- **Cause 1:** `IntercityTrackElevationOperator` placed segment sample `tex` at `extent · tex / nHoriz`. A segment from `elevation.Cache` has `GroundResolution + 1` samples spanning its extent, so the spacing is `extent / (nHoriz − 1)`, as `ClusterConformElevationOperator` already documented. Every sample was taken to be up to one grid step (19 m) short of where it is, restarting at every fragment, so the band was carved in a sawtooth beside the axis.
  - `ElevationSegment` now says where its samples are (`StepX/Z`, `SampleX/Z`).
  - The operator carves by `IntercityCorridor.IsInCarvedBand`: within two grid steps of the station axis, the same rule the corridor assumes.
  - After: 3.4 m median, 8.3 m p90, 12.1 m worst, signed median 0.07 m — the 20 m grid itself.
- **Cause 2:** KAR-483 aimed the assist 20 m right of the axis (keep-right), on the edge of a ~±30 m flat. The corridor carries no traffic, so `IntercityCorridor.LaneOffset` is now 0: the middle of the groove.
- **Then widened (owner):** the terrain between samples is interpolated over two triangles per cell (`CacheEntry.GetElevationPixelAt`), so a point is at the track's height only where all three corners of its triangle were flattened. That is the carved band less up to a cell diagonal (28.3 m).
  - Measured over the shipped lines with the corrected positions:
    - carved 2 steps (40 m) either side: the axis is always flat, but the flat floor is as narrow as **12 m** beside it (p01 14.5);
    - 2.5 steps: 21.5 m;
    - 3 steps: 31.5 m.
  - `IntercityCorridor.CarveHalfWidth` is now 3 steps (60 m). The corridor (`HalfWidth`) is the floor guaranteed flat, 60 − 20·√2 ≈ 31.7 m either side.
- **Uncarved fragments:** the operator ran only on fragments whose border the line's own ±2.5 m edges cross, inside the stations' bounding box (which has no depth across an axis-aligned line). So where the axis passed within a band's width of a fragment border, the neighbour's share was never carved: 179 samples on 24 lines at 40 m, 429 on 39 at 60 m. It now asks `IntercityCorridor.BandTouches`, with the box grown by the band.
 `ClusterBaseElevationOperator` has the same divisor; it writes a whole city rectangle, so the error is a ragged edge of one sample. Left alone unless measured to matter.
- **Terrain moves:** every intercity groove now lies where its line is. Nothing caches elevation across runs, so no `DbVersion` bump.
- **Tests:**
  - `IntercityGrooveTests` (5): segment sample positions; the shipped grooves (carved in the fragments `BandTouches` selects, at the segment's positions, by the shared band rule) centred on their axes; every point within `HalfWidth` of every line on a fully flattened triangle (at least 31.5 m measured); all 257 369 band samples between the stations carved; the operator scanned.
  - `IntercityCorridorTests` moved to the centred lane; the hands-off closed loop settles on the axis with 0.00 m error.
  - xUnit 2275 pass; the 26 failures are the known KAR-461 drift. TALE 133/133.
