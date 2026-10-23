### 2026-10-23 — KAR-455 (TALE S1-b): the player interaction model

- Interaction types in `models/nogame.interactions.json` may carry a `player` block (`PlayerInteractionSpec`): trust change, target properties and fact, cooldown with repeat scaling, wealth per cash, and a witness block. New player-only types `assault`, `run_over`, `pay`, `bribe`, `drive` (no condition class, so NPC pairs never select them); `chat` (+0.02) and `help` (+0.4) got player blocks.
- `PlayerInteractions` (via `TaleManager.ApplyPlayerInteraction` / `ApplyWitnessedPlayerInteraction`) writes trust only through `ApplyPlayerTrust`, flattens positive changes by `(1 − trust)` (one help from first contact → 0.49), scales repeats inside the cooldown (one punch's several contact episodes count once), adds properties and wealth, and sets `player_fact.*`. Witnesses react with `factor × (1 − 0.5·d/radius)`, × 1.5 for the victim's groupmates.
- Pure helpers for S1-d: `WitnessSelector` (nearest first, ties by id, capped, line of sight only along that prefix, order-independent) and `PlayerActionClassifier` (player weapon → assault, player vehicle → run-over).
- Nothing in the game calls it yet; S1-c, S1-d and S1-g do. 31 new xUnit tests (`PlayerInteractionsTests`), most on the shipped JSON.
- Docs: ARCHITECTURE §7, REFERENCE (player block), `TALE-S1-PLAYER-IN-GRAPH.md` §4 status.
