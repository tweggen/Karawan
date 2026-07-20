# Frame Number Inconsistency Investigation

> **SUPERSEDED (2026-07-20).** The analysis below is sound but its survey of consumers is
> incomplete, and the fix it recommends (Option A, implemented as commit `c3f40eeb`) caused a
> regression: walking characters played the fall animation, and idle alternated between falling
> and standing.
>
> **What it missed:** `FrameNos` is not consumed only by `SilkThreeD`'s uniform path. It is
> *also* uploaded verbatim as the `instanceFrameno` vertex attribute under the **SSBO** strategy,
> where `models/shaders/LIghtingVS.vert` computes `instanceFrameno * nBones + boneId` with **no
> `FirstFrame` term**. The SSBO strategy is the live one on OpenGL ≥ 4.3 — i.e. on most desktops —
> so switching `FrameNos` to local frame numbers made every clip read from the start of
> `AllBakedMatrices`, which holds whichever animation sorts first by name (`Death_FallForwards`).
> The double-add it fixed was real, but only on a path that platform never takes.
>
> **Resolution: Option B was the correct one.** `FrameNos` now always carries the **global** baked
> frame number, produced by `ModelAnimation.GetGlobalFrame(localFrameno)` in `MeshBatch.Add`, and
> the uniform/UBO paths no longer add `FirstFrame` a second time. All three strategies now agree
> on a single meaning for the value. See `ModelAnimation.cs` and `MeshBatch.cs` for the contract.
>
> **Lesson for future edits here:** before changing what `FrameNos` means, enumerate *every*
> consumer — the two CPU-side slice sites in `SilkThreeD`/`SilkRenderState` **and** the GLSL that
> reads the vertex attribute — and check which strategy the target platform actually selects
> (`SilkThreeD`'s constructor, mirrored in `ShaderSource`).

## Problem Summary

There is an inconsistency in how frame numbers are interpreted in the rendering pipeline, causing array out-of-bounds exceptions in `SilkThreeD.cs:510`.

## Data Flow Analysis

### 1. **Definition: What is a Frame Number?**

- **Local Frame Number**: Position within a single animation (0 to `NFrames-1`)
- **Global Frame Number**: Absolute index in the shared `AllBakedMatrices` array

Each `ModelAnimation` has:
- `FirstFrame`: Starting offset in `AllBakedMatrices` 
- `NFrames`: Count of frames for this animation
- Valid global indices for this animation: `[FirstFrame, FirstFrame + NFrames)`

### 2. **Current Data Flow**

#### CameraOutput.cs:293 - Computation
```csharp
ModelAnimation ma = animState.ModelAnimation;
globalFrameno = ma.FirstFrame + cGpuAnimationState.AnimationState.ModelAnimationFrame;
// Variable named "globalFrameno" but contains: FirstFrame + LocalFrame
// This is actually a global index, correctly computed
```

- **Input**: `ModelAnimationFrame` = local frame number (0 to NFrames-1)
- **Output**: `globalFrameno` = FirstFrame + LocalFrame (correct global index)
- **Issue**: Variable name says "global" but we're later treating it as local

#### MeshBatch.cs:132 - Storage
```csharp
animationBatch.FrameNos.Add(globalFrameno);
// Stores: FirstFrame + LocalFrame
```

- **Stores**: The computed `globalFrameno` (which is actually `FirstFrame + LocalFrame`)
- **Line 90 TODO**: "TXWTODO: There is a misalignment of frame number here." (Pre-existing comment!)

#### SilkThreeD.cs:510 - Usage
```csharp
16 * (int)(modelAnimation.FirstFrame + frameno) * nBones
// Where frameno comes from FrameNos array
// Computes: FirstFrame + (FirstFrame + LocalFrame) = 2*FirstFrame + LocalFrame
```

- **Input**: `frameno` from `FrameNos` = `FirstFrame + LocalFrame`
- **Computation**: `FirstFrame + frameno` = **FirstFrame + (FirstFrame + LocalFrame) = 2×FirstFrame + LocalFrame**
- **Result**: Tries to access beyond valid animation frames → **Array out of bounds**

### 3. **Expected Behavior**

The indexing into `AllBakedMatrices` should compute:
```
index = FirstFrame + LocalFrameNumber
```

But currently computes:
```
index = FirstFrame + (FirstFrame + LocalFrameNumber)  ← WRONG (double-adds FirstFrame)
```

## Root Cause

**Semantic conflict**:
- `CameraOutput.cs` computes and names the value `globalFrameno` (meaning: already has FirstFrame added)
- `SilkThreeD.cs` assumes `frameno` is a **local** frame number (and adds FirstFrame itself)
- The middle layer (`MeshBatch.FrameNos`) passes data without clarifying which interpretation it contains

## The Fix: Two Options

### **Option A: Store Local Frame Numbers (IMPLEMENTED, THEN REVERTED ✗)**

> Implemented as `c3f40eeb`, reverted 2026-07-20 — see the note at the top of this file.
> This option is incorrect: it breaks the SSBO strategy, which reads `FrameNos` raw.


Changed `CameraOutput.cs:284-296`:

```csharp
// Before (WRONG)
uint globalFrameno = 0;
// ...
globalFrameno = ma.FirstFrame + cGpuAnimationState.AnimationState.ModelAnimationFrame;
meshBatch.Add(aAnimationsEntry, animState, globalFrameno, matrix, _frameStats);

// After (CORRECT)
uint localFrameno = 0;
// ...
localFrameno = cGpuAnimationState.AnimationState.ModelAnimationFrame;
meshBatch.Add(aAnimationsEntry, animState, localFrameno, matrix, _frameStats);
```

Also updated `MeshBatch.cs`:
- Changed parameter name from `globalFrameno` to `localFrameno`
- Renamed internal variable from `localFrameno` to `batchFrameno` for clarity in batching logic
- Removed the TODO comment that noted this misalignment
- Line 132 now stores the local frame number directly

**Result**: 
- ✓ `FrameNos` contains local frame numbers (0 to NFrames-1)
- ✓ `SilkThreeD.cs:510` works correctly with the snapshot values
- ✓ Race condition protection via snapshots remains intact

### **Option B: Use Global Indices (CORRECT — IMPLEMENTED 2026-07-20 ✓)**
Change `SilkThreeD.cs:510` to expect pre-computed global indices:

```csharp
// CameraOutput.cs:293 stays the same
uint globalFrameno = ma.FirstFrame + cGpuAnimationState.AnimationState.ModelAnimationFrame;

// SilkThreeD.cs:510 changes
16 * (int)(frameno) * nBones  // frameno is now fully global
```

**Pros**: 
- No re-computation needed in SilkThreeD
- Variable names are clear

**Cons**:
- Harder to understand which frame belongs to which animation at usage site
- Loses the semantic connection to the animation

## Recommendation

**Implement Option A** because:
1. The TODO comment in `MeshBatch.cs:90` suggests this was a known issue
2. SilkThreeD.cs logic is already set up correctly for local frames
3. Only change needed: one line in CameraOutput.cs
4. Rename the variable for clarity

## Verification Points

After fix, verify:
1. ✅ `FrameNos` values are in range `[0, Animation.NFrames)`
2. ✅ `FirstFrame + FrameNos[i]` always falls within `AllBakedMatrices` bounds
3. ✅ Array out-of-bounds exceptions in SilkThreeD.cs no longer occur
4. ✅ Animation frames render correctly (no visual glitches)

## Affected Files

- `Splash/CameraOutput.cs` - Line 293-296
- `Splash/MeshBatch.cs` - Line 90 (TODO comment, kept for reference)
- `Splash.Silk/SilkThreeD.cs` - Line 510 (no change needed)
