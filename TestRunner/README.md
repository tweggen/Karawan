# TestRunner - TALE Test Harness

A dedicated test harness for running TALE test scripts (Phases 0, 1, 3) via the ExpectEngine framework.

## Overview

TestRunner is a thin CLI harness that:
1. Initializes the Joyce engine with full module support
2. Loads the game configuration (nogame.json)
3. Registers all game modules (including TestDriverModule)
4. Runs the engine's event loop to process test scripts
5. Captures and reports test results

## Build

```bash
dotnet build TestRunner.csproj -c Release -p:EnableSourceLink=false
```

## Usage

### Run a Single Test
```bash
JOYCE_TEST_SCRIPT="tests/tale/phase0-des/01-initialization.json" \
  ./bin/Release/net9.0/TestRunner
```

### Run via Test Script
```bash
./run_tests.sh phase0
./run_tests.sh phase1
./run_tests.sh phase3
./run_tests.sh all
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
5. **Engine Creation**: Headless Silk.NET platform
6. **Game Config**: Load nogame.json → InterpretConfig()
   - Registers all modules from nogame.implementations.json
   - **Crucially**: Registers TestDriverModule
7. **Engine Loop**: e.Execute() starts the main event processing loop
   - TestDriverModule activates and loads test script
   - Test events are injected and processed
   - Results are captured

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

The TestDriverModule is registered via the game config:
- **File**: `nogameCode/nogame/Main.cs`
- **Line**: `new MyModule<engine.testing.TestDriverModule>()`
- **Activation**: Triggers when `JOYCE_TEST_SCRIPT` env var is set
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
- **TestRunner.csproj**: Project file with dependencies
- **README.md**: This file

## Related Documentation

- **Test Execution**: See `docs/tale/TESTING_QUICK_START.md`
- **Test Scripts**: See `models/tests/tale/phase{0,1,3}-*/`
- **Test Specifications**: See `docs/tale/TALE_TEST_SCRIPTS_PHASE_*.md`
- **ExpectEngine Framework**: See `docs/tale/EXPECT_ENGINE_IMPLEMENTATION.md`

## Status

✅ **Implemented**: Full initialization pipeline working
✅ **Tested**: Confirms TestDriverModule activation
✅ **Ready**: For Phase 0/1/3 test execution
⏳ **Next**: Run full test suite with `./run_tests.sh all`
