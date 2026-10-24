### 2026-10-24 — The TaleSocial debug category gets its switch

- `Dc.TaleSocial` had no `debug.category.talesocial` entry in `models/nogame.properties.json`, so `DebugFilter` could never enable it: every `Trace(_dc, …)` in `RuntimeSocialEvolver` (encounter ticks, re-detection), `PlayerInteractions` and `PlayerActionReporter` (S1-b, S1-d: assaults, run-overs, witnesses) was lost. Found while writing the S1 manual test story.
- Added the entry (default `false`) and `DebugCategoryPropertiesTests`, which fails when any `Dc` value lacks its switch (checked: it fails without the new entry).
