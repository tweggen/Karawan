using System;

namespace Splash;

public class Flags
{
    /**
     * How the renderer is allowed to group instances into a single batch.
     *
     * Renderer-agnostic on purpose: a backend that cannot feed a per-instance frame
     * number needs batches split per animation and per frame, whichever API it uses.
     * The backend expresses that need through IThreeD.HasPerInstanceAnimationFrames.
     *
     * The GL-specific choice that drives it (SSBO vs UBO vs uniform) lives in
     * Splash.OpenGL.GLAnimBuffers - it names a GL concept and does not belong here.
     */
    [Flags]
    public enum AnimBatching
    {
        ByFrameno = 1,
        ByAnimation = 2
    };
}
