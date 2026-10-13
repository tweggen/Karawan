# Karawan Game Systems Documentation

This directory contains design and architecture documentation for major game systems (non-engine, non-TALE).

## Systems by Category

### 🎤 **Expect engine** — [../testing/docs/](../testing/docs/)
The ExpectEngine test harness (formerly filed under narration)

- [EXPECT_ENGINE.md](../testing/docs/EXPECT_ENGINE.md) — Concept and architecture
- [EXPECT_IMPLEMENTATION.md](../testing/docs/EXPECT_IMPLEMENTATION.md) — Implementation details

### 🌍 **World Generation** — [../world-gen/](../world-gen/)
Procedural world creation and L-system generation

- [LSYSTEM.md](../world-gen/docs/LSYSTEM.md) — L-system features and capabilities
- [LSYSTEM_EDITOR.md](../world-gen/archive/LSYSTEM_EDITOR.md) — L-system editor planning
- [BIRD-SWARM-OPERATOR.md](../world-gen/plans/proposed/BIRD-SWARM-OPERATOR.md) — Fragment operators (bird swarms, etc.)

### 📋 **Quest System** — [../main/docs/quest/](../main/docs/quest/)
Quest mechanics and lifecycle

- [QUEST_SYSTEM.md](../main/docs/quest/QUEST_SYSTEM.md) — Quest architecture and refactoring

### 💾 **Persistence** — [../main/docs/persistence/](../main/docs/persistence/)
Save/load and data storage systems

- [LITEDB.md](../main/docs/persistence/LITEDB.md) — LiteDB storage implementation
- [GAME_SERVER_API.md](../main/docs/persistence/GAME_SERVER_API.md) — online login and cloud save: what exli answers and what the game shows

### 📱 **Platforms** — [../main/docs/platforms/](../main/docs/platforms/)
Platform-specific features and concerns

- [ANDROID.md](../main/docs/platforms/ANDROID.md) — Android/MAUI specific (keyboard, input)

### 🔧 **Build** — [../main/docs/build/](../main/docs/build/)
Build pipeline, asset manifest, MSBuild task topology

- [PIPELINE.md](../main/docs/build/PIPELINE.md) — `__include__` resolution, AndroidResources.xml/InnoResources.iss generation, dual Mix.cs implementations, common stale-binary failure modes

## See Also

- [../main/docs/architecture/](../main/docs/architecture/) — Core system architectures (rendering, physics, audio, input)
- [../testing/](../testing/) — Testing infrastructure
- [../tale/](../tale/) — TALE narrative simulation system

---

**Last Updated:** 2026-05-05
