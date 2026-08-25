# Rendering Architecture

This document describes the rendering pipeline of the Joyce engine used by Silicon Desert 2.

## Camera System

Cameras are ECS entities with `Camera3` and `Transform3ToWorld` components. Each camera has:
- **CameraMask** (uint): Bitmask controlling which entities are visible. An entity is rendered by a camera when `entity.CameraMask & camera.CameraMask != 0`.
- **Renderbuffer**: If non-null, the camera renders to an off-screen FBO. If null, it renders directly to the screen (FBO 0).
- **CameraFlags**: Bitfield (PreloadOnly, DisableDepthTest, EnableFog, RenderSkyboxes, etc.).

### Camera Masks in Silicon Desert 2

| Camera | Mask | Renderbuffer | Purpose |
|--------|------|-------------|---------|
| Root Scene | 0x00000001 | rootscene_3d | Main 3D world |
| Loading Scene | 0x00000010 | rootscene_3d | Loading spinner |
| ScreenComposer | 0x00400000 | (none) | Composites renderbuffer textures to screen |
| Map | 0x00800000 | varies | Minimap / fullscreen map |
| OSD | 0x01000000 | (none) | On-screen display (text, buttons) |
| Logos | 0x02000000 | (none) | Title cards / login screen |

## Rendering Pipeline

### Data Collection (Logical Thread)

`LogicalRenderer.CollectRenderData()` runs on the logical thread each frame:

1. `CreatePfRenderbufferSystem` creates `PfRenderbuffer` components for cameras with renderbuffers.
2. `CreatePfInstanceSystem` creates `PfInstance` components (platform-specific mesh/material entries).
3. All cameras with `Camera3` + `Transform3ToWorld` are queried and sorted by `CameraMask` ascending.
4. `DrawInstancesSystem` collects visible entities for each camera (filtering by camera mask, frustum culling, LOD).
5. `DrawSkyboxesSystem` collects skybox geometry for cameras with `RenderSkyboxes` flag.
6. A `RenderFrame` is assembled:
   - **Renderbuffer cameras first** (sorted by mask)
   - **Direct-to-screen cameras second** (sorted by mask)
7. The `RenderFrame` is enqueued for the render thread.

### GPU Rendering (Render Thread)

`SilkRenderer.RenderFrame()` processes each `RenderPart` in order:

1. **BeginRenderFrame**: Resets cached shader/material/texture state.
2. For each RenderPart:
   a. Switch framebuffer target (renderbuffer FBO or screen FBO 0).
   b. Clear: first direct-to-screen camera clears color+depth; subsequent cameras clear depth only. Renderbuffer cameras clear on first use per frame.
   c. Set view/projection matrices.
   d. **Standard pass** (opaque): blending off, backface culling on.
   e. **Transparent pass**: blending on (SrcAlpha/OneMinusSrcAlpha), culling off.
3. **EndRenderFrame**.

### Material Upload

Materials are uploaded on-demand during rendering via `FillMaterialEntry()`:
- Looks up diffuse and emissive textures in `TextureManager`.
- If a texture is not found, `haveUploadSuccess = false` and the material remains un-uploaded.
- Un-uploaded materials are retried each frame (subject to per-frame upload time budgets).

### Texture Binding State

`SilkTextureChannelState` manages per-channel texture binding with caching:
- `_currentSkTexture` tracks what's currently bound to avoid redundant GL calls.
- `_useTexture(null)` binds a 1x1 transparent black fallback texture.
- `ResetCachedState()` invalidates the cache at frame boundaries (needed when external code like GlStateSaver may have changed GL state).

**Critical invariant**: After `ResetCachedState()`, the transparent fallback must be actively bound to prevent stale textures from the previous frame bleeding through.

## Screen Composition

The `ScreenComposer` module composites off-screen renderbuffers onto the screen:

1. Creates an orthographic camera (mask 0x00400000).
2. For each layer, creates a plane mesh textured with the renderbuffer's output.
3. The plane transform includes `Matrix4x4.CreateScale(1f, -1f, 1f)` (Y-flip) to correct OpenGL's bottom-up texture orientation.

The rootscene_3d renderbuffer is the primary layer, showing the 3D world content.

## OSD (On-Screen Display)

The OSD system renders 2D text and UI elements:

1. `RenderOSDSystem` draws `OSDText` components onto a `DoubleBufferedFramebuffer` (SkiaSharp-backed).
2. `Display` module creates a plane mesh textured with the display buffer.
3. The OSD plane has camera mask 0x01000000, rendered by the dedicated OSD camera.
4. The OSD camera uses orthographic projection with depth testing disabled.

## Login Screen Cameras

During the login/logos screen, only these cameras are active:
1. **ScreenComposer** (0x00400000) - renders the rootscene_3d plane (initially transparent since no camera has rendered to the renderbuffer yet).
2. **OSD** (0x01000000) - renders login menu text/buttons.
3. **Logos** (0x02000000) - renders title card images.

## Known Issue: Mirrored Rendering (Fixed)

### Symptom
A Y-flipped duplicate of the OSD/login content appeared overlaid on the login screen.

### Root Cause
`SilkTextureChannelState.ResetCachedState()` set `_currentSkTexture = null` without binding the transparent fallback texture. When `_useTexture(null)` was subsequently called (for materials whose textures hadn't been uploaded yet, like the ScreenComposer's rootscene_3d texture before the root scene starts), the check `_currentSkTexture != skTexture` evaluated to `null != null = false`, so no texture rebinding occurred. The GL texture unit retained whatever was bound in the previous frame — typically the OSD framebuffer texture. The ScreenComposer then rendered this stale OSD content through its Y-flip transform, creating a mirrored overlay.

### Fix
`ResetCachedState()` now actively binds the transparent fallback texture before clearing the cache, ensuring a known GL state at frame boundaries.

### Timeline
- Introduced in commit 98e4570b ("have the tree visible") which added `ResetCachedState()` to handle GL state sharing with Avalonia/Aihao.
- The bug only manifested when a material with a missing texture was rendered (ScreenComposer plane before the root scene camera starts rendering to rootscene_3d).
