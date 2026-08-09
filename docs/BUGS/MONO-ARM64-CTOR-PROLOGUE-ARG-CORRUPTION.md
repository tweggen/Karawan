# Mono/ARM64: a call in a field initialiser corrupts the constructor's trailing struct argument

**Status:** root cause proven on device; workaround applied and **verified on device 2026-08-09**
(ship handling normal, runaway gone)
**Platform:** Android arm64-v8a only. Never reproduces on desktop x64.
**Runtime:** .NET 9.0.13, Mono (`libmonosgen-2.0.so`), `net9.0-android36.0`, JIT (no AOT)
**Device OS:** Linux 6.6.102-android15-8 (Android 16)
**Found:** 2026-08-09

---

## Summary

A C# **field or property initialiser containing a call** is emitted by the compiler into the
prologue of **every** constructor of that class, before the constructor body runs. On Mono/ARM64
that prologue call corrupts the constructor's **trailing value-type parameter**: it is read
**8 bytes early**, picking up the last 8 bytes of the preceding parameter.

Deterministic, bit-identical across processes, invisible on x64.

## Symptom that led here

The player ship's angular velocity ran away to ~2000 rad/s within ~100 ms of a touch-drag, then
the emergency clamp zeroed it, repeatedly. Ship uncontrollable.

## The chain, end to end

1. `engine.physics.Object` declares a field initialiser with a call:

   ```csharp
   Engine Engine = I.Get<Engine>();          // runs in EVERY constructor prologue
   ```

2. One of its constructors ends in an optional `Vector3`:

   ```csharp
   public Object(Engine engine, DefaultEcs.Entity entity, BodyInertia inertia,
                 TypedIndex shape, Vector3 Position, Quaternion Orientation,
                 Vector3 bodyOffset = default)
   ```

3. The caller omits `bodyOffset`, so it must arrive as `<0,0,0>`. Measured on entry:

   ```
   Orientation = {X:-0.0009596219 Y:0.18314148 Z:-0.0041142507 W:0.9830776}
   bodyOffset  = <-0.0041142507, 0.9830776, 0>      // == Orientation.Z, Orientation.W, 0
   ```

4. The same constructor forwards to `CreateDynamic.Execute(..., Quaternion qOrientation,
   BodyInertia inertia, ...)`, and `inertia` arrives with the identical 8-byte skew:

   ```
   passed in : [ 0.0022057407, 0, 0.0011028703, 0, 0, 0.0022057407 | 0.002 ]
   received  : [ 0, 0.9740994, 0.0022057407, 0, 0.0011028703, 0 | 0 ]
                 └─ quat.Z, quat.W ─┘ └──── correct[0..4], shifted right by two ────┘
   ```

5. That tensor is **indefinite** — a 0.974 off-diagonal against ~zero diagonals, eigenvalues
   ≈ ±0.974 instead of 2.2058E-3, a ratio of **441.6**. So the hover controller's damping and
   limiter terms — computed correctly — were amplified ~442x and partially **reversed**. The
   correction drove the spin instead of opposing it.

## Minimal reproducer

`JoyceCode/engine/physics/AbiProbe.cs`, case M. Thirteen cases run at startup; **only M fails**,
and M is the only one with a constructor prologue.

```csharp
private static T _genericGet<T>() where T : class, new() => new T();

private sealed class ProbeCtorWithPrologue
{
    private readonly object _fromGenericCall = _genericGet<object>();   // <-- the trigger
    public Vector3 SomeOffset { get; set; } = Vector3.Zero;
    public Quaternion SomeRotation { get; set; } = Quaternion.Identity;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public ProbeCtorWithPrologue(
        object engine, DefaultEcs.Entity entity, ProbeInertia32 s, int shape,
        Vector3 position, Quaternion orientation,
        Vector3 bodyOffset = default)     // <-- arrives as <0, orientation.W, 0>
    { ... }
}
```

Device output:

```
ABIPROBE M FAIL: incomingStruct=ok
                 incomingBodyOffset=<0, 0.9740994, 0>   (expected <0,0,0>)
                 forwardedStruct=ok
                 orientation={X:0 Y:0.22612044 Z:0 W:0.9740994}
```

`0` and `0.9740994` are exactly `orientation.Z` and `orientation.W`.

## What was ruled out — all by measurement, not argument

| hypothesis | how it died |
|---|---|
| Declared `StructLayout Size` (32) exceeding natural extent (28) | Replaced `Size = 32` with an explicit padding field; `sizeof` verified still 32/64; corruption unchanged |
| `RigidPose` affected too (same 28→32 shape) | Stored pose measured **exact** |
| Struct size > 16 bytes | A 12-byte `Vector3` is corrupted identically |
| Argument position after HFAs | Probe case A (same position) passes |
| Stack spilling past exhausted integer registers | Probe case F passes |
| 16-byte alignment (`alignas(16)` analogue) | Probe case G passes |
| Misaligned stack pointer | Measured `align16=0 / OK` at all three sites |
| Stale build artefacts / incremental compilation | Full clean rebuild (44 `obj`/`bin` dirs across app **and** dependency source); unchanged. Build identity now stamped via MVID + `probeRev` |
| Concurrency / lock discipline | Full audit found every Bepu mutation correctly locked; corruption is deterministic and single-threaded |

Probe cases A–L (size, layout, position, spilling, alignment, `this`, preceding struct, three
aggregates over the SIMD budget, compiler-supplied defaults, arguments produced by calls) **all
pass**. Only M, with a prologue, fails.

## Secondary failure mode

Passing the same argument as `in` (readonly reference) instead of by value does **not** fix it —
it crashes, reproduced twice:

```
Fatal signal 11 (SIGSEGV), code 1 (SEGV_MAPERR), fault addr 0x678c1e00000000
  #01 libmonosgen-2.0.so
  #03 libmonosgen-2.0.so (mono_runtime_invoke_checked+140)
```

Consistent with a *pointer* read from the same 8-byte-skewed slot.

## Fix

Do not put calls in field or property initialisers of classes whose constructors take a trailing
value-type parameter. Assign in each constructor body instead:

```csharp
- Engine Engine = I.Get<Engine>();
+ Engine Engine;                       // assigned in every constructor body
```

Applied to `engine.physics.Object` (5 constructors; 3 already received an `Engine` parameter they
were ignoring).

### It takes a *real* call — intrinsics are fine

`Object` still has `public Vector3 BodyOffset { get; set; } = Vector3.Zero;` and
`public Quaternion BodyRotation { get; set; } = Quaternion.Identity;`. Both are static **property**
getters, so both are `call` in IL, and both remain in the prologue of all three
`Vector3 bodyOffset = default` constructors. The device is nevertheless correct after the fix.

So the JIT intrinsifies those to a zeroing before the prologue's frame shape is fixed, and only a
call that survives as an actual `bl` triggers the corruption. Useful, because it means the
near-universal `= new()` / `= Vector3.Zero` idioms are not all landmines — but it is an empirical
result on one runtime version, not a guarantee.

## Blast radius

Not physics-specific. Exposure needs **both**:

1. an **instance** field/property initialiser containing a call that is not intrinsified, and
2. a constructor whose **last** parameter is a value type.

Three things narrow it usefully:

- `static` initialisers are irrelevant — they compile into `.cctor`, not an instance prologue.
- Constructors chaining `: this(...)` are exempt — the compiler does not re-emit initialisers.
- Condition 2 is the one actually under our control, and `= new()` lock objects are near-universal
  here. So the enforceable house rule is **"never make the last constructor parameter a struct"**,
  not "avoid call initialisers".

Optional trailing struct parameters (`= default`) are the worst case: the value is supplied by the
compiler at the call site, so nobody is watching it.

Known collateral before the fix: **every** `engine.physics.Object` in the game stored a corrupt
`BodyOffset` of `<Orientation.Z, Orientation.W, 0>`, which `ApplyPosesSystem` subtracts from every
pose each frame (`vPosition -= po.BodyOffset`). Small, wrong, and previously invisible.

## Codebase sweep

A brace-aware scan of 925 files / 1151 type declarations, with parameter types resolved against
struct and enum declarations harvested from this repo and the sibling dependency repos.

**Both conditions met, besides the fixed `Object` and the deliberate `AbiProbe` repro — 2 sites,
neither reachable on ARM64 today:**

| site | trailing param | why it is not biting |
|---|---|---|
| `engine/EntityComponentTypeReader.cs:47` | `DefaultEcs.Entity` (sole param, so the early read lands on `this`) | constructed only from `ui/EntityInspector.cs:26`, a desktop ImGui debug panel |
| `engine/tale/JsonlEventLogger.cs:24` | `DateTime` after a `string` | constructed only from `Testbed/TestbedMain.cs:342`, desktop-only offline DES harness |

**Value-type-adjacent, unproven but same shape** — 4-byte enums trailing a call initialiser:
`builtin/modules/satnav/LocalPathfinder.cs:291` (optional `TransportationType`, the high-risk
form), `Splash/MeshBatch.cs:145` (hot render path), `Splash.Silk/SkTexture.cs:719`.

**Latent — 309 classes** have a call initialiser but no trailing value-type parameter; 117 of them
declare a constructor and are one signature change away. Includes `engine/Engine.cs`,
`engine/physics/API.cs`, `engine/world/Fragment.cs`, `Splash.Silk/Platform.cs`.

**Most fragile declaration in the tree:**
`engine/navigation/TemporalConstraintState.cs:8` —
`public record TemporalConstraintState(bool CanAccess, TimeSpan UntilChange)`. Trailing parameter is
a struct, and it already has two property initialisers; both are bare parameter reads today, so
adding any call to either reproduces the confirmed pattern exactly.

Also checked and clean: no partial-class exposure (call initialiser and struct-trailing constructor
never split across files); all 4 structs with instance initialisers declare only a parameterless
constructor.

## Regression detection

`AbiProbe.RunOnce()` is called from `CreateDynamic.Execute` — once per process, on the first dynamic
body, which every dynamic body in the game passes through. It is **silent** in the expected state.
It speaks only when the picture changes:

| outcome | meaning | level |
|---|---|---|
| A–L pass, M fails | expected; the runtime defect is still present and worked around | Trace |
| A–L pass, **M passes** | runtime fixed upstream — revisit the workarounds | Warning |
| any of A–L fails | broader than the known defect; treat every struct-carrying call as suspect | Warning |

## Prior art

Same failure mode in a different compiler — MSVC ARM64, "Incorrect code generation for Arm64 code
with aligned structs pushed to parameter stack"
(<https://developercommunity.visualstudio.com/t/Incorrect-code-generation-for-Arm64-code/10576646>,
Closed - Fixed). There the caller stored at `[sp,#0x20]` and the callee read `0x18` — the same
one-slot, 8-byte discrepancy, also x64-clean / ARM64-broken.

No matching .NET issue was found. Four candidate issue numbers were checked and are **unrelated**:
`dotnet/runtime#88220` (COM source generation), `dotnet/runtime#91432` (Options source gen),
`dotnet/runtime#71833` (ServicePointManager obsoletion), `dotnet/aspnetcore#55817` (Blazor JS
interop). Worth filing upstream against `dotnet/runtime` (Mono ARM64 JIT).

## How to re-verify

1. Deploy a Debug build to an arm64 Android device.
2. `adb logcat -s DOTNET:*` and check `ABIPROBE BUILD: probeRev=… mvid=…` matches the build under
   test — the MVID changes on every compilation, so a stale deploy is detectable.
3. `ABIPROBE RESULT:` — case M is the signal. All other cases are controls and must pass.
