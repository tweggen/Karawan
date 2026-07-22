# Build Pipeline & Asset Manifest

**Status**: Reference  
**Last Updated**: 2026-05-11

How the Karawan/Joyce build turns `models/*.json` and friends into shippable assets, how those assets are registered for runtime lookup, and the failure modes that aren't obvious from reading the code.

---

## Pipeline order (from `nogame/nogame.csproj`)

Four MSBuild targets run, strictly sequenced:

```
EnsureGeneratedDirectory
        ↓
GatherTexturesHost  (Cmdline)       — packs texture atlases (atlas-*.json + atlas-*.png)
        ↓
CompileAssetsHost   (Chushi)        — bakes animation collections (ac-{hash}) and TALE scenarios (sc-{hash})
        ↓
GatherResources     (Cmdline)       — emits AndroidResources.xml + InnoResources.iss
        ↓
Compile             (csc)
```

All three pre-`Compile` targets write into `nogame/generated/`. **That directory is NOT tracked in git** — every dev builds their own copy. Stale `nogame/generated/` is the most common cause of "I added a file but the engine can't find it" symptoms.

The ordering between `GatherTexturesHost` and `CompileAssetsHost` is **load-bearing** and enforced via `DependsOnTargets="GatherTexturesHost"` on `CompileAssetsHost`. Chushi opens the packed `atlas-*.json` files during its texture-loading pass (`AAssetImplementation._whenLoadedTextures`), so the texture packer must finish first. Without the explicit dependency, MSBuild runs same-`BeforeTargets` targets in declaration order, and a fresh clone fails (no atlas present on first build); an existing clone happens to succeed because the previous build's atlases are still on disk.

---

## Two MSBuild task patterns

`nogame.csproj` registers three custom MSBuild tasks via `<UsingTask>`:

| Task | Loaded as | Path | Process model |
|---|---|---|---|
| `Res2TargetTask`     | UsingTask DLL | `Tooling/Cmdline/bin/Debug/netstandard2.0/$(HostRid)/joycecmd.dll` | **In-process** (loaded into MSBuild) |
| `PackTexturesTask`   | UsingTask DLL | same DLL | Wrapper only — **spawns** child process |
| `CompileAssetsTask`  | UsingTask DLL | same DLL (Chushi side) | Wrapper only — **spawns** child process |

`PackTexturesTask` and `CompileAssetsTask` both use `Process.Start(Executable, args)` where `Executable` points at a *published* binary:

- `..\Tooling\Cmdline\bin\Debug\net9.0\$(HostRid)\publish\joycecmd.exe`
- `..\Chushi\bin\Debug\net9.0\$(HostRid)\publish\Chushi.exe`

**Implication for dev workflow:** if you edit `Tooling/Cmdline/*.cs`, you have **two** outputs to refresh:

1. The netstandard2.0 task DLL (used by `Res2TargetTask`):  
   `dotnet build Tooling/Cmdline/Cmdline.csproj -c Debug -f netstandard2.0 -r win-x64`
2. The net9.0 published exe (used by `PackTexturesTask`):  
   `dotnet publish Tooling/Cmdline/Cmdline.csproj -c Debug -f net9.0 -r win-x64 --self-contained`

A bare `dotnet build nogame.csproj` will refresh (1) incrementally but does **not** publish (2). I have observed `PackTextures` running stale code while `Res2Target` runs current code in the same build invocation — symptoms look like the BFS works for one task and not the other. Always re-publish after touching Cmdline source.

`dotnet build-server shutdown` is also useful — MSBuild caches the task assembly across runs and can pin an old version even after a rebuild succeeds.

---

## Two `Mix.cs` implementations

The `__include__` resolver lives in two parallel files:

- `JoyceCode/engine/casette/Mix.cs` — runtime, uses `engine.Logger` (`Trace(_dc, $"…")`, `Warning(_dc, $"…")`).
- `Tooling/Cmdline/Mix.cs` — build-time, uses `Action<string> Trace` because Cmdline targets `netstandard2.0` for MSBuild compatibility and doesn't have access to the full Joyce logger framework.

**These must stay in functional sync.** When fixing a Mix bug, fix both. The Cmdline copy can't use interpolated-string handlers, categories, or other modern logger features — prefix messages with `"WARNING: "` instead of calling a real Warning sink.

`View.cs` is duplicated the same way (`JoyceCode/engine/casette/View.cs` vs. `Tooling/Cmdline/View.cs`).

---

## How `__include__` produces the asset manifest

Source-of-truth chain:

```
models/nogame.json
    ├── "narration": { "__include__": "nogame.narration.json" }
    ├── "resources": { "__include__": "nogame.resources.json" }
    └── ...

models/nogame.narration.json
    └── "scripts":
            ├── "tale.tag.routine": { "__include__": "tale/conversations/tale.tag.routine.json" }
            └── ... (more nested includes)
```

`Tooling/Cmdline/Mix._upsertIncludes` is a BFS that:

1. Walks every `__include__` in the JSON tree.
2. On hit, opens the file, calls `View.Upsert(currentPath, doc.RootElement, …)`, adds the path to `Mix.AdditionalFiles`, and **re-enqueues the loaded fragment** so its own `__include__` directives get resolved too.
3. Continues until the queue is empty. Includes nest arbitrarily.

`GameConfig._loadGameConfigFile` then iterates `_mix.AdditionalFiles` and inserts each into `MapResources`. `Res2Target` walks `MapResources` and emits two manifests:

- `nogame/generated/AndroidResources.xml` — `<AndroidAsset Include="…" LogicalName="basename"/>` entries the MAUI Android app reads.
- `nogame/generated/InnoResources.iss` — `Source: …; DestDir: {app}\assets\;` entries the Windows Inno Setup installer copies.

**Recursive include discovery does work.** If level-2 (or deeper) includes are missing from the manifest, suspect a stale `nogame/generated/` or stale published binary, not a BFS bug.

---

## Runtime asset lookup (desktop)

`Karawan/AssetImplementation.cs` (`override AAssetImplementation`):

```
_mapAssociations : SortedDictionary<basename, uri>
```

Populated from the manifest at startup (and dynamically extended by `Mix.AddAssociation` at runtime if it finds files on disk that weren't in the manifest).

`engine.Assets.Open(basename)` does:

1. `File.OpenRead(resourcePath + basename)` — handles flat-layout installations.
2. If that fails: `File.OpenRead(resourcePath + uri)` where `uri` is the manifest entry — handles dev/debug layouts where files live in subdirectories.

`engine.Assets.Exists(basename)` checks `_mapAssociations.ContainsKey(basename)` only — it does **not** fall through to disk. This is what the `Mix._upsertIncludes` runtime path uses to decide whether an `__include__` is resolvable. **A file that exists on disk but isn't in the manifest will return `false`** unless the dev-mode `File.Exists(pathProbe)` fallback in `Mix._upsertIncludes` kicks in (which depends on the resolved path being correct relative to `Engine.ResourcePath`).

---

## `Engine.ResourcePath` vs. `Mix.Directory`

Two separate path roots, easy to confuse:

- **`Engine.ResourcePath`** — set by the launcher (`DesktopMain._determineResourcePath`). Points at `./models/`, `./assets/`, or `../../../../../models/` depending on dev / installed / bin-output context. Used by `engine.Assets.Open` to resolve URIs.
- **`Mix.Directory`** — set when the Mix is constructed. At **build time** (`GameConfig._loadGameConfigFile`) it's the absolute directory of `nogame.json`. At **runtime** (`Loader` registers Mix via `I.Register`) it is **never set** — defaults to `""`. Used by `_upsertIncludes` to compute `jsonCompletePath = Path.Combine(Directory, includePath)` for the `File.Exists` dev-fallback probe.

The runtime `Mix.Directory == ""` is a latent footgun: `pathProbe = ResourcePath + "" + includePath`, which works only if includes are relative to `ResourcePath`. They are, as long as the include directives in JSON are written that way (e.g. `"tale/conversations/foo.json"` not `"models/tale/…"`).

---

## Failure modes & diagnostic patterns

### Symptom: runtime warns `node '' not found in script 'tale.X'`

`NarrationScript.FromJson` was handed `{"__include__": "..."}` instead of the resolved content. The include silently failed at runtime, leaving the script with empty `StartNodeId` and empty `Nodes`. Causes:

- Stale `nogame/generated/AndroidResources.xml` from before the include file existed → `engine.Assets.Exists` returns `false` → `_upsertIncludes` skips. **Fix: rebuild `nogame.csproj`.**
- Wrong path resolution at runtime (CWD-dependent) → both manifest and disk-fallback miss. **Fix: check `Engine.ResourcePath` and the include directive.**
- **Stale ancestor cache in `engine.casette.View`** — the include succeeded but `View.InvalidateCachesForPath` only invalidated the upserted path and its descendants, not its ancestors. The first `_view.Upsert("/narration", ...)` populated the `/narration` cache (with placeholders still in `scripts.X`), and the subsequent deeper `_view.Upsert("/narration/scripts/X", content)` calls did not invalidate `/narration`, so `_callSingleWhenLoaded` saw a stale tree where every script entry was still a `{__include__: ...}` placeholder. Manifest registration (`Added tag "tale.X.json" for "..."`) succeeds, asset traces look healthy, but every script has empty `StartNodeId`. **Fix:** `InvalidateCachesForPath` must also invalidate ancestor caches — see commit history of `JoyceCode/engine/casette/View.cs:InvalidateCachesForPath` (descendants pull this path as `Relation.Ancestor`; ancestors pull it as `Relation.Descendant`, so both directions are affected by a partial change).

After commit `1dd86134`, all three Mix include-resolution failure paths emit `Warning` (or `WARNING:` in Cmdline) instead of `Trace`. Watch the log when a conversation looks empty.

### Symptom: build-task A behaves differently from build-task B in the same build

A and B almost certainly run from different binaries. `Res2TargetTask` runs the netstandard2.0 task DLL in-process; `PackTexturesTask` and `CompileAssetsTask` spawn a published net9.0 executable child process. After editing Cmdline source, refresh **both** outputs.

```bash
dotnet build-server shutdown
dotnet publish Tooling/Cmdline/Cmdline.csproj -c Debug -f net9.0 -r win-x64 --self-contained
dotnet build   Tooling/Cmdline/Cmdline.csproj -c Debug -f netstandard2.0 -r win-x64 --no-incremental
rm nogame/generated/AndroidResources.xml nogame/generated/InnoResources.iss
dotnet build nogame/nogame.csproj
```

### Symptom: `GatherResources` skipped on build but you expected it to run

The `GatherResources` target has no `Inputs`/`Outputs` declaration, so it should run every time. If it appears to skip, MSBuild may be reusing a cached task host. `dotnet build-server shutdown` clears that.

### Symptom: PackTextures fails with SkiaSharp `BadImageFormatException`

Unrelated to includes — SkiaSharp's native lib didn't load (architecture mismatch or missing). This crash happens **after** `gc.Load()` has finished resolving includes, so the include resolution itself isn't affected.

### Note: PackTextures intentionally runs before atlases exist

The first `_loadGameConfigFile` invocation (from `PackTextures`) sees a `nogame.resources.json` that lists atlas files which haven't been baked yet. This is fine — the load only walks the texture/channel structure; missing atlas binaries surface as `Warning: resource file for ... does not exist` traces but don't abort the BFS.

---

## Adding a new asset that's referenced via `__include__`

Workflow:

1. Add the file under `models/` (or wherever).
2. Add the include directive in the appropriate parent JSON.
3. **Rebuild `nogame.csproj`** — this regenerates `nogame/generated/AndroidResources.xml` and `InnoResources.iss` with the new entry.
4. Run the game.

You should **not** need to add the file to `models/nogame.resources.json` explicitly. The auto-discovery in `Mix._upsertIncludes` handles arbitrary nesting depth. (An earlier commit, `61995e76`, did exactly this for `tale/conversations/*.json` and was reverted in `7b2f5cfd` once we confirmed the auto-discovery was correct — the problem had been a stale manifest, not a BFS bug.)

If a manifest entry is missing after a rebuild, treat it as a real bug — promote the include warning, look at the trace, and follow the BFS dequeue order to find where it stopped.

---

## See also

- `JoyceCode/engine/casette/Mix.cs` — runtime include resolver
- `Tooling/Cmdline/Mix.cs` — build-time include resolver (must stay in sync)
- `Tooling/Cmdline/GameConfig.cs` — drives the Mix BFS, populates `MapResources`
- `Tooling/Cmdline/Res2Target.cs` — emits the manifests
- `Tooling/Cmdline/AndroidResourceWriter.cs` / `InnoResourceWriter.cs` — manifest format
- `Karawan/AssetImplementation.cs` — desktop asset lookup
- `Wuka/Platforms/Android/AssetImplementation.cs` — Android asset lookup
- `Karawan/DesktopMain.cs:_determineResourcePath` — runtime resource path detection
