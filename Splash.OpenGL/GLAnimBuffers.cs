namespace Splash.OpenGL;

/**
 * Which GL buffer type carries the baked bone matrices to the shader.
 *
 * This lives in Splash.OpenGL, not Splash: it names a GL concept and every reference to it
 * is inside this project. Splash is the platform-agnostic renderer abstraction and must not
 * name a graphics API - see docs/ARCHITECTURE/PLATFORM_BACKEND.md §5 and WP-0.2.
 *
 * The renderer-agnostic consequence of this choice is expressed as
 * IThreeD.HasPerInstanceAnimationFrames, which is what Splash/ consumes instead. Only the
 * SSBO strategy carries a per-instance frame number, so the other two require batches to be
 * split per animation and frame (Splash.Flags.AnimBatching, which stays in Splash because
 * batching is a renderer-agnostic concept).
 */
public enum GLAnimBuffers
{
    AnimSSBO = 0,
    AnimUniform = 1,
    AnimUBO = 2,
}
