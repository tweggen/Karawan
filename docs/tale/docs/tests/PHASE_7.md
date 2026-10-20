# Phase 7 Test Scripts — Spatial Grounding & Navigation

> **Status:** removed · **Changed:** 2026-10-20 (KAR-433, TALE S0 W1)

`models/tests/tale/phase7-spatial/` held 102 generated placeholders: each had a title and three
prose "steps" (`setup`, `populate`, `verify`) and nothing a runner could execute, and no test
tier ran them. They are deleted; `ScriptRunner` would now reject such a file anyway.

Their topics were SpatialModel extraction, location type assignment, entry positions, travel
times, building occupancy and indoor activities, and fragment-accurate position tracking. What
of that is tested lives in xUnit (`tests/JoyceCode.Tests/engine/tale/`), e.g.
`StreetEntryPositionTests` (entry positions on the pavement) and `AdvanceNpcDeterminismTests`
(positions per NPC). Design: `../phases/PHASE_7.md`.
