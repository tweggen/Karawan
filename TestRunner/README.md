# TestRunner - TALE Test Harness

A dedicated test harness for running TALE test scripts (`models/tests/tale/`) via the ExpectEngine framework.

## Overview

TestRunner is a thin CLI harness that:
1. Initializes the Joyce engine with full module support
2. Loads the game configuration (nogame.json)
3. Activates `TestDriverModule` itself and starts the DES simulation on a background thread
4. Runs the engine's logical thread to process test scripts
5. Captures and reports test results

## Build

```bash
dotnet build TestRunner/TestRunner.csproj -c Release
```

## Usage

### Run a Single Test
```bash
JOYCE_TEST_SCRIPT="tests/tale/phase0-des/01-initialization.json" \
  dotnet TestRunner/bin/Release/net10.0/TestRunner.dll
```

### Run via Test Script
```bash
./run_tests.sh phase0
./run_tests.sh smoke
./run_tests.sh all            # 200 tests, 60-day sims
./run_tests_parallel.sh all   # same, in parallel
```

## Architecture

### Initialization Flow

1. **Platform Setup**: Graphics API (OpenGL 4.1/4.3)
2. **Resource Path**: Locate models/ directory
3. **Launch Config**: Load game.launch.json
4. **Asset Implementation**: Minimal implementation that:
   - Resolves test script paths (relative to models/)
   - Falls back to shaders/ and textures/ subdirectories
   - Returns empty streams for missing graphics resources
5. **Engine Creation**: `Splash.OpenGL.Platform.EasyCreateHeadless()` (no window)
6. **Game Config**: Load nogame.json → InterpretConfig()
   - Registers all modules from nogame.implementations.json (including `engine.testing.TestDriverModule`)
7. **Test Activation**: `I.Get<engine.testing.TestDriverModule>().ModuleActivate()`
   - `JOYCE_TEST_SCRIPT=entity-behavior-tests` runs `BehaviorBugfixTests` instead and exits
8. **Engine Loop**: `ExecuteLogicalThreadOnly()` + `CallOnPlatformAvailable()`; the DES thread waits on `TestDriverModule.SessionReady`
9. **Completion**: waits on `TestDriverModule.Completed` (`JOYCE_TEST_TIMEOUT`, default 60 s) and exits with its `ExitCode`

### MinimalAssetImplementation

Custom asset implementation that:
- Extends `AAssetImplementation` (auto-registers itself)
- Searches multiple paths for assets:
  ```
  - models/<tag>
  - models/shaders/<tag>
  - models/textures/<tag>
  - nogame/generated/<tag>
  - <tag> (absolute/relative path)
  ```
- Returns empty streams for missing non-critical resources
- Logs warnings but continues (graceful degradation)

### Module Registration

The TestDriverModule is registered in `models/nogame.implementations.json` but is **not** in the game's module tree (removed from `nogameCode/nogame/Main.cs` in `8059624e`):
- **Activation**: `TestRunnerMain` activates it explicitly; it runs a script when `JOYCE_TEST_SCRIPT` (or GlobalSettings `test.script`) is set
- **Behavior**: Loads test JSON, runs test session, reports results

## Test Result Format

### Success
```
[TEST] PASS: Test name passed
[TEST] Elapsed: 00:00:01.234
```

### Failure
```
[TEST] FAIL: Expected event 'npc_created' within 5 seconds
[TEST] Elapsed: 00:00:05.123
```

## Troubleshooting

### Build Errors
- `SourceLink` issues: Use `-p:EnableSourceLink=false`
- Stale build tools: run `bash Tooling/Cmdline/build.sh && bash Chushi/build.sh` first
- Missing dependencies: Run `dotnet restore` first

### Runtime Issues
- "Platform Asset Implementation not setup": Asset impl initialization order (fixed)
- "Could not open test script": Verify path is relative to models/ directory
- NullReferenceException in Platform.Execute(): Requires full engine event loop

### Graphics Resource Warnings
- "Warning: Asset not found: LIghtingVS.vert"
- **Expected**: Non-critical for test execution
- Engine gracefully degrades for headless rendering

## Files

- **TestRunnerMain.cs**: Entry point and initialization
- **BehaviorBugfixTests.cs**: Entity-level behaviour checks (`run_tests.sh bugfix`)
- **TestRunner.csproj**: Project file with dependencies
- **README.md**: This file

## Related Documentation

- **Test Execution**: See `docs/testing/docs/RUN_TESTS_GUIDE.md`
- **Test Scripts**: See `models/tests/tale/phase*/`
- **Test Specifications**: See `docs/tale/docs/tests/PHASE_*.md`
- **ExpectEngine Framework**: See `docs/testing/docs/EXPECT_IMPLEMENTATION.md`

## Status

✅ **Implemented**: Full initialization pipeline working
✅ **Tested**: Confirms TestDriverModule activation
✅ **In use**: `run_tests.sh` / `run_tests_parallel.sh` drive it for all 200 regression tests (not run by CI)
