### KAR-501: a baker change re-bakes, not only an input change (2026-11-25)

**Why.** Chushi skipped a bake whenever the output was newer than its sources. The baker's
own code was never part of that decision, and `nogame/generated/` survives clean builds, so a
file baked by an old baker looked current forever. Measured on Linux: re-baking the files from
2026-08-20 changed the decoded content of 13 of 14 `ac-` and 3 of 13 `mo-` files. Found while
chasing the invisible walking player on one Intel notebook (KAR-500).

**What.**
- `JoyceCode/builtin/baking/BakeStamps.cs`: `BakeVersions` (Model/Animation/Scenario = 1),
  `BakerIdentity.HashDirectory` (SHA-256 over the published Chushi directory, without
  `.pdb`/`.xml`), `BakeManifest` (`nogame/generated/bake-manifest.json`, atomic save),
  `BakeDecision.Of`.
- Chushi: the three up-to-date checks now return *why* a file is stale; each file is baked
  if stale **or** its stamp differs. Stamps of files about to be baked are removed and saved
  before any bake starts; finished bakes record theirs, and the manifest is saved in a
  `finally`, so one failed scenario does not cost the others their stamps.
- Scenarios carry no baker hash (an hour per full bake): `BakeVersions.Scenario` is bumped by
  hand; version 1 adopts existing unstamped scenario bakes.
- Log: `Baker identity: <hash> (from <dir>)` and one `baking`/`skipping … - <reason>` line per file.

**Measured.** First build after the change: 27 model/animation bakes ("no baker stamp"),
13 scenarios adopted. Second build: all skipped. A faked foreign stamp on one model re-bakes
exactly that model. Republishing unchanged Chushi keeps the hash (deterministic builds).

**Also found.** Two consecutive bakes differ in file bytes (17 of 27) with identical decoded
content: the LZ4 output is not byte-stable. Filed as KAR-502; never compare bakes by size.
