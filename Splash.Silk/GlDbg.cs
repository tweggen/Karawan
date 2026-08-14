using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Karawan.Graphics.OpenGL;
using static engine.Logger;

namespace Splash.Silk;

/**
 * The ONE way to ask "did that GL call fail".
 *
 * Before this there were nine, and the differences between them were not choices anybody
 * made - they were sediment. Five drained the error queue and four read a single error;
 * three reported through Error, one through Warning, one straight to Console.Error and one
 * discarded the error it had just consumed. Gates ranged from a const false, to an instance
 * bool, to [Conditional("DEBUG")], to nothing at all. Fourteen sites in SilkThreeD were
 * ungated and ran on every platform, mobile included, at 2,524 glGetError calls per frame -
 * 31.5% of all GL traffic in the captured GATE-F frame.
 *
 * HOW A SITE LOOKS NOW
 *
 *     GlDbg.Check(gl);                       // where am I? the compiler fills that in
 *     GlDbg.Check(gl, $"vao={handle}");      // when the value matters, not just the place
 *
 * There is no gate to write, because there is no gate to forget. There is no location
 * string to write, because CallerMemberName / CallerLineNumber cannot drift from the code
 * the way a hand-typed "after BindVertexArray" drifts the moment the call above it moves.
 *
 * ZERO COST WHEN COMPILED OUT
 *
 * [Conditional] is doing real work here, and it is stronger than an `if` on a const. The
 * compiler removes the CALL AND ITS ARGUMENTS at every call site when JOYCE_GL_DIAG is
 * undefined - so `$"vao={handle}"` is never built, never allocated, never evaluated. Not a
 * branch the JIT hopefully folds: no IL at all. Splash.Silk.csproj defines the symbol for
 * Debug and leaves it undefined for Release.
 *
 * LEAST COST WHEN COMPILED IN
 *
 * Where the driver can push errors at us (KHR_debug - see GlDiagnostics) polling is pure
 * waste, because the callback already reports the same faults WITH the failing call named,
 * which no amount of polling can do. So Check() returns on a single static field read and
 * the whole scheme costs one predictable branch. Polling only happens where there is no
 * callback to be had: desktop GL below 4.3 (macOS tops out at 4.1) and ES 3.0/3.1 drivers
 * without GL_KHR_debug.
 *
 * The interpolated-string handler is what keeps the second form honest. Arguments to a
 * [Conditional] method are erased when compiled out, but when compiled IN they are
 * evaluated before the call - so without the handler every `$"..."` would allocate on every
 * draw even in callback mode. The handler checks the mode before appending anything, the
 * same trick engine.DebugFilter uses.
 *
 * WHY EVERY SITE DRAINS
 *
 * glGetError clears ONE error per call, so a site that reads once leaves the rest queued
 * for whoever asks next. That is what produced the "GL ERROR BEFORE ... (pre-existing)"
 * loops in DrawMeshInstanced: they were draining a queue that the single-read sites in
 * SkProgramEntry and the ImGui controller kept dirty. Drain-always removes the cause, and
 * with it the need for any "drain before" idiom - which is why no such API exists here.
 */
public static class GlDbg
{
    public enum Mode
    {
        /** Nothing is watching. */
        Off,

        /** The driver pushes errors to us; Check() is a no-op and wants to stay one. */
        Callback,

        /** No debug output on this context, so Check() sites are the only visibility. */
        Poll,
    }

    /**
     * Read on every Check() when compiled in, so it stays a plain static field behind a
     * property and nothing more.
     */
    public static Mode Active { get; private set; } = Mode.Off;

    /**
     * Decide once, at context creation, how errors will be observed for this run.
     *
     * NOT [Conditional]: the callback costs nothing when nothing is wrong - the driver
     * calls us only on a fault - so it is worth having in a Release build where the
     * polling sites have been compiled away. That asymmetry is the point. What
     * JOYCE_GL_DIAG controls is per-call polling, which is the part with a price.
     */
    public static void Init(GL gl, GlDiagnostics.DebugApi api)
    {
        if (api != GlDiagnostics.DebugApi.None && GlDiagnostics.Install(gl, api))
        {
            Active = Mode.Callback;
            return;
        }

#if JOYCE_GL_DIAG
        Active = Mode.Poll;
        Trace("GlDbg: no debug output on this context, falling back to polling at Check() sites.");
#else
        Active = Mode.Off;
        Trace("GlDbg: no debug output on this context and polling is compiled out; GL errors are unobserved.");
#endif
    }

    /**
     * Report - and clear - anything the calls above this point raised.
     *
     * The location is supplied by the compiler. Pass `what` only to add a VALUE the line
     * number cannot convey (a handle, an index); never to restate where you are.
     */
    [Conditional("JOYCE_GL_DIAG")]
    public static void Check(
        GL gl,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
        => _drain(gl, null, member, file, line);

    /**
     * As above, with a value the location alone cannot convey.
     *
     * Two overloads rather than one optional parameter: a ref struct cannot be an optional
     * parameter, and this way `Check(gl)` and `Check(gl, $"...")` each bind to exactly one
     * method with no ambiguity to reason about at the call site.
     */
    [Conditional("JOYCE_GL_DIAG")]
    public static void Check(
        GL gl,
        GlDbgInterpolation what,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
        => _drain(gl, what.ToStringAndClear(), member, file, line);

    private static void _drain(GL gl, string? detail, string member, string file, int line)
    {
        if (Active != Mode.Poll) return;

        GLEnum error;
        while ((error = gl.GetError()) != GLEnum.NoError)
        {
            Error($"GL {error} at {Path.GetFileName(file)}:{line} ({member})"
                  + (string.IsNullOrEmpty(detail) ? "" : $" {detail}"));
        }
    }
}

/**
 * Builds Check()'s optional detail string only when it will actually be read.
 *
 * Without this, `GlDbg.Check(gl, $"vao={handle}")` would allocate and format on every call
 * whenever JOYCE_GL_DIAG is defined - including on Windows and Android, where the callback
 * is active and Check() discards the string immediately. In DrawMeshInstanced that is
 * thousands of dead allocations a frame in every Debug run.
 *
 * Mirrors engine.DebugFilter's handler; see that type for the pattern in its original use.
 */
[InterpolatedStringHandler]
public ref struct GlDbgInterpolation
{
    private DefaultInterpolatedStringHandler _inner;
    private readonly bool _wanted;

    public GlDbgInterpolation(int literalLength, int formattedCount, out bool shouldAppend)
    {
        _wanted = GlDbg.Active == GlDbg.Mode.Poll;
        shouldAppend = _wanted;
        _inner = _wanted ? new DefaultInterpolatedStringHandler(literalLength, formattedCount) : default;
    }

    public void AppendLiteral(string value)
    {
        if (_wanted) _inner.AppendLiteral(value);
    }

    public void AppendFormatted<T>(T value)
    {
        if (_wanted) _inner.AppendFormatted(value);
    }

    public void AppendFormatted<T>(T value, string? format)
    {
        if (_wanted) _inner.AppendFormatted(value, format);
    }

    public string ToStringAndClear() => _wanted ? _inner.ToStringAndClear() : string.Empty;
}
