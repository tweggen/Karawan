# Karawan Engine Architecture Documentation

High-level architecture and design for core engine systems.

## Core Systems

### 🎨 **Rendering** — [RENDERING.md](RENDERING.md)
Graphics pipeline, mesh batching, material system

### ⚙️ **Physics** — PHYSICS.md (not written yet)
Physics engine integration, collisions, movement

### 🔊 **Audio** — AUDIO.md (not written yet)
Audio engine, sound effects, music systems

### ⌨️ **Input** — INPUT.md (not written yet)
Input pipeline, event handling, controller mapping

### 🎮 **Engine Core** — ENGINE.md (not written yet)
Joyce engine foundation: ECS, scene management, modules

### 🧵 **Threading** — [THREADING.md](THREADING.md)
Thread roles (logical/platform/worker pool), capped scheduler, managed-blocker protocol

## Decisions

### 🔌 **Platform backend** — [PLATFORM_BACKEND.md](PLATFORM_BACKEND.md)
ADR (**accepted, implemented 2026-08-14**): windowing, GL/audio bindings and model import. Moved off
Silk.NET to SDL3 plus self-generated bindings, and baked FBX out of the runtime. §9 lists its
load-bearing claims by evidence class so it can be attacked directly.

## Implementation References

For implementation details of specific engine components, see:
- **Rendering**: `JoyceCode/engine/`, `Splash.OpenGL/` and `Splash.API.OpenGL/`
- **Physics**: `JoyceCode/engine/physics/`
- **Audio**: `Boom/` and `Boom.OpenAL/`
- **Input**: `JoyceCode/builtin/controllers/InputController.cs`
- **ECS**: DefaultEcs `World`, created in `JoyceCode/engine/Engine.cs`

## See Also

- [../../../SYSTEMS/](../../../SYSTEMS/) — Game systems (quests, persistence, world generation, narration)
- [../../../tale/docs/](../../../tale/docs/) — TALE narrative simulation
- [PROCESS.md](../../../../PROCESS.md) — Generic development process
- [CLAUDE.md](../../../../CLAUDE.md) — Project overview

---

**Last Updated:** 2026-04-10
