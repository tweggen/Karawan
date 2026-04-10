# ExpectEngine Implementation Results

## What Was Built

An expect-style system testing framework for the Karawan/Joyce game engine, split into two layers:

### 1. ExpectEngine Library (standalone, NuGet-able)

**Location:** `ExpectEngine/`

A generic, host-agnostic library with zero engine dependencies. Uses `System.Threading.Channels` internally for lock-free cross-thread event buffering.

| File | Purpose |
|------|---------|
| `ExpectEngine.csproj` | net9.0 project, zero external dependencies |
| `TestEvent.cs` | Host-agnostic event: Type (string), Code (string), Payload (object), Timestamp |
| `TestResult.cs` | Outcome enum (Pass/Fail/Timeout/Error), message, step index, elapsed time, log |
| `ITestEventSource.cs` | Interface for host to provide events: `IDisposable Subscribe(Action<TestEvent>)` |
| `ITestEventSink.cs` | Interface for host to accept injected events: `Task InjectEventAsync(TestEvent)` |
| `TestExpectationException.cs` | Exception types for test failures and timeouts |
| `TestSession.cs` | Core expect engine with Channel-based async operations |
| `ScriptRunner.cs` | JSON test script interpreter |

**TestSession API:**
- `FishForEvent(predicate, timeout)` — skip non-matching events, return first match
- `ExpectEvent(predicate, timeout)` — strict: next event must match
- `InjectEvent(event)` — push an event into the host application
- `AwaitCondition(condition, timeout, pollInterval)` — poll until true
- `ExpectNoEvent(predicate, window)` — assert silence

### 2. Joyce Integration Layer

**Location:** `JoyceCode/engine/testing/`

Bridges the engine's event system to ExpectEngine.

| File | Purpose |
|------|---------|
| `JoyceTestEventSource.cs` | Subscribes to `SubscriptionManager` at root path ("") to tap all events |
| `JoyceTestEventSink.cs` | Injects events via `EventQueue.Push()` (thread-safe) |
| `TestDriverModule.cs` | `AModule` that loads/runs test scripts, reports results via exit code |

### 3. First Test Script

**Location:** `models/tests/startup-smoke.json`

Verifies the critical startup path:
1. Root scene kicks off (`nogame.scenes.root.Scene.kickoff`)
2. Narration auto-starts (`narration.script.started`)
3. First narration node reached (`narration.node.reached`)

## How It Works

### Activation
The `TestDriverModule` activates on every game start but returns immediately (no-op) unless one of these is set:
- **GlobalSettings** `test.script` — path to a JSON test script
- **Environment variable** `JOYCE_TEST_SCRIPT` — same, overrides globalSettings

### Event Tapping
`JoyceTestEventSource` subscribes at the root `PathNode` of the `SubscriptionManager` (empty path ""). This causes it to receive ALL events before any specific path subscribers. It never sets `IsHandled`, so events flow through normally — the test driver is purely an observer.

### Execution Flow
1. `TestDriverModule.OnModuleActivate()` checks for a script path
2. Loads the script via `engine.Assets.Open()` (works on desktop and Android)
3. Runs the script on `Task.Run()` (background thread, doesn't block the engine)
4. The `TestSession` uses a `Channel<TestEvent>` to bridge engine events (logical thread) to the test runner (background thread)
5. On completion, reports results to console and calls `Engine.Exit()` + `Environment.Exit(exitCode)`

### Exit Codes
- `0` = all test steps passed
- `1` = any step failed, timed out, or errored

## Running Tests

```bash
# Normal game run — TestDriverModule activates but does nothing
dotnet run --project nogame/nogame.csproj

# Run with a test script (requires autoLogin for non-interactive start)
JOYCE_TEST_SCRIPT=tests/startup-smoke.json dotnet run --project nogame/nogame.csproj

# Check result
echo $?  # 0 = pass, 1 = fail
```

Note: The `debug.option.autoLogin` setting in `nogame.globalSettings.json` must be set to `"local"` or `"locally"` for the test to auto-start without user interaction at the login screen.

## JSON Test Script Format

```json
{
  "name": "test-name",
  "description": "Human-readable description",
  "globalTimeout": 120,
  "steps": [
    {
      "expect": { "type": "event.type", "code": "optional.code" },
      "timeout": 30,
      "comment": "Description of what we're waiting for"
    },
    {
      "inject": { "type": "event.type", "code": "value" },
      "comment": "Send an event to the engine"
    },
    {
      "sleep": 1000,
      "comment": "Wait 1 second"
    },
    {
      "action": "quit",
      "result": "pass"
    }
  ]
}
```

**Step types:**
- `expect` — wait for an event matching type (and optionally code), using FishForEvent (skips non-matching)
- `inject` — push an event into the engine's EventQueue
- `sleep` — wait N milliseconds
- `action` — control flow (currently: `quit` with `pass` or `fail` result)

## Files Modified

| File | Change |
|------|--------|
| `Karawan.sln` | Added ExpectEngine project |
| `Joyce/Joyce.csproj` | Added ProjectReference to ExpectEngine |
| `JoyceCode/JoyceCode.projitems` | Added 3 Compile entries for engine/testing/*.cs |
| `nogameCode/nogame/Main.cs` | Added `MyModule<TestDriverModule>()` to ModuleDepends |
| `models/nogame.implementations.json` | Registered `engine.testing.TestDriverModule` |
| `models/nogame.globalSettings.json` | Added `test.script` setting (default empty) |

## Android Considerations

- Test scripts are loaded via `engine.Assets.Open()` which uses the platform's asset system
- On Android (Wuka), assets from `models/` are copied to the APK's assets directory automatically
- No filesystem access assumptions in the generic ExpectEngine library
- Results visible in logcat via `Logger.Trace()`

## Future Extensions

- **Lua test scripts** using a separate Lua state (not the game's scripting state) for complex conditional logic
- **xUnit wrapper** that launches the game process and asserts on exit code for CI integration
- **Additional step types**: `awaitCondition` (poll engine state), `log` (emit test log), `repeat` (loop steps)
- **Event recording** for replay-based regression testing
