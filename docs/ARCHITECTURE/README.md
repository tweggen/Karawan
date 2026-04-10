# Karawan Engine Architecture Documentation

High-level architecture and design for core engine systems.

## Core Systems

### 🎨 **Rendering** — [RENDERING.md](RENDERING.md)
Graphics pipeline, mesh batching, material system

### ⚙️ **Physics** — [PHYSICS.md](PHYSICS.md)
Physics engine integration, collisions, movement

### 🔊 **Audio** — [AUDIO.md](AUDIO.md)
Audio engine, sound effects, music systems

### ⌨️ **Input** — [INPUT.md](INPUT.md)
Input pipeline, event handling, controller mapping

### 🎮 **Engine Core** — [ENGINE.md](ENGINE.md)
Joyce engine foundation: ECS, scene management, modules

## Implementation References

For implementation details of specific engine components, see:
- **Rendering**: `JoyceCode/engine/` and `Splash.Silk/`
- **Physics**: `JoyceCode/engine/physics/`
- **Audio**: `Boom/` and `Boom.OpenAL/`
- **Input**: `JoyceCode/builtin/controllers/InputController.cs`
- **ECS**: `JoyceCode/engine/DefaultEcs.cs`

## See Also

- [../SYSTEMS/](../SYSTEMS/) — Game systems (quests, persistence, world generation, narration)
- [../tale/](../tale/) — TALE narrative simulation
- [PROCESS.md](../PROCESS.md) — Generic development process
- [CLAUDE.md](../../CLAUDE.md) — Project overview

---

**Last Updated:** 2026-04-10
