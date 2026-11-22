### 2026-11-22 — KAR-494: the TALE panel counts NPCs per role

- The panel's header lists the shown cluster's NPCs per role, most first, e.g. "Roles: worker 101, merchant 47, …". You can see whether a city has drifters or merchants without looking for them with the overlay. Asked for by the owner after KAR-492.
- `TaleDebugSnapshot.RoleCounts`, filled by `TaleDebugSnapshotBuilder` in its existing pass over the cluster's NPCs.
- **Tests:** `TaleDebugSnapshotTests` (counts sum to the NPCs, match per role, sorted). xUnit 2308 pass; the 26 failures are the known KAR-461 drift. TALE 133/133.
