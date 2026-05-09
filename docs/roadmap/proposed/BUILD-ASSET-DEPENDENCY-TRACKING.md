# Reliable Asset-Bake Dependency Tracking

## Context

The `nogame.csproj` build chains three custom MSBuild targets before `Compile`:

```
EnsureGeneratedDirectory → CompileAssetsHost (Chushi)
                        → GatherTexturesHost (texture packer)
                        → GatherResources (resource compiler)
                        → Compile
```

`CompileAssetsHost` invokes Chushi to bake `ac-{hash}` animation collections and
`sc-{hash}` scenarios into `nogame/generated/`. `GatherTexturesHost` runs the
texture packer.

Two recent issues exposed weakness in this pipeline:

1. **Orphaned child process (now fixed).** Both `CompileAssetsTask.cs` and
   `PackTexturesTask.cs` had `process.WaitForExit(1000 * 10)` and always
   returned `true` regardless of exit code. A fresh Chushi bake takes far
   longer than 10 seconds, so MSBuild moved on before the bake was complete
   and `Res2TargetTask` snapshotted an incomplete `nogame/generated/`. The
   runtime then logged a `FileNotFoundException` per missing `ac-{hash}` file
   and fell back to in-process baking via `Model.BakeAnimations`. Fixed in
   this branch — both tasks now `WaitForExit()` with no timeout and
   `return process.ExitCode == 0`.

2. **MSBuild can't see asset sources.** Even with the timeout fixed, the
   chain only fires when `Compile` runs. `Compile` only runs when MSBuild's
   project-level "is up-to-date" check decides something needs rebuilding.
   That check considers `<Compile>` items (the `.cs` files) and references —
   it does **not** consider `models/**/*.json`, `models/**/*.fbx`, or any
   other asset source. So:

   - Edit `models/nogame.animations.json` → no `.cs` change → MSBuild skips
     the entire chain → stale `nogame/generated/`.
   - Drop a new `.fbx` referenced from JSON → same outcome.

   Chushi's per-file incremental check (`_IsAnimationUpToDate`,
   `_IsScenarioUpToDate` in `Chushi/ConsoleMain.cs`) is solid — it just
   never gets invoked when the host chain is skipped at the MSBuild layer.

This proposal adds two small layers so that asset edits reliably trigger
the bake, while no-op builds remain near-instant.

---

## Design

### Layer 1 — `<UpToDateCheckInput>` items

Add to `nogame.csproj` so that both `dotnet build`'s incremental machinery
and VS's fast up-to-date check treat asset sources as build inputs:

```xml
<ItemGroup>
    <UpToDateCheckInput Include="..\models\**\*.json" />
    <UpToDateCheckInput Include="..\models\**\*.fbx" />
    <UpToDateCheckInput Include="..\models\**\*.glb" />
    <UpToDateCheckInput Include="..\models\**\*.obj" />
    <UpToDateCheckInput Include="..\models\**\*.png" />
</ItemGroup>
```

Effect: editing any of these files marks the project as out-of-date, so
`Compile` runs, so the asset bake chain fires.

### Layer 2 — Stamp-file Inputs/Outputs on the bake targets

Currently both `CompileAssetsHost` and `GatherTexturesHost` run
unconditionally whenever they're reached. Give them explicit `Inputs` and
`Outputs` with stamp files so MSBuild can skip them when nothing has
changed.

```xml
<ItemGroup>
    <BakeAssetInputs Include="..\models\**\*.json" />
    <BakeAssetInputs Include="..\models\**\*.fbx" />
    <BakeAssetInputs Include="..\models\**\*.glb" />
    <BakeAssetInputs Include="..\models\**\*.obj" />

    <BakeTextureInputs Include="..\models\**\*.png" />
    <BakeTextureInputs Include="..\models\**\textures*.json" />
</ItemGroup>

<Target Name="CompileAssetsHost"
        BeforeTargets="GatherResources"
        Inputs="@(BakeAssetInputs)"
        Outputs="..\nogame\generated\.bake-assets.stamp">
    <Message Text="compiling assets ($(HostRid))" />
    <CompileAssetsTask OutputDirectory="../nogame/generated"
                       GameJson="../models/nogame.json"
                       Executable="..\Chushi\bin\Debug\net9.0\$(HostRid)\publish\Chushi$(ExeSuffix)" />
    <Touch Files="..\nogame\generated\.bake-assets.stamp" AlwaysCreate="true" />
</Target>

<Target Name="GatherTexturesHost"
        BeforeTargets="GatherResources"
        Inputs="@(BakeTextureInputs)"
        Outputs="..\nogame\generated\.bake-textures.stamp">
    <Message Text="gathering textures ($(HostRid))" />
    <PackTexturesTask OutputDirectory="../nogame/generated"
                      GameJson="../models/nogame.json"
                      Executable="..\Tooling\Cmdline\bin\Debug\net9.0\$(HostRid)\publish\joycecmd$(ExeSuffix)" />
    <Touch Files="..\nogame\generated\.bake-textures.stamp" AlwaysCreate="true" />
</Target>
```

Effect: when reached, MSBuild compares any input's mtime against the stamp
file. If all inputs are older, the target is skipped without spawning the
child process at all — MSBuild prints `Skipping target "CompileAssetsHost"
because all output files are up-to-date with respect to the input files`.
If anything is newer, Chushi runs (and its own per-file check determines
which artifacts need re-baking), then the stamp is touched.

`Touch` is a built-in MSBuild task; no new dependencies.

### Why two layers and not one

- `UpToDateCheckInput` alone makes MSBuild trigger the chain on JSON/FBX
  edits, but the targets still run unconditionally inside the chain, paying
  one Chushi spawn even when Chushi's per-file check would skip everything.
- `Inputs/Outputs` on the targets alone make individual targets skip
  cheaply, but only when the chain is reached — which won't happen on an
  asset-only edit.

Together: edits trigger the chain (layer 1); individual targets skip
cleanly when their slice is up-to-date (layer 2); no-op builds skip
everything at the MSBuild project level.

### Edge cases & caveats

1. **Glob evaluation.** Item globs are evaluated at MSBuild item-evaluation
   time. New files appear correctly. Renames/deletions don't always
   invalidate — MSBuild may see the *remaining* files, all older than the
   stamp, and skip. The stamp-file mechanism handles updates well; deletes
   are an SDK-level quirk that affects all glob-based incremental MSBuild.
   Acceptable: a deletion that should retire a baked artifact is rare, and
   a one-time `Remove-Item nogame\generated\.bake-*.stamp` from PowerShell
   forces re-bake.
2. **Stamp file must not be cleaned by anything else.** It lives in
   `nogame/generated/`, the same directory as `ac-*` and `sc-*` artifacts.
   Already gitignored (entire `generated/` directory). No additional
   `.gitignore` entries needed.
3. **First build after this change** will re-bake everything (no stamp
   file exists yet). That's correct behavior — a fresh checkout's first
   build also re-bakes everything.
4. **Chushi's internal incremental check stays in place.** The two layers
   above eliminate *unnecessary Chushi spawns*; Chushi's
   `_IsAnimationUpToDate` / `_IsScenarioUpToDate` continues to skip
   *individual artifacts* within a single Chushi run. Both are needed:
   layer 2 prevents spawning Chushi at all when nothing changed; Chushi's
   own check minimizes work when one source out of many has changed.
5. **`BeforeTargets="BeforeBuild"` alternative considered, rejected.**
   Could move bake targets to `BeforeBuild` so they always fire regardless
   of `Compile` skipping. Rejected because (a) `UpToDateCheckInput` already
   solves the trigger problem and (b) `BeforeBuild` would force a Chushi
   spawn on every `dotnet build` even when the project is fully up-to-date,
   defeating MSBuild's incremental machinery.

---

## Files to modify

| File | Change |
|---|---|
| `nogame/nogame.csproj` | Add `<UpToDateCheckInput>` items for `models/**` asset sources. Add `<BakeAssetInputs>` and `<BakeTextureInputs>` ItemGroups. Add `Inputs`/`Outputs` attributes to `CompileAssetsHost` and `GatherTexturesHost` targets, plus `<Touch>` calls to stamp files. |

No code changes. No new dependencies. No JSON-schema changes.

The `EnsureGeneratedDirectory` target stays as-is — it's a precondition for
the stamp files themselves to be writable.

---

## Verification

1. **Clean build:** `Remove-Item nogame\generated\* -Force; dotnet build nogame\nogame.csproj` — should re-bake everything, write both stamp files, complete normally.
2. **No-op build:** immediately re-run `dotnet build nogame\nogame.csproj` — both bake targets should print `Skipping target "..." because all output files are up-to-date`. No Chushi or texture-packer process spawn (verify with Process Explorer or by checking `nogame/generated` mtimes).
3. **JSON-only edit:** touch `models/nogame.animations.json`, run `dotnet build nogame\nogame.csproj` — `CompileAssetsHost` runs, `GatherTexturesHost` skips. Inside Chushi, `_IsAnimationUpToDate` returns true for unchanged entries, false for affected ones.
4. **FBX edit:** touch a model `.fbx`, build — same as above, animation collection containing that model is re-baked.
5. **Texture edit:** touch a `.png` under `models/`, build — `GatherTexturesHost` runs, `CompileAssetsHost` skips.
6. **Runtime check:** after a successful build, launch the game (`dotnet run --project Karawan/Karawan.csproj`) and verify no `FileNotFoundException` warnings of the form `Could not find file ...\nogame\generated\ac-...~` appear in the log.

---

## Out of scope (future follow-ups)

- Wuka/Android build path: `Wuka/obj/.../assets/` has its own copy of
  `ac-*` files, presumably staged from `nogame/generated/` by a separate
  Android target. Verify that path picks up the fixed bake outputs; not
  expected to be broken by this change but worth confirming the first time
  Wuka is rebuilt.
- The duplicate-Process pattern in `CompileAssetsTask.cs` /
  `PackTexturesTask.cs` (called out by the existing TODO at the top of
  `CompileAssetsTask.cs`: "This is copy paste from PackTexturesTask, why
  dont we superclass it?") could be extracted into a shared base class.
  Not required for this fix.
