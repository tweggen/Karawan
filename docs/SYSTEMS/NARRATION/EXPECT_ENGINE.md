# ExpectEngine: Event-Driven System Testing

## Problem

The Karawan/Joyce game engine has no automated system-level tests. The game already supports non-interactive startup (via `globalSettings.json` flags like `debug.option.autoLogin`), but there is no way to verify that the game reaches expected states, handles events correctly, or completes key flows without manual observation.

Unit tests alone cannot cover the integration between modules, the rendering pipeline, world generation, narration, quests, and input handling. What's needed is a way to run the game end-to-end and assert on its behavior.

## Concept

An **expect-style test framework** inspired by tcl's `expect`, adapted for event-driven C# applications. The framework:

1. **Observes** events emitted by the host application
2. **Waits** for specific events or conditions with timeouts
3. **Injects** events or actions in response to observed state
4. **Reports** pass/fail results

This is analogous to how `expect` works with interactive console programs, but operates on structured events inside the process rather than on text streams.

## Architecture

### Generic NuGet Package: `ExpectEngine`

A standalone library with **zero dependencies on Karawan/Joyce**. It provides the core expect pattern and communicates with the host application through two interfaces:

```csharp
/// Implemented by the host application to provide events to the test session.
public interface ITestEventSource
{
    /// Subscribe to receive all events. Returns a disposable subscription.
    IDisposable Subscribe(Action<TestEvent> onEvent);
}

/// Implemented by the host application to accept injected events.
public interface ITestEventSink
{
    /// Inject an event into the host application.
    Task InjectEvent(TestEvent testEvent);
}

/// A test event — a named event with optional typed payload.
public record TestEvent(string Type, string Code = null, object Payload = null);
```

The library provides:

```csharp
public class TestSession
{
    // Wait for an event matching the predicate, fail on timeout
    Task<TestEvent> ExpectEvent(Func<TestEvent, bool> predicate, TimeSpan timeout);

    // Skip non-matching events, return first match
    Task<TestEvent> FishForEvent(Func<TestEvent, bool> predicate, TimeSpan timeout);

    // Poll a condition until true or timeout
    Task AwaitCondition(Func<bool> condition, TimeSpan timeout, TimeSpan? pollInterval);

    // Inject an event into the host application
    Task InjectEvent(TestEvent evt);

    // Assert no matching event arrives within a time window
    Task ExpectNoEvent(Func<TestEvent, bool> predicate, TimeSpan window);

    // Signal test completion
    void Complete(TestResult result);
}
```

Internally, `TestSession` uses:
- `System.Threading.Channels.Channel<TestEvent>` as an async event queue
- `TaskCompletionSource` for converting event arrival into awaitable futures
- `CancellationTokenSource` for timeout enforcement

### Karawan Integration: `Joyce.TestDriver`

An engine-level module that bridges `ExpectEngine` to the Joyce event system:

- Implements `ITestEventSource` by subscribing to the engine's `EventQueue`
- Implements `ITestEventSink` by pushing events via `Engine.QueueMainThreadAction`
- Activates when an environment variable or CLI flag is set (e.g., `JOYCE_TEST_SCRIPT`)
- Loads test scripts from the asset system (works on both desktop and Android)
- Reports results via process exit code and structured log output

### Test Script Format

**JSON** (declarative, works as assets on Android, no scripting engine needed):

```json
{
  "name": "startup-narration-flow",
  "description": "Verify that narration starts after root scene kickoff",
  "globalTimeout": 60,
  "steps": [
    {
      "expect": { "type": "nogame.scenes.root.Scene.kickoff" },
      "timeout": 30,
      "comment": "Wait for the main scene to start"
    },
    {
      "expect": { "type": "narration.scriptStarted", "code": "main" },
      "timeout": 10,
      "comment": "Narration should auto-start"
    },
    {
      "inject": { "type": "input.key.pressed", "code": "e" },
      "comment": "Advance the narration"
    },
    {
      "expect": { "type": "narration.nodeReached" },
      "timeout": 5
    },
    {
      "action": "quit",
      "result": "pass"
    }
  ]
}
```

**Lua** (optional, more powerful for conditionals/loops):
- Uses a **separate Lua state** from the game's scripting — no interference
- Lua bindings expose `expect()`, `inject()`, `awaitCondition()`, `quit()`
- Useful for complex multi-branch test scenarios

## Cross-Platform Considerations

### Android
- Test scripts are loaded as assets through the existing asset system
- No filesystem access assumptions in the generic library
- `Joyce.TestDriver` handles platform-specific script loading
- Results reported via logcat and/or a results file written to app-accessible storage

### Desktop (Windows/macOS/Linux)
- Test scripts loaded from filesystem paths
- Results via process exit code (0 = pass, nonzero = fail) and stdout
- CI-friendly: xUnit tests can launch the game process, pass a test script, and assert on exit code

## Prior Art

| Library | Pattern | Limitation |
|---------|---------|------------|
| Akka.NET TestKit | `ExpectMsg`, `FishForMessage`, `AwaitCondition` | Requires Akka actor system |
| Microsoft Coyote | State machine + systematic exploration | Concurrency-focused, no expect API |
| MassTransit Test Harness | `Consumed.Any<T>()` with timeout | Tied to message bus |
| Rx.NET TestScheduler | Virtual time, event sequence assertions | Requires IObservable wrapping |
| Unity Test Framework | Play Mode coroutine tests | Unity-only |

None provides a standalone, host-agnostic expect framework for event-driven applications.

## Project Structure

```
ExpectEngine/                  — NuGet-able library (netstandard2.1 / net9.0)
  ITestEventSource.cs
  ITestEventSink.cs
  TestEvent.cs
  TestSession.cs
  TestResult.cs
  ScriptRunner.cs              — JSON test script interpreter
ExpectEngine.Tests/            — xUnit tests for the library
Joyce/TestDriver/              — Karawan integration module
  TestDriverModule.cs          — AModule, bridges EventQueue <-> ExpectEngine
  JoyceTestEventSource.cs
  JoyceTestEventSink.cs
```
