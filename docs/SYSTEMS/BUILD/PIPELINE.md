# Build Pipeline & Asset Manifest

**Status**: Reference  
**Last Updated**: 2026-08-05

How the Karawan/Joyce build turns `models/*.json` and friends into shippable assets, how those assets are registered for runtime lookup, and the failure modes that aren't obvious from reading the code.

---

## NuGet versions: Central Package Management

Since WP-0.1 (2026-08-05) **every NuGet version lives in `Directory.Packages.props` at the
repository root**. A `csproj` carries `<PackageReference Include="X" />` with **no** `Version`
attribute; the version comes from a matching `<PackageVersion>` entry.

Adding or changing a package therefore means editing **two** files, not one.

Three things about this setup are not obvious and will bite:

1. **`Silk.NET.Assimp` is pinned to 2.22.0 on purpose.** Bumping it is what corrupted model
   loading. See non-negotiables N5/N8 in
   `docs/roadmap/proposed/IMPLEMENTATION-PLAN-PLATFORM-BACKEND.md`.

2. **Two packages are legitimately referenced at two different versions**, and CPM allows only
   one central version each. The odd one out uses `VersionOverride` at its call site:
   | Package | Central | Override |
   |---|---|---|
   | `SixLabors.ImageSharp` | 3.1.10 (`Karawan`, `examples/Launcher`) | `Splash.Silk` → 3.0.1 |
   | `SkiaSharp.NativeAssets.Linux` | 2.88.7 (`Karawan`, `examples/Launcher`) | `Tooling/Cmdline` → 3.119.1 |
   Unifying either one would change what resolves. Don't "tidy" them without deciding that
   deliberately.

3. **CPM silently disables MAUI's implicit package references.**
   `Microsoft.Maui.Sdk`'s `BundledVersions.targets` sets
   `DisableMauiImplicitPackageReferences=true` whenever `ManagePackageVersionsCentrally` is
   `true`. So `<UseMaui>true</UseMaui>` in `Wuka.csproj` **stops pulling in
   `Microsoft.Maui.Controls` at all** — with no warning and no error; the package simply
   vanishes from `dotnet list package`. `Wuka.csproj` therefore declares it explicitly. If a
   future MAUI package goes missing after a workload update, this is the first thing to check.

`Aihao.old/` is dead code and opts out entirely via its own `Aihao.old/Directory.Packages.props`
setting `ManagePackageVersionsCentrally=false` — NuGet resolves the *nearest* such file walking
up, so the root one never applies there.

To verify a package change didn't move anything else, diff `dotnet list <proj> package` across
all twelve managed projects before and after; that is exactly what WP-0.1's AC-0.1.3 did.

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

### Generated-file names must be derived identically on both sides

Some manifest entries are not files that exist in the source tree — they name files the bake *will* produce (`ac-{hash}` animation collections, `sc-{hash}` TALE scenarios, `mo-{hash}` models). Their names are hashes, and the hash is computed **twice** from independent copies of the code, because `Tooling/Cmdline` cannot reference `JoyceCode`:

| Artifact | Build-time (manifest) | Runtime / bake |
|---|---|---|
| `ac-{hash}` | `GameConfig.ModelAnimationCollectionFileName` + `GameConfig.LoadAnimation` | `ModelAnimationCollectionReader.ModelAnimationCollectionFileName` + `AAssetImplementation._whenLoadedAnimations` |
| `sc-{hash}` | `GameConfig.ScenarioFileName` + `LoadScenarioList` | `engine.tale.bake.ScenarioFileName.Of` + `AAssetImplementation._whenLoadedScenarios` |
| `mo-{hash}` | `GameConfig.ModelFileName` + `GameConfig.LoadResourceList` | `builtin.baking.ModelFileName.Of` + `AAssetImplementation._registerModelEntry` |

Drift between the copies is invisible on desktop (the game just re-bakes on demand) but **breaks the Android build**, where every manifest entry becomes an `<AndroidAsset Include="…"/>` that MSBuild must find on disk:

```
Quelldatei "../nogame/generated/ac-…~" wurde nicht gefunden.   (source file not found)
```

Note the hash inputs are **not just the hash function** — the JSON shape feeding it counts too. The animation-packs change (`animations.json` moved from a flat `animationUrls` string to a `packs` dict, one baked file per (model, pack) pair) updated `_whenLoadedAnimations` but not `LoadAnimation`, which kept reading the now-absent `animationUrls`. The manifest then listed one `ac-` per *model*, hashed from the model name alone, while the bake wrote one per *(model, pack)* — 12 phantom entries against 14 real files, none overlapping. Fixed 2026-08-02 by teaching `LoadAnimation` the `packs` format (returns a `List<Resource>`; legacy `animationUrls` still handled).

**Rule: any change to the shape of `/animations` or `/scenarios`, or to a hash input, must be applied to both columns in the same commit.** To verify without a full Android build:

```bash
dotnet build nogame/nogame.csproj
diff <(grep -o 'ac-[A-Za-z0-9_~-]*' nogame/generated/AndroidResources.xml | sort -u) \
     <(ls nogame/generated/ | grep '^ac-' | sort)
# and the same for mo- :
diff <(grep -o 'mo-[A-Za-z0-9_~-]*' nogame/generated/AndroidResources.xml | sort -u) \
     <(ls nogame/generated/ | grep '^mo-' | sort)
```

`mo-{hash}` has one extra hash input the others do not: the model's **load
properties**. Two call sites loading the same fbx with a different `Scale` (or
`Axis`, `AnimAxis`, …) do not get the same `Model`, so the properties are part of
the identity. They are declared beside the resource and must match what the game
passes in its `ModelCacheParams`:

```json
{ "uri": "../models/.../man_business_Rig.fbx", "type": "model",
  "modelProperties": { "Scale": "1" } }
```

Significance is decided by **exclusion**: only `AnimationUrls`, `CPUNodes` and
`ModelBaseBone` are known not to reach the persisted graph (each justified at
`ModelFileName._insignificantProperties`). Everything else counts, including a
property that does not exist yet — so adding one forces a re-bake, which is
merely wasteful, rather than silently reusing a file baked without it, which
would be wrong and invisible. This bit during WP-4.2: the game passes `Scale=1`
and the first bake did not declare it, so the runtime hashed to a name the bake
had never written.

---

## Models are baked (Phase 4)

Since WP-4.1–4.4, **fbx import is a build-time capability only.** Chushi reads
each declared model through Assimp and writes a `mo-{hash}` file; the game
deserialises that and never sees an fbx. Consequences worth knowing:

- **`libassimp.so` is not in the APK**, and `scripts/check-apk.py` fails if it
  reappears (`FORBIDDEN_LIBS`) — a runtime project re-acquiring an Assimp
  reference is easy to do by accident and otherwise invisible.
- **The importer lives in `JoyceFbx`**, referenced by `Mazu`/`Chushi` and the test
  suite and by nothing that ships. It is NOT in `Joyce`, because every runtime
  target references `Joyce` and would pick Assimp up transitively — which is what
  used to happen.
- **`ModelCache.FbxLoader`** is the seam. Chushi assigns it at startup; in the
  game it is null and asking for an unbaked fbx raises rather than silently
  working.
- **The fbx files stay in the tree** — they are the bake input. They are marked
  `"type": "model"` (ships its bake instead) or `"type": "animationSource"`
  (ships nothing; its output is the `ac-{hash}`), so they are no longer packaged.
- **Two builds are still needed** after adding a model, for the usual reason:
  `Wuka.csproj` `<Import>`s the generated manifest at project-evaluation time.

Equivalence between the two load paths is asserted per model by
`tests/JoyceCode.Tests/engine/joyce/BakedModelEquivalenceTests.cs` — geometry to
`1e-6`, bone names and **order** exactly, because `AllBakedMatrices` is indexed
`frame * NBones + boneIndex` and a reorder renders a foreign pose plausibly
instead of failing.

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

### Symptom: a subsystem works from source but is silently absent on Android / in the installer

The subsystem reads its data with `Directory.GetFiles` / `Directory.Exists` instead of `engine.Assets.Open`. A from-source desktop run has `Engine.ResourcePath = ./models/`, so the directory is really there and everything works; on Android the model tree only exists inside the APK, reachable through `AssetManager` and nothing else (`Wuka/Platforms/Android/AssetImplementation.cs`), and an installed Windows build has a flat `assets/` directory with no subdirectories at all. `Directory.Exists` returns `false` in both, and a subsystem that treats that as "feature not configured" disables itself without an error.

That is what happened to the TALE storylets (fixed 2026-08-03). `StoryletLibrary.LoadFromDirectory` enumerated `models/tale/*.json`, so nothing ever declared those files as resources and the manifests carried **zero** of them — only the 11 `tale/conversations/*.json` narration scripts, which are packaged because `nogame.narration.json` `__include__`s them. `TaleModule.OnModuleActivate` hit `!Directory.Exists(talePath)`, returned before creating `TaleManager`, and left `TaleModule.TaleManager` null while still reporting the module as active. `Scene.cs` then handed that null to `TaleSpawnOperator`, which crashed on the first per-fragment spawn poll.

**Rule: game-runtime code loads data through `engine.Assets`, never through `Directory`/`File`.** `Directory`-based loading is fine in the build tools and test harnesses (`ScenarioCompiler`, `Testbed`, `TestRunner`) — they always run on a desktop with a real model tree.

Two ways to make files reachable that way, both of which also get them into the manifests:

| Mechanism | Use when | Discovery |
|---|---|---|
| `__include__` in a parent config | The content belongs in the merged config tree (narration scripts, roles, interactions) | Automatic via `Mix.AdditionalFiles` |
| An entry in `models/nogame.resources.json` | The file is opened as a standalone document at runtime (storylets) | Explicit `uri`, optional `type` |

The storylets use the second: 14 entries tagged `"type": "taleStorylet"`. That one declaration does double duty — the resource compiler ships the files, and `TaleModule._collectStoryletTags` filters `/resources/list` on that same type to learn what to open, so what ships and what loads cannot drift apart. `tests/JoyceCode.Tests/engine/tale/TaleStoryletResourceTests.cs` fails the build if a file in `models/tale/` is missing from the list, if a declared file doesn't exist, or if a basename collides with another resource (Android flattens every asset to its basename).

### Symptom: a manifest entry is regenerated but the APK still lacks the file

`Wuka.csproj` does `<Import Project="../nogame/generated/AndroidResources.xml" />`. MSBuild evaluates `Import` at **project-evaluation time**, before any target in that build runs — so a build that regenerates the manifest is still using the copy that existed when it started. The first build after adding an asset updates the manifest; the **second** build stages it into `Wuka/obj/…/assets/`. Verify with:

```bash
ls Wuka/obj/Debug/net9.0-android36.0/assets/ | wc -l
```

### The generated manifest must NOT carry an `Sdk` attribute

`AndroidResourceWriter` opens the file with a bare `<Project>`, deliberately. Give it
`Sdk="Microsoft.NET.Sdk"` and MSBuild re-imports the SDK at the `<Import>` site — two `MSB4011`
warnings, the second of which reports that *Wuka.csproj's own* bottom `Sdk.targets` import is the
one being ignored. The .NET SDK and the Android/MAUI workloads then land in the middle of
Wuka.csproj, and every static `ItemGroup` in them sees only the items declared **above** the
import: the `libSDL3`/`libmain`/`libopenal` natives, the `AndroidResource` icons, every
`PackageReference` and every `ProjectReference` are all below it. `dotnet build` survives this
(targets read items at execution time, after evaluation has finished); IDE project evaluators need
not, and it was a live suspect for Rider's "Unable evaluate deployment properties". Regression
check — this must print nothing:

```bash
dotnet msbuild Wuka/Wuka.csproj -getProperty:RuntimeIdentifier 2>&1 | grep MSB4011
```

### Symptom: `ClassNotFoundException` on a class that IS in the APK

Seen 2026-08-08, Release only, on device after the permission dialog:

```
java.lang.ClassNotFoundException: crc64e20757511145c75a.GameActivity
  at crc64e20757511145c75a.MainActivity.n_onRequestPermissionsResult(Native Method)
```

`GameActivity` was in `classes2.dex` and correctly declared in the merged manifest. **Read the
message as naming the wrong class.** Loading a class requires resolving its superclass, and
`GameActivity extends org.libsdl.app.SDLActivity` — all 49 `org/libsdl/app/*` classes were absent,
so ART reported the *subclass* as not found. `GameSurface`/`SDLSurface` had the same break.

Cause: `Wuka/obj/Release/net9.0-android36.0/android-arm64/` dated from **Aug 2**, six days before
WP-2.2 vendored the SDL3 Java glue on Aug 8. The incremental Release build did run
`_CompileBindingJava` and did produce `binding/bin/Wuka.jar` with all 49 classes — d8 was simply
never given it. Build result: **0 errors, no relevant warnings**, signed APK, installs, launches.

Fix, and the only reliable one:

```bash
rm -rf Wuka/obj/Release Wuka/bin/Release
dotnet build Wuka/Wuka.csproj -c Release
```

A clean build carries all 49; incremental builds *after* that clean build keep them, so this is a
one-time poisoning at the commit that introduced the Java sources — not an ongoing defect.

**Do not grep the dex for a class name to check this.** A dex holds the name string of every class
it *references*, so the string is present either way. `scripts/check-apk.py` parses the
`class_defs` table, asserts the required natives and classes are there, and scans generically for
dangling superclasses — it names the genuinely missing class rather than the one in the crash:

```bash
python scripts/check-apk.py Wuka/bin/Release/net9.0-android36.0/android-arm64/de.nassau_records.silicondesert2-Signed.apk
```

Worth running after any change to `AndroidJavaSource`, native libraries, or package references —
this is the second time a green build produced an APK missing something (the first cost a whole
work package to `libSDL3.so`; see the comment in `Wuka.csproj`).

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

This applies only to files that are genuinely part of the config tree. A file the runtime opens as a standalone document — it is not `__include__`d anywhere, so the BFS never sees it — must be listed in `models/nogame.resources.json`, or it ships nowhere. See the "works from source but absent on Android" failure mode above.

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
