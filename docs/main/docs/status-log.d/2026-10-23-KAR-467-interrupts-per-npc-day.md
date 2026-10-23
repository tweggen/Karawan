### 2026-10-23 — KAR-467: Testbed interrupts are counted per NPC-day

- **Finding:** `SimMetrics`' `interrupts_per_day.mean` was the whole cluster's daily count, not a per-NPC rate. The 1–3 target in `TESTBED.md` and `PHASE_3.md` is per NPC per day, so every real Testbed run (500 NPCs, ~28 interrupts a day) failed `testbed_targets.json`'s maximum of 3. The W9 baseline (KAR-452) had reported the cluster count as "per NPC-day".
- **Fix:** the statistics are now per NPC-day; `cluster_mean` keeps the cluster count. Measured, 30 days, 500 NPCs: medium 0.057, large 0.064 per NPC-day — about twenty times below the designed 1–3, noted for S2b. The targets file is unchanged (min 0.0, max 3.0).
- `Testbed --cluster medium --days 30 --quiet` and `--cluster large` exit 0. `--cluster small` picks a cluster with no homes and fails nearly every target by construction.
- Docs: `TESTBED.md`, `PHASE_0C.md`, `TALE-S0-GROUNDWORK.md` §12 (W9 baseline corrected).
