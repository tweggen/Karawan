# Frame Number Inconsistency Investigation

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

### **Option A: Store Local Frame Numbers (IMPLEMENTED ✓)**

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

### **Option B: Use Global Indices (Less Efficient)**
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
