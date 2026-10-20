# ExpectEngine Implementation Results

## What Was Built

An expect-style system testing framework for the Karawan/Joyce game engine, split into two layers:

### 1. ExpectEngine Library (standalone, NuGet-able)

**Location:** `ExpectEngine/`

A generic, host-agnostic library with zero engine dependencies. Uses `System.Threading.Channels` internally for lock-free cross-thread event buffering.

| File | Purpose |
|------|---------|
| `ExpectEngine.csproj` | net10.0 project, zero external dependencies |
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

**Location:** `models/tests/startup-smoke.json` (a game-process script; see *Running Tests* for its current status)

Verifies the critical startup path:
1. Root scene kicks off (`nogame.scenes.root.Scene.kickoff`)
2. Narration auto-starts (`narration.script.started`)
3. First narration node reached (`narration.node.reached`)

## How It Works

### Activation
`TestDriverModule` is **not** part of the game's module graph (commit `8059624e`, 2026-03-14, removed it from `nogameCode/nogame/Main.cs`). The host that wants a test activates it explicitly: `TestRunner/TestRunnerMain.cs` resolves it with `I.Get<engine.testing.TestDriverModule>()` (registered in `models/nogame.implementations.json`) and calls `ModuleActivate()`. On activation it returns immediately (no-op) unless a script path is set:
- **GlobalSettings** `test.script` — path to a JSON test script (relative to `models/`)
- **Environment variable** `JOYCE_TEST_SCRIPT` — same, used only when `test.script` is empty

### Event Tapping
`JoyceTestEventSource` subscribes at the root `PathNode` of the `SubscriptionManager` (empty path ""). This causes it to receive ALL events before any specific path subscribers. It never sets `IsHandled`, so events flow through normally — the test driver is purely an observer.

### Execution Flow
1. `TestDriverModule.OnModuleActivate()` checks for a script path
2. Creates the `TestSession` (which subscribes to the event source) and then sets the static `SessionReady` signal, so a host that produces events (TestRunner's DES thread) can wait for it instead of sleeping
3. Loads the script via `engine.Assets.Open()` (works on desktop and Android) and runs it on `Task.Run()` (background thread, doesn't block the engine)
4. The `TestSession` uses a `Channel<TestEvent>` to bridge engine events (logical thread) to the test runner (background thread)
5. On completion `_reportResult` prints `[TEST] <OUTCOME>: <message>` to the console, queues `Engine.Exit()` on the main thread, stores the static `ExitCode` and sets the static `Completed` signal

### Exit Codes
- `0` = all test steps passed
- `1` = any step failed, timed out, or errored

The host decides the process exit: TestRunner waits on `Completed` (timeout `JOYCE_TEST_TIMEOUT`, default 60 s) and calls `Environment.Exit(ExitCode)`; it exits 1 on its own timeout. For hosts that never wait on `Completed`, `TestDriverModule` calls `Environment.Exit(exitCode)` itself after a 2 s delay as a fallback. The shell runners (`run_tests.sh`, `tools/run_one_tale_test.sh`) classify a run by grepping the `[TEST] PASS` / `[TEST] FAIL` line, not by exit code.

## Running Tests

Tests run through the headless `TestRunner`; see `docs/testing/docs/RUN_TESTS_GUIDE.md` for the suite runners.

```bash
dotnet build TestRunner/TestRunner.csproj -c Release

# One script, path relative to models/
JOYCE_TEST_SCRIPT=tests/tale/phase0-des/01-initialization.json \
  dotnet TestRunner/bin/Release/net10.0/TestRunner.dll
echo $?  # 0 = pass, 1 = fail
```

`TestRunner` also starts the TALE DES simulation on a background thread (or the Phase 6 cluster-lifecycle setup when the script path contains `phase6`), which is what the TALE scripts expect against. Timeout knobs read by `ScriptRunner`: `JOYCE_TEST_TIMEOUT_SCALE` (multiplies every in-script timeout) and `JOYCE_TEST_SLEEP_SCALE` (multiplies `sleep` steps).

A plain game run (`dotnet run --project Karawan/Karawan.csproj` with `JOYCE_TEST_SCRIPT` set) no longer activates the module, so the game-process scripts under `models/tests/*.json` (`startup-smoke.json`, `taxi-quest-smoke.json`, ...) and the `tests/run-*.sh` wrappers that launch the game with `JOYCE_TEST_SCRIPT` do not currently execute a script. Whether to re-wire or retire them is open, tracked separately.

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
- `expect` — wait for an event matching `type` and optionally `code` (exact) and/or `codeMatches` (a .NET regex over the code), using FishForEvent: non-matching events are skipped **and consumed**, so expect steps match in order. `"count": n` (on the step) waits for n matching events within the one `timeout`.
- `inject` — push an event into the engine's EventQueue
- `sleep` — wait N milliseconds
- `action` — control flow (currently: `quit` with `pass` or `fail` result)

**Validation (KAR-433).** `ScriptRunner.LoadFromStream` rejects a script that
- has a top-level key other than `name`, `description`, `globalTimeout`, `steps` and the
  documentation keys `phase`, `category`, `priority`, `preconditions`, `expectedOutcome`;
- has no steps, or no `expect` step — a script that checks nothing must not pass;
- has a step with an unknown key, or with not exactly one of `expect`/`inject`/`sleep`/`action`;
- has an `expect` with a key other than `type`/`code`/`codeMatches`, or a `count` below 1.

The host reports the rejection as an Error. 49 TALE scripts once declared their checks in an
`"expected"` block nobody read and passed with no steps; `TaleScriptsLoadTests` (xUnit, so CI)
loads every script under `models/tests/tale/` through this validation.

## Files Modified

| File | Change |
|------|--------|
| `Karawan.sln` | Added ExpectEngine project |
| `Joyce/Joyce.csproj` | Added ProjectReference to ExpectEngine |
| `JoyceCode/JoyceCode.projitems` | Added 3 Compile entries for engine/testing/*.cs |
| `nogameCode/nogame/Main.cs` | Originally added `MyModule<TestDriverModule>()` to ModuleDepends; removed again in `8059624e` (activation moved to TestRunner) |
| `TestRunner/TestRunnerMain.cs` | Activates `TestDriverModule` and waits on `SessionReady` / `Completed` |
| `models/nogame.implementations.json` | Registered `engine.testing.TestDriverModule` |
| `models/nogame.globalSettings.json` | Added `test.script` setting (default empty) |

## Android Considerations

- Test scripts are loaded via `engine.Assets.Open()` which uses the platform's asset system
- On Android (Wuka), assets from `models/` are copied to the APK's assets directory automatically
- No filesystem access assumptions in the generic ExpectEngine library
- Results visible in logcat via `Logger.Trace()`

## Future Extensions

- **Lua test scripts** using a separate Lua state (not the game's scripting state) for complex conditional logic
- **xUnit wrapper** that launches the game process and asserts on exit code (CI runs only the in-process xUnit suite `tests/JoyceCode.Tests`, not ExpectEngine scripts)
- **Additional step types**: `awaitCondition` (poll engine state), `log` (emit test log), `repeat` (loop steps)
- **Event recording** for replay-based regression testing
