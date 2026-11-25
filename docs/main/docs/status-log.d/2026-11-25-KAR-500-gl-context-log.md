### KAR-500: the log names the GPU, driver and GL limits (2026-11-25)

**Why.** On one Windows 11 notebook (Intel(R) Graphics, driver 32.0.101.9028) the walking
player's body does not draw, while NPCs animate and the cube in the player's hand follows
the hand bone (a CPU node). Nothing in the log said which GPU, driver or GL context the game
ran on, so the first round of diagnosis needed Device Manager screenshots.

**What.** At GL context setup `GlDiagnostics.LogContext` writes one unfiltered line:
vendor, renderer, version, GLSL version, the animation strategy (`GLAnimBuffers`), the
error-reporting mode (`GlDbg.Active`), and the limits the skinning paths rely on —
`MAX_TEXTURE_SIZE`, `MAX_UNIFORM_BLOCK_SIZE`, `MAX_VERTEX_UNIFORM_COMPONENTS`, and on SSBO
contexts only (`_hasGL43`) `MAX_SHADER_STORAGE_BLOCK_SIZE`,
`MAX_SHADER_STORAGE_BUFFER_BINDINGS`, `MAX_VERTEX_SHADER_STORAGE_BLOCKS`. Example (Linux,
nouveau): `vendor="Mesa", renderer="NVD9", version="4.3 (Core Profile) Mesa 26.2.4…",
animations=AnimSSBO, errors=Callback; … MAX_SHADER_STORAGE_BLOCK_SIZE=134217728 …`.

The nine GL constants were added to `docs/roadmap/proposed/wp-5.1/surface/surface.json` and
the binding regenerated with `gen.py` against the pinned gl.xml (values verified there; CI's
reproduction check covers it). The Roslyn surface probe (`wp-5.1/surface`) no longer
compiles Splash.OpenGL (it needs .NET 9 and rejects C# 13 features) — it was a one-time
extraction from Silk.NET, so `surface.json` is now edited as the input it is.

**Measured for the open fault.** The player is the only character on the `full` animation
pack: its bone-matrix SSBO is 4.6 MB (74,800 matrices), the largest baked; NPC packs are at
most 4.1 MB. Well inside the 16 MB the spec guarantees, so size alone does not explain it.
Camera distance was ruled out in play (zooming out changes nothing). Not fixed here.
