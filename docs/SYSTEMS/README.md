# Karawan Game Systems Documentation

This directory contains design and architecture documentation for major game systems (non-engine, non-TALE).

## Systems by Category

### 🎤 **Narration System** — [NARRATION/](NARRATION/)
Dialogue and narrative engine (Expect engine)

- [EXPECT_ENGINE.md](NARRATION/EXPECT_ENGINE.md) — Concept and architecture
- [EXPECT_IMPLEMENTATION.md](NARRATION/EXPECT_IMPLEMENTATION.md) — Implementation details

### 🌍 **World Generation** — [WORLD_GEN/](WORLD_GEN/)
Procedural world creation and L-system generation

- [LSYSTEM.md](WORLD_GEN/LSYSTEM.md) — L-system features and capabilities
- [LSYSTEM_EDITOR.md](WORLD_GEN/LSYSTEM_EDITOR.md) — L-system editor planning
- [FRAGMENT_OPERATORS.md](WORLD_GEN/FRAGMENT_OPERATORS.md) — Fragment operators (bird swarms, etc.)

### 📋 **Quest System** — [QUEST/](QUEST/)
Quest mechanics and lifecycle

- [QUEST_SYSTEM.md](QUEST/QUEST_SYSTEM.md) — Quest architecture and refactoring

### 💾 **Persistence** — [PERSISTENCE/](PERSISTENCE/)
Save/load and data storage systems

- [LITEDB.md](PERSISTENCE/LITEDB.md) — LiteDB storage implementation

### 📱 **Platforms** — [PLATFORMS/](PLATFORMS/)
Platform-specific features and concerns

- [ANDROID.md](PLATFORMS/ANDROID.md) — Android/MAUI specific (keyboard, input)

## See Also

- [../ARCHITECTURE/](../ARCHITECTURE/) — Core system architectures (rendering, physics, audio, input)
- [../TESTING/](../TESTING/) — Testing infrastructure
- [../tale/](../tale/) — TALE narrative simulation system

---

**Last Updated:** 2026-04-10
