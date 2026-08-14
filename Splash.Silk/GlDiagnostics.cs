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
 * DESKTOP ONLY, DELIBERATELY
 *
 * Installed only under SilkThreeD's _hasGL43, which is false for OpenGLES by construction,
 * so mobile behaviour is unchanged and keeps polling.
 *
 * KHR_debug IS available on ES - core in ES 3.2, and as GL_KHR_debug with ...KHR-suffixed
 * entry points on ES 3.0/3.1 - and the device this ships to reports ES 3.2. Enabling it
 * there is a deliberate later step: it would need the suffixed entry points, severity
 * filtering tight enough that chatty mobile drivers do not turn logging into the
 * bottleneck, and SYNCHRONOUS kept off because serialising a tile-based deferred GPU is
 * punishing. None of that should ride along with a desktop change.
 */
public static class GlDiagnostics
{
    /** True once the driver is pushing messages, which is what makes polling redundant. */
    public static bool IsCallbackActive { get; private set; }

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
     * Install the callback. Call ONLY where KHR_debug is known present - under
     * SilkThreeD's _hasGL43, which means desktop GL 4.3 or newer.
     */
    public static unsafe void Install(GL gl)
    {
        if (IsCallbackActive) return;

        try
        {
            _keepAlive = _onMessage;
            gl.DebugMessageCallback(Marshal.GetFunctionPointerForDelegate(_keepAlive), null);

            /*
             * Filter before enabling, not after. Some drivers emit a performance hint per
             * draw call; at ~110 mesh batches a frame that is thousands of log lines a
             * second, and the logging becomes the bottleneck the diagnostic was meant to
             * find. Everything off, then errors and the two severities worth waking up for.
             */
            gl.DebugMessageControl(DONT_CARE, DONT_CARE, DONT_CARE, 0, null, false);
            gl.DebugMessageControl(DONT_CARE, DONT_CARE, SEVERITY_HIGH, 0, null, true);
            gl.DebugMessageControl(DONT_CARE, DONT_CARE, SEVERITY_MEDIUM, 0, null, true);
            gl.DebugMessageControl(DONT_CARE, TYPE_ERROR, DONT_CARE, 0, null, true);

            IsCallbackActive = true;
            Trace("GlDiagnostics: KHR_debug callback installed; glGetError polling is now redundant.");
        }
        catch (Exception e)
        {
            // A driver that advertises 4.3 but rejects the callback should cost the
            // diagnostic, not the frame. Polling stays available.
            _keepAlive = null;
            IsCallbackActive = false;
            Warning($"GlDiagnostics: could not install the KHR_debug callback, falling back to polling: {e}");
        }
    }

    /**
     * Drain and report pending errors - the fallback for contexts with no debug output.
     *
     * A no-op once the callback is active, which is the point: the same call sites work on
     * both paths and only pay on the one that needs it.
     */
    public static void Poll(GL gl, string where)
    {
        if (IsCallbackActive) return;

        while (true)
        {
            var error = gl.GetError();
            if (error == GLEnum.NoError) return;

            Error($"GL error at {where}: {error}");
        }
    }
}
