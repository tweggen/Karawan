using System;
using System.Numerics;
using Silk.NET.Input;

namespace Splash.Silk;

/// <summary>
/// The seam between <see cref="Platform"/> and whatever actually owns the window, the GL
/// context and the frame loop.
/// </summary>
/// <remarks>
/// <para>
/// This is WP-3.3. Before it, <c>Platform</c> took a <c>Silk.NET.Windowing.IView</c>
/// directly - 29 usages - and that single parameter was the reason every launcher had to
/// import Silk. Android cannot supply one at all once SDL3 owns the activity, because
/// <c>Silk.NET.Windowing.Window.GetView</c> needs Silk's own SDL2 window underneath.
/// </para>
/// <para>
/// Deliberately NOT in <c>Splash</c>: the interface still traffics in Silk's
/// <see cref="IInputContext"/> (see <see cref="SilkInputContext"/>) and in Silk's GL, both
/// of which stay until Phase 5. Moving it down to <c>Splash</c> would drag Silk with it and
/// buy nothing.
/// </para>
/// <para>
/// <b>The frame loop is owned by the backend, not by Platform.</b> Silk drives it through
/// <c>IView.Run</c>; SDL3 on Android drives it from SDL's own thread, entered through
/// <c>libmain.so</c>. Platform supplies the per-frame work as callbacks and does not care
/// which.
/// </para>
/// </remarks>
public interface IWindowBackend : IDisposable
{
    /// <summary>Logical window size, in the units the engine's view rectangle uses.</summary>
    Vector2 Size { get; }

    /// <summary>Drawable size in pixels. Differs from <see cref="Size"/> on hidpi.</summary>
    Vector2 FramebufferSize { get; }

    /// <summary>
    /// GL entry-point resolver, handed to <c>GL.GetApi</c>.
    /// </summary>
    /// <remarks>
    /// Silk exposes <c>GL.GetApi(Func&lt;string, nint&gt;)</c>, so a backend only has to
    /// produce this one function - on SDL3 it is <c>SDL_GL_GetProcAddress</c>. That is why
    /// swapping the window backend does not disturb the GL bindings at all.
    /// </remarks>
    Func<string, nint> GetProcAddress { get; }

    /// <summary>True once the loop should stop.</summary>
    bool IsClosing { get; }

    /// <summary>Present the current frame.</summary>
    void SwapBuffers();

    /// <summary>
    /// Silk's input context, or <c>null</c> when the backend feeds
    /// <c>engine.news.EventQueue</c> itself.
    /// </summary>
    /// <remarks>
    /// Android has always been the second case: <c>GameSurface.OnTouch</c> pushes touch
    /// events straight into the queue and never went through Silk input. The queue is the
    /// contract - only the left-hand side of the translation changes.
    /// </remarks>
    IInputContext? SilkInputContext { get; }

    /// <summary>
    /// Fullscreen, where the platform supports it. Desktop only; a no-op elsewhere.
    /// </summary>
    void SetFullscreen(bool isFullscreen);

    /// <summary>
    /// Show or hide the mouse cursor, and take or release relative-motion mode with it.
    /// </summary>
    /// <remarks>
    /// Mirrors what <c>Platform._setMouseEnabled</c> used to do inline through Silk's
    /// <c>Cursor.CursorMode</c>: <c>Normal</c> when the mouse is "enabled" (the pointer is a
    /// pointer, UI hit-testing applies), <c>Raw</c> otherwise (hidden and captured, the
    /// mouse-look mode). The engine's default is <c>_mouseEnabled == false</c>, i.e. HIDDEN.
    ///
    /// Needed as a backend concern for the same reason as the keyboard: the SDL3 backend has
    /// no Silk input context, so guarding that null - as WP-3.1 did - left the cursor simply
    /// never configured. GATE-C found it visible in fullscreen on Windows 11.
    /// </remarks>
    void SetMouseVisible(bool isVisible);

    /// <summary>
    /// Show or hide the on-screen keyboard, on platforms that have one.
    /// </summary>
    /// <remarks>
    /// This has to be a backend concern rather than something <see cref="Platform"/> does
    /// through <see cref="SilkInputContext"/>, because that context is <c>null</c> on the
    /// SDL3 backend - which is precisely the backend that HAS an on-screen keyboard. The
    /// old call chain (<c>IKeyboard.BeginInput()</c> -> SDL2's <c>SDL_StartTextInput</c>)
    /// became unreachable on Android when Silk windowing was removed, and nothing replaced
    /// it, so the keyboard simply never appeared. See KI-10.
    ///
    /// Desktop backends forward to Silk's <c>BeginInput</c>/<c>EndInput</c>, which is what
    /// Platform used to call inline; a desktop platform has a real keyboard and nothing
    /// visible happens either way.
    /// </remarks>
    void SetKeyboardVisible(bool isVisible);

    /// <summary>
    /// Which windowing library is underneath.
    /// </summary>
    /// <remarks>
    /// Previously sniffed by testing whether the view's type name contained "Glfw" or
    /// "Sdl", which worked only because Silk happens to name its view types after their
    /// backend. A backend can simply say.
    /// </remarks>
    Platform.UnderlyingFrameworks UnderlyingFramework { get; }

    /// <summary>
    /// Attach the callbacks above to the underlying window's events. Called once, after
    /// they have all been assigned.
    /// </summary>
    /// <remarks>
    /// Separate from construction because Silk only produces a usable input context after
    /// its Load event fires, so the wiring cannot happen in the constructor.
    /// </remarks>
    void Subscribe();

    // ---- callbacks, installed by Platform before Run() ----------------------------

    Action? OnLoad { get; set; }
    Action<Vector2>? OnResize { get; set; }
    Action<double>? OnUpdate { get; set; }
    Action<double>? OnRender { get; set; }
    Action? OnClosing { get; set; }
    Action<bool>? OnFocusChanged { get; set; }

    /// <summary>Run once per iteration before events are pumped.</summary>
    Action? BeforeEvents { get; set; }

    /*
     * Keyboard, for backends with no Silk input context. The argument is the engine's own
     * key-code string ("a", "(enter)", "(cursorleft)"), i.e. the SAME right-hand side the
     * Silk path produces - only the translation into it differs per backend.
     *
     * They are callbacks rather than direct EventQueue pushes on purpose: Platform pairs
     * every raw key event with an InputMapper logical translation, and a backend that
     * pushed straight to the queue would silently skip that. Logical input would then be
     * dead on Android and nowhere else.
     */
    /*
     * WP-6.3: key events carry the PHYSICAL position alongside the legacy code string.
     * The string stays because it is the existing contract with game code; the ScanCode
     * is what bindings should migrate onto (WP-6.4), because the string only looks like
     * a character - "a" is the A-POSITION, which prints Q on AZERTY.
     *
     * OnKeyCharacter stays a bare string: it is TEXT, already composed by layout and IME,
     * and has no physical position to report. Never synthesise it from a key event.
     */
    Action<string, engine.inputs.ScanCode>? OnKeyPressed { get; set; }
    Action<string, engine.inputs.ScanCode>? OnKeyReleased { get; set; }
    Action<string>? OnKeyCharacter { get; set; }

    /*
     * Mouse, same arrangement and for the same reason (WP-3.1). Positions are absolute
     * window pixels, matching what Silk's IMouse.Position reports, because Platform maps
     * them through _fullToViewPosition and _shallReturnBecauseUI - both of which are
     * defined in window pixels.
     *
     * The button number is the engine's, i.e. Silk's MouseButton ordinal: 0 left, 1 right,
     * 2 middle. It reaches game code as the event Code string, so it is contract.
     */
    Action<Vector2>? OnMouseMoved { get; set; }

    /// <summary>Pointer position, then the scroll delta.</summary>
    /// <remarks>
    /// The position travels WITH the event rather than being tracked between events.
    /// Platform's <c>_shallReturnBecauseUI</c> needs to know where the pointer was, and
    /// Silk answers that from <c>IMouse.Position</c> - which a backend with no Silk input
    /// context does not have. SDL reports it on the wheel event itself, so carrying it here
    /// removes the need for any shared cursor state, and with it the ordering hazard of
    /// updating that state from a second handler on the same callback.
    /// </remarks>
    Action<Vector2, Vector2>? OnMouseWheel { get; set; }
    Action<int, Vector2>? OnMousePressed { get; set; }
    Action<int, Vector2>? OnMouseReleased { get; set; }

    /*
     * Gamepad. Stick and trigger values are already in the engine's convention: -1..+1 for
     * sticks with SDL's sign passed through UNCHANGED, and -1 meaning RELEASED for triggers.
     * Converting in the backend rather than in Platform keeps every SDL/Silk quirk on the
     * left-hand side of the seam - see Sdl3GamepadCodes for why those two conventions are
     * what they are, including why the earlier +Y-is-UP flip was wrong.
     *
     * The button name is the engine's string ("A", "RightStick", "DPadUp"), which is what
     * InputController switches on.
     */
    Action<int, Vector2>? OnGamepadStickMoved { get; set; }
    Action<int, float>? OnGamepadTriggerMoved { get; set; }
    Action<string, uint>? OnGamepadButtonPressed { get; set; }
    Action<string, uint>? OnGamepadButtonReleased { get; set; }

    /// <summary>
    /// Release anything blocked on the platform's main-thread monitor.
    /// </summary>
    /// <remarks>
    /// Not decoration. The pre-WP-3.3 loop called <c>_triggerWaitMonitor()</c> at three
    /// specific points - top of the iteration, before update, and once after the loop ends -
    /// and code elsewhere in the engine blocks waiting for exactly those. A backend that
    /// forgets to call it deadlocks on shutdown rather than failing visibly, so it is part
    /// of the contract instead of an implementation detail of the Silk loop.
    /// </remarks>
    Action? ReleaseMainThreadWaiters { get; set; }

    /// <summary>
    /// Drive the loop until closing. Returns only when the window is gone.
    /// </summary>
    void Run();

    /// <summary>Ask the loop to stop.</summary>
    void Close();
}
