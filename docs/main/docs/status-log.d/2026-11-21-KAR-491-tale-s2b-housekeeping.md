### 2026-11-21 — KAR-491: TALE S2b housekeeping

- **Deleted `models/tale/interactions/`:** 10 files and 20 storylets, loaded by nothing. `StoryletLibrary.LoadFromDirectory` does not recurse, and `nogame.resources.json` declares only the 14 top-level files. Every storylet in it duplicated one in the role files (checked 2026-11-21). No code, test or script referenced it.
- **Docs and comments aligned with the code (S2b plan §5):**
  - `wait_edge` is parsed but not implemented, neither in the DES nor live (ARCHITECTURE.md); planned with S3.
  - `ArcStack.cs`: a claim uses its trigger's `interrupt_scope` (since KAR-477), nest by default.
  - The roadmap's S3 line: `help_request` is emitted by `ask_for_help` in `worker.json`.
- **Tests:** xUnit 2287 pass; the 26 failures are the known KAR-461 drift. TALE 133/133.
