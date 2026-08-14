using System;
using System.Runtime.InteropServices;
using Karawan.Graphics.OpenGL;
using static engine.Logger;

namespace Splash.Silk;

/**
 * GL error reporting, driver-pushed where possible instead of polled.
 *
 * WHY NOT glGetError
 *
 * glGetError answers "did anything fail since you last asked". To learn WHICH call failed
 * you must ask after every call, which is what makes it expensive - and it is a
 * synchronising call, so the driver flushes pending work to answer accurately. Measured on
 * this renderer, glGetError was about a THIRD of all GL traffic: 85,843 of ~260,000 calls
 * across the captured GATE-F traces.
 *
 * It also mislocates. The GL error flag is sticky, so an error raised at startup sits in
 * the queue until something drains it - this codebase already hit exactly that, and
 * SilkThreeD's _hasGL43 comment records it: a startup error was reported as "pre-existing"
 * by the first DrawMeshInstanced on every single run, making real errors indistinguishable
 * from noise.
 *
 * WHAT KHR_debug DOES INSTEAD
 *
 * The driver calls US, with the source, the type, a severity and a human-readable message
 * naming the actual fault rather than an enum. Nothing is polled, so nothing is paid when
 * nothing is wrong. GL_DEBUG_OUTPUT was ALREADY being enabled here - SilkThreeD turned it
 * on and the comment admitted "nothing calls DebugMessageCallback, so this currently only
 * populates the (unread) debug message log". This installs the missing half.
 *
 * DESKTOP AND MOBILE
 *
 * This was desktop-only when first written, gated on SilkThreeD's _hasGL43. That gate was
 * wrong for the job: _hasGL43 also selects the SSBO animation strategy, and it is false for
 * OpenGLES BY CONSTRUCTION - so ES 3.2, which has KHR_debug in core, scored false and kept
 * polling. Mobile got the worst arrangement available: no callback, and every one of the
 * ungated glGetError sites, on tile-based GPUs where polling costs most.
 *
 * Capability is now decided by _detect, on what the spec actually says:
 *
 *   desktop GL >= 4.3     core           unsuffixed entry points
 *   OpenGL ES  >= 3.2     core           unsuffixed entry points
 *   OpenGL ES  3.0 / 3.1  GL_KHR_debug   ...KHR-suffixed entry points (the extension
 *                                        mandates the suffix on ES; a context exports
 *                                        one spelling or the other, never both)
 *   anything else         none           GlDbg falls back to polling
 *
 * Detection is spec-driven rather than probing glGetProcAddress for a non-null pointer,
 * because GLTrace deliberately hands back a thunk for every entry point it knows - present
 * in the driver or not - so a pointer probe reports success under tracing on a driver that
 * has no such call.
 *
 * SYNCHRONOUS stays off everywhere, and matters more here than on desktop: serialising a
 * tile-based deferred renderer to deliver messages on the offending call's stack is
 * punishing on mobile. Severity filtering is applied BEFORE enabling anything, for the same
 * reason it is on desktop - mobile drivers are the chatty ones.
 */
public static class GlDiagnostics
{
    /** Which spelling of the KHR_debug entry points this context exports, if any. */
    public enum DebugApi
    {
        None,

        /** Unsuffixed - core in desktop GL 4.3+ and in OpenGL ES 3.2+. */
        Core,

        /** ...KHR-suffixed - the GL_KHR_debug extension on OpenGL ES 3.0/3.1. */
        Khr,
    }

    /** True once the driver is pushing messages, which is what makes polling redundant. */
    public static bool IsCallbackActive { get; private set; }

    /**
     * What this context can do, from the API name and version SilkThreeD already parsed
     * out of global config, plus - on ES below 3.2 only - the extension string.
     *
     * glGetString(GL_EXTENSIONS) is queried ONLY on ES, where it is valid and returns the
     * whole space-separated list. On a desktop core profile the same call is an error
     * (GL_INVALID_ENUM), which would seed the very error queue this system exists to keep
     * clean; desktop never reaches that branch because >= 4.3 is decided by version alone.
     */
    public static DebugApi Detect(GL gl, string api, int versionNumber)
    {
        if (api == "OpenGL") return versionNumber >= 430 ? DebugApi.Core : DebugApi.None;
        if (api != "OpenGLES") return DebugApi.None;
        if (versionNumber >= 320) return DebugApi.Core;
        if (versionNumber < 300) return DebugApi.None;

        string extensions;
        try
        {
            extensions = gl.GetStringS(StringName.Extensions) ?? "";
        }
        catch (Exception e)
        {
            Warning($"GlDiagnostics: could not read the ES extension string: {e.Message}");
            return DebugApi.None;
        }

        // Substring alone would also match a hypothetical GL_KHR_debug_something, so the
        // list is split on the separator the spec defines.
        foreach (var ext in extensions.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (ext == "GL_KHR_debug") return DebugApi.Khr;
        }
        return DebugApi.None;
    }

    /*
     * The driver keeps the raw pointer. A collected delegate would jump into freed memory
     * at the first message - the same hazard Sdl3WindowBackend's s_lifecycleWatch carries,
     * and the reason this is a static field rather than a local.
     */
    private static DebugProc? _keepAlive;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void DebugProc(uint source, uint type, uint id, uint severity,
                                    int length, IntPtr message, IntPtr userParam);

    // KHR_debug constants. Not in the generated enum surface because no call site names
    // them; spelled out here with their registry values rather than invented.
    private const uint DONT_CARE = 0x1100;
    private const uint SEVERITY_HIGH = 0x9146;
    private const uint SEVERITY_MEDIUM = 0x9147;
    private const uint SEVERITY_LOW = 0x9148;
    private const uint SEVERITY_NOTIFICATION = 0x826B;
    private const uint TYPE_ERROR = 0x824C;
    private const uint TYPE_DEPRECATED = 0x824D;
    private const uint TYPE_UNDEFINED = 0x824E;
    private const uint TYPE_PORTABILITY = 0x824F;
    private const uint TYPE_PERFORMANCE = 0x8250;

    private static string _typeName(uint type) => type switch
    {
        TYPE_ERROR => "error",
        TYPE_DEPRECATED => "deprecated",
        TYPE_UNDEFINED => "undefined-behaviour",
        TYPE_PORTABILITY => "portability",
        TYPE_PERFORMANCE => "performance",
        _ => $"0x{type:X}",
    };

    private static void _onMessage(uint source, uint type, uint id, uint severity,
                                   int length, IntPtr message, IntPtr userParam)
    {
        string text;
        try
        {
            text = length > 0
                ? Marshal.PtrToStringUTF8(message, length) ?? ""
                : Marshal.PtrToStringUTF8(message) ?? "";
        }
        catch (Exception)
        {
            // Never let a diagnostic throw across the native boundary: an exception
            // escaping into the driver is undefined behaviour, and this is the code that
            // is supposed to be reporting problems, not creating them.
            text = "<unreadable debug message>";
        }

        string what = $"GL {_typeName(type)} [id {id}]: {text}";

        switch (severity)
        {
            case SEVERITY_HIGH:
                Error(what);
                break;
            case SEVERITY_MEDIUM:
                Warning(what);
                break;
            default:
                Trace(what);
                break;
        }
    }

    /**
     * Install the callback for the spelling this context exports.
     *
     * Returns whether the driver is now pushing messages. False is a normal outcome, not a
     * failure to handle here - GlDbg reads it and falls back to polling.
     */
    public static unsafe bool Install(GL gl, DebugApi api)
    {
        if (IsCallbackActive) return true;
        if (api == DebugApi.None) return false;

        try
        {
            _keepAlive = _onMessage;
            IntPtr callback = Marshal.GetFunctionPointerForDelegate(_keepAlive);

            /*
             * Filter before enabling, not after. Some drivers emit a performance hint per
             * draw call; at ~110 mesh batches a frame that is thousands of log lines a
             * second, and the logging becomes the bottleneck the diagnostic was meant to
             * find. Everything off, then errors and the two severities worth waking up for.
             */
            if (api == DebugApi.Khr)
            {
                gl.DebugMessageCallbackKHR(callback, null);
                gl.DebugMessageControlKHR(DONT_CARE, DONT_CARE, DONT_CARE, 0, null, false);
                gl.DebugMessageControlKHR(DONT_CARE, DONT_CARE, SEVERITY_HIGH, 0, null, true);
                gl.DebugMessageControlKHR(DONT_CARE, DONT_CARE, SEVERITY_MEDIUM, 0, null, true);
                gl.DebugMessageControlKHR(DONT_CARE, TYPE_ERROR, DONT_CARE, 0, null, true);
            }
            else
            {
                gl.DebugMessageCallback(callback, null);
                gl.DebugMessageControl(DONT_CARE, DONT_CARE, DONT_CARE, 0, null, false);
                gl.DebugMessageControl(DONT_CARE, DONT_CARE, SEVERITY_HIGH, 0, null, true);
                gl.DebugMessageControl(DONT_CARE, DONT_CARE, SEVERITY_MEDIUM, 0, null, true);
                gl.DebugMessageControl(DONT_CARE, TYPE_ERROR, DONT_CARE, 0, null, true);
            }

            IsCallbackActive = true;
            Trace($"GlDiagnostics: KHR_debug callback installed ({api} entry points); "
                  + "glGetError polling is now redundant.");
            return true;
        }
        catch (Exception e)
        {
            // A context that advertises the capability but rejects the callback should cost
            // the diagnostic, not the frame. Polling stays available.
            _keepAlive = null;
            IsCallbackActive = false;
            Warning($"GlDiagnostics: could not install the KHR_debug callback, falling back to polling: {e}");
            return false;
        }
    }
}
