# Animation Assimp Version Compensation Plan

**Status:** Planning phase  
**Date Created:** 2026-04-23  
**Context:** Assimp 5.4.1 → 6.0.2 upgrade broke FBX animation loading. Implementing version-specific compensation code to support both versions.

## Overview

The goal is to:
1. Create an enum to track Assimp version being used
2. Implement version-aware compensation code in FBX loader and animation baking
3. Allow the codebase to handle both Assimp 5.4.1 and 6.0.2 (and future versions)
4. Document exact differences between versions

## Problem Statement

**Assimp 5.4.1 (used via Silk.NET 2.22.0):** Animations work correctly  
**Assimp 6.0.2 (used via Silk.NET 2.23.0):** Animations display corrupted (garbage polygons, swirled rotations)

### Known Changes Between 5.4.1 and 6.0.2

From commit analysis and issue tracking:

1. **PreRotation/PostRotation handling** (commit ce0a50e, Oct 2024)
   - Fixed: Removed PreRotation/PostRotation from `NeedsComplexTransformationChain()` check
   - Status: INCLUDED in 6.0.2
   - Impact: Should improve animation correctness

2. **Bind pose calculation** (commit 384db86, Nov 2023)
   - Changed: `Transform()` → `TransformLink().Inverse()`
   - Status: Unclear if in 5.4.1 or 6.0.2
   - Impact: Affects vertex deformation during animation

3. **Animation keyframe insertion** (Issue #6330)
   - Behavior: Extra rotation keys inserted for rotations > 180°
   - Status: Likely in 6.0.2
   - Impact: Distorts animation paths

4. **Time scaling and frame counting**
   - Changed: Frame duration calculation (unclear exact change)
   - Status: Mentioned in multiple issues, exact change unclear
   - Impact: Could affect FirstFrame offsets and frame counts

5. **Interpolation mode for animation keys**
   - Changed: 6.0.0 introduced "interpolation mode to vector and quaternion keys"
   - Status: In 6.0.2
   - Impact: May affect how frames between keyframes are interpolated

## Architecture Plan

### Step 1: Create Version Enum

**File:** `JoyceCode/engine/joyce/AssimpVersion.cs`

```csharp
namespace engine.joyce;

public enum AssimpVersion
{
    Assimp5_4_1,  // Silk.NET 2.22.0
    Assimp6_0_2,  // Silk.NET 2.23.0
}
```

### Step 2: Detect Runtime Version

**Location:** `JoyceCode/builtin/loader/fbx/FbxModel.cs` or new file `JoyceCode/builtin/loader/fbx/AssimpVersionDetector.cs`

Need to detect which version of Assimp is loaded at runtime:
- Query a simple animation property that differs between versions
- Or read from build-time constant (set in .csproj based on package version)

**Proposal:** Add static method `AssimpVersionDetector.GetVersion()` that:
1. Checks Silk.NET.Assimp package version from reflection
2. Caches result as static property
3. Returns `AssimpVersion` enum

### Step 3: Identify Compensation Points

Based on investigation, these are the code locations that need version-aware compensation:

#### A. Animation Frame Count Calculation

**File:** `JoyceCode/engine/joyce/ModelAnimationCollection.cs` in `BakeAnimations()`

**Current code (lines ~514-520):**
```csharp
float duration = ma.Duration;
uint nFrames = UInt32.Max((uint)(duration * 60f), 1);
ma.NFrames = nFrames;
```

**Issue:** Duration value from Assimp may be interpreted differently
- 5.4.1: Duration in seconds? frames? ticks?
- 6.0.2: Different interpretation?

**Compensation needed:**
- Read both `ma.Duration` and `ma.TicksPerSecond` from loaded animation
- Apply version-specific scaling/interpretation
- Document exact units for each version

#### B. FirstFrame Offset Calculation

**File:** `JoyceCode/engine/joyce/ModelAnimationCollection.cs` in `BakeAnimations()` first pass

**Current code (lines ~510-520):**
```csharp
uint currentFrameOffset = 0;
foreach (var kvp in MapAnimations)
{
    ModelAnimation ma = kvp.Value;
    // ... calculate nFrames ...
    ma.FirstFrame = currentFrameOffset;
    currentFrameOffset += nFrames;
}
```

**Issue:** If frame count calculation differs, offsets will be wrong
- 5.4.1: Frames calculated as `duration * 60`
- 6.0.2: May need different multiplier or scaling

**Compensation needed:**
- Make frame count calculation version-aware
- Add debug output showing calculated offsets per version
- Validate total AllBakedMatrices size matches expected

#### C. Keyframe Data Reading

**File:** `JoyceCode/builtin/loader/fbx/FbxModel.cs` in animation channel loading

**Issue:** How keyframe time values are interpreted
- 5.4.1: Keyframe times in what units?
- 6.0.2: Changed interpolation mode for keys

**Compensation needed:**
- Check if keyframe count differs
- Verify keyframe time scaling
- Possibly skip or adjust extra keyframes inserted by 6.0.2

#### D. Bone Offset Matrix

**File:** `JoyceCode/builtin/loader/fbx/FbxModel.cs` in bone loading

**Issue:** Bind pose calculation changed
- 5.4.1: Used `Transform()`?
- 6.0.2: Uses `TransformLink().Inverse()`?

**Compensation needed:**
- May need to re-inverse the offset matrix for 5.4.1
- Or apply additional transformation for 6.0.2
- Compare bone transform values between versions on same model

#### E. Animation Playback / Rendering

**File:** `Splash.Silk/SilkRenderState.cs` lines ~65-69

**Current code:**
```csharp
Span<Matrix4x4> span = model.AnimationCollection.AllBakedMatrices.AsSpan()
    .Slice((int)(modelAnimation.FirstFrame + frameno) * nBones, nBones);
```

**Issue:** If frame indexing differs, rendering reads wrong matrices
- Must match frame calculation from baking

**Compensation needed:**
- Ensure FirstFrame offsets are correct (depends on baking fixes)
- May need to adjust frameno calculation based on version

### Step 4: Implementation Sequence

1. **Create enum and detector** (1 session)
   - `AssimpVersion.cs` with enum
   - `AssimpVersionDetector.cs` with runtime detection
   - Add unit test to verify detection

2. **Profile both versions** (1-2 sessions)
   - Load same FBX model on both Assimp versions
   - Log: Duration, TicksPerSecond, NFrames, FirstFrame values
   - Compare bone transforms and keyframe data
   - Create test spreadsheet with exact values

3. **Implement frame count compensation** (1 session)
   - Add version-aware multiplier/scaling
   - Create helper function: `GetFrameCount(duration, ticksPerSecond, assimpVersion)`
   - Add debug output per version

4. **Implement FirstFrame offset compensation** (1 session)
   - Ensure offset calculation matches frame count version
   - Add validation: `TotalFrames * NBones == AllBakedMatrices.Length`
   - Add detailed debug logging

5. **Investigate keyframe differences** (1-2 sessions)
   - Count keyframes per bone per animation on both versions
   - Check for extra inserted keyframes (Issue #6330)
   - Determine if we need to skip/merge keyframes for 6.0.2

6. **Investigate bone offset matrix** (1 session)
   - Load same model on both versions
   - Compare `bone.mOffsetMatrix` values
   - Determine if transformation needed for one version

7. **Test and validate** (1 session)
   - Run animations on both versions
   - Compare visual output (should be identical)
   - Test idle, walk, run, jump animations

## Testing Strategy

### Unit Tests

Create `tests/JoyceCode.Tests/AnimationAssimpVersionTests.cs`:

```csharp
[TestClass]
public class AnimationAssimpVersionTests
{
    [TestMethod]
    public void CanDetectAssimpVersion()
    {
        var version = AssimpVersionDetector.GetVersion();
        Assert.IsNotNull(version);
    }
    
    [TestMethod]
    public void FrameCountCalculationMatches()
    {
        // Load same test FBX on both versions
        // Assert frame counts are equal or properly compensated
    }
    
    [TestMethod]
    public void FirstFrameOffsetsCorrect()
    {
        // Verify AllBakedMatrices allocation size
        // Verify FirstFrame values are sequential without overlap
    }
    
    [TestMethod]
    public void AnimationPlaysIdenticallyOnBothVersions()
    {
        // Load model, bake animations
        // Compare bone matrices at each frame
        // Assert identical (within floating point epsilon)
    }
}
```

### Manual Tests

1. **Visual regression test:**
   - Switch between 2.22.0 and 2.23.0 Silk.NET in Joyce.csproj
   - Rebuild and run game
   - Visually confirm animations match
   - Document any differences

2. **Performance test:**
   - Measure baking time on both versions
   - Ensure compensation code doesn't slow things down

## Debugging Tools

Add to `JoyceCode/engine/joyce/ModelAnimationCollection.cs`:

```csharp
public void DumpAnimationStatistics()
{
    Trace(_dc, $"=== Animation Statistics for {_model.Name} ===");
    Trace(_dc, $"Assimp Version: {AssimpVersionDetector.GetVersion()}");
    Trace(_dc, $"Total animations: {MapAnimations.Count}");
    Trace(_dc, $"Total frames: {_nextAnimFrame}");
    Trace(_dc, $"AllBakedMatrices size: {AllBakedMatrices?.Length}");
    Trace(_dc, $"Skeleton bones: {_model.Skeleton.NBones}");
    
    foreach (var kvp in MapAnimations)
    {
        var ma = kvp.Value;
        Trace(_dc, $"  {kvp.Key}: Duration={ma.Duration}, TicksPerSecond={ma.TicksPerSecond}, " +
            $"NFrames={ma.NFrames}, FirstFrame={ma.FirstFrame}");
    }
}
```

Call this in `BakeAnimations()` when debug output is enabled.

## Open Questions for Investigation

1. **What is the exact Duration value format?**
   - Is it seconds in both versions?
   - Or frames in one version?
   - Or ticks that need TicksPerSecond conversion?

2. **Does Assimp 6.0.2 insert extra keyframes?**
   - If yes, should we filter them out?
   - Or adjust frame count accordingly?

3. **What changed in bone offset matrix calculation?**
   - Do we need to re-inverse for 5.4.1?
   - Or apply additional transform for 6.0.2?

4. **Are there changes to skeletal hierarchy or bone indices?**
   - Could affect which bone gets which animation data

## Files to Modify

1. **New files:**
   - `JoyceCode/engine/joyce/AssimpVersion.cs` - enum
   - `JoyceCode/builtin/loader/fbx/AssimpVersionDetector.cs` - detection logic
   - `docs/ANIMATION_ASSIMP_COMPENSATION.md` - detailed findings (this file's follow-up)

2. **Existing files:**
   - `JoyceCode/engine/joyce/ModelAnimationCollection.cs` - BakeAnimations() method
   - `JoyceCode/builtin/loader/fbx/FbxModel.cs` - FBX loading logic
   - `Joyce/Joyce.csproj` - potentially document Assimp version used
   - `tests/JoyceCode.Tests/JoyceCode.Tests.csproj` - add new tests

## Success Criteria

- [ ] Can detect Assimp version at runtime
- [ ] Debug output shows version-specific values
- [ ] Animations render identically on both Assimp 5.4.1 and 6.0.2
- [ ] No visual artifacts (garbage polygons, swirled rotations)
- [ ] Unit tests pass on both versions
- [ ] Code can be easily extended for future Assimp versions

## Next Steps

1. Read this plan
2. Create AssimpVersion.cs and AssimpVersionDetector.cs
3. Add version detection unit test
4. Load test FBX model on both versions and capture debug output
5. Create comparison spreadsheet of Duration/TicksPerSecond/NFrames values
6. Based on findings, implement compensation code step by step
