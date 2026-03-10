# LiteDB Storage

## Overview

LiteDB 5.0.15 is used as an embedded document database for local persistence. The system manages 3 separate database files storing game state, configuration, and world generation data.

## Database Files

All stored at `GlobalSettings["Engine.RWPath"]` + `{name}.db`:

| Database | Version | Purpose |
|----------|---------|---------|
| `gamestate.db` | 3 | Player save data |
| `gameconfig.db` | 3 | Login/config persistence |
| `worldcache.db` | 1029 | Cached procedural street networks |

## File Paths by Platform

- **Windows:** `%APPDATA%\Karawan Engine\karawan\`
- **Linux:** `~/.local/share/Karawan Engine/karawan/`
- **macOS:** `~/Library/Application Support/Karawan Engine/karawan/`
- **Android:** Internal app storage (`Environment.SpecialFolder.ApplicationData`)

### Desktop Path Overrides

- `--rwpath <path>` CLI argument
- `JOYCE_RWPATH` environment variable
- `--settings-file <json>` / `--setting <key>=<value>`

Priority: CLI > Environment > Config defaults.

### Path Resolution

- **Desktop** (`DesktopMain.cs`): `LaunchConfig.GetRWPath()` computes `SpecialFolder.ApplicationData / Branding.Vendor / Branding.AppName`
- **Android** (`GameActivity.cs`): `Environment.GetFolderPath(SpecialFolder.ApplicationData)` (app-private internal storage)

## Debug vs Release

No differences. Database initialization is identical in both modes.

## Collections and Schema

### gamestate.db — GameState (single document, BsonId = 2)

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| `Id` | int | 2 | BsonId (constant) |
| `PlayerPosition` | Vector3 | Zero | Player world position |
| `PlayerOrientation` | Quaternion | Identity | Player facing direction |
| `Health` | int | 1000 | Player health value |
| `Cash` | int | 0 | Player currency |
| `PlayerMode` | string | "hover" | Current player mode ("hover", "walking") |
| `PlayerEntity` | int | 0 | Legacy player mode (0=hover, 1=walk) |
| `Story` | string | "" | Active narration script state (JSON) |
| `FollowedQuestId` | string | null | Quest currently followed for satnav/markers |
| `FollowedQuestIds` | List\<string\> | [] | All followed quest IDs (redundant with entity persistence, kept for quick access) |
| `WorldModifications` | List\<string\> | [] | World modification descriptors (e.g. "destroyed:building:cluster3:42") |
| `Entities` | string | "" | JSON-serialized all persistable entity components |
| `GameNow` | DateTime | GameT0 | In-game clock/time |
| `NumberCubes` | int | 0 | Collectible count |
| `NumberPolytopes` | int | 0 | Collectible count |

Entity persistence: all `[IsPersistable]`-marked ECS components are serialized to JSON via `EntitySaver` and stored in the `Entities` string field. On load, `EntitySaver.LoadAll()` restores them.

### Narration State (Story field)

The `Story` field contains a JSON string with:
- `startupTriggered` (bool) — whether the startup narration has fired
- `props` (object) — narration-set properties (key/value pairs set via `props.set` events). Tracked in `NarrationBindings._narrationProps`, serialized on save, restored before any scripts run on load. Supports string, float, and bool values.
- `activeScript.name` — script identifier
- `activeScript.mode` — "conversation", "narration", or "scriptedScene"
- `activeScript.instanceId` — context instance (e.g. NPC entity ID)
- `activeScript.nodeId` — current node in the script
- `activeScript.visitCounts` — per-node visit count dictionary

**Remaining gaps:** NPC conversation history is not explicitly tracked (can be solved by adding `props.set` events to conversation scripts — the props persistence will handle it automatically). Speaker context outside the active script is not a real gap — `RestoreAt` re-enters the node and re-processes Speaker flow statements.

### gameconfig.db — GameConfig (single document, BsonId = 1)

| Field | Type | Purpose |
|-------|------|---------|
| `Id` | int | BsonId = 1 (constant) |
| `Username` | string | Login email |
| `Password` | string | Login password |
| `WebToken` | string | Session token |
| `LoginMode` | int | 0=LoginGlobally, 1=LoginLocally |

### worldcache.db — 3 Collections

**ClusterDesc** (multiple documents):

| Field | Type | Purpose |
|-------|------|---------|
| `Id` | int | BsonId, cluster identifier |
| `Pos` | Vector3 | World position |
| `Size` | float | Cluster radius |
| `Name` | string | Display name |
| `Index` | int | List index |
| `Merged` | bool | Merged with another |
| `IdString` | string | String key |

**Stroke** (multiple documents, indexed on `ClusterId`):

| Field | Type | Purpose |
|-------|------|---------|
| `Sid` | int | BsonId, stroke ID |
| `ClusterId` | int | Parent cluster (indexed) |
| `A`, `B` | StreetPoint | Start/end points (Include relationships) |

**StreetPoint** (multiple documents, indexed on `ClusterId`):

| Field | Type | Purpose |
|-------|------|---------|
| `Id` | int | BsonId, point ID |
| `ClusterId` | int | Parent cluster (indexed) |
| `Pos` | Vector2 | 2D position |
| `Creator` | string | Creator system name |
| `InStore` | bool | Currently stored |

## Core Service: DBStorage

**Location:** `JoyceCode/engine/DBStorage.cs`

### Access Pattern

All access through `WithOpen(dbName, version, action)`:

```
lock(_lo)
  -> _open(dbName, version)      // create or verify version
  -> action(db)                  // caller's code
  -> _close(dbName)              // commit + dispose
```

### Version Handling

On open, if the existing DB has a lower `UserVersion` than expected, the entire file is deleted and recreated. This ensures schema compatibility without migration logic.

### Error Recovery

- Collection-level: drop and recreate collection on deserialization failure
- Database-level: delete and recreate on version mismatch

### Custom BsonMapper

Registered types for round-trip serialization:
- `Vector2` — BsonArray of 2 doubles
- `Vector3` — BsonArray of 3 doubles
- `Quaternion` — BsonArray of 4 doubles
- `DateTime` — BsonDocument with `ticks` field (preserves timezone info)

## Auto-Save System

**Location:** `nogameCode/nogame/modules/AutoSave.cs`

- Interval: 60 seconds (configurable via `SaveInterval` property)
- Flow: `EntitySaver.SaveAll()` -> update `GameState.Entities` -> `DBStorage.SaveGameState()` -> optional cloud sync to `silicondesert.io`
- On load: tries cloud first (if online), falls back to local, creates new game if no save exists

## Key Files

| File | Role |
|------|------|
| `JoyceCode/engine/DBStorage.cs` | Core database service (open/close/collections) |
| `nogameCode/nogame/GameState.cs` | Game state entity definition |
| `nogameCode/nogame/config/GameConfig.cs` | Configuration entity definition |
| `JoyceCode/engine/world/ClusterDesc.cs` | World cluster entity |
| `JoyceCode/engine/streets/ClusterStorage.cs` | Street network caching logic |
| `JoyceCode/engine/streets/StreetPoint.cs` | Street point entity |
| `JoyceCode/engine/streets/Stroke.cs` | Street stroke entity |
| `nogameCode/nogame/modules/AutoSave.cs` | Auto-save timer and cloud sync |

## Not Using LiteDB

The Aihao editor does not use LiteDB. It works directly with JSON config files via the Mix system.
