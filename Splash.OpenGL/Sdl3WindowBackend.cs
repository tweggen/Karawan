using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using static SDL3.SDL;
using static engine.Logger;

namespace Splash.OpenGL;

/// <summary>
/// <see cref="IWindowBackend"/> over SDL3 - the Android path, and eventually the desktop one.
/// </summary>
/// <remarks>
/// <para>
/// Proven end to end by the WP-2.1 spike on an Adreno 825: <c>OpenGL ES 3.2</c>, a presented
/// frame, multi-touch, rotation and resume. This is the same sequence, wired into
/// <see cref="Platform"/> instead of a standalone activity.
/// </para>
/// <para>
/// <b>Silk's GL bindings still work.</b> <c>GL.GetApi</c> accepts a bare
/// <c>Func&lt;string, nint&gt;</c>, and SDL supplies exactly that as
/// <c>SDL_GL_GetProcAddress</c>. Replacing the window backend does not touch the renderer.
/// </para>
/// <para>
/// <b>This runs on SDL's thread, not the Android UI thread.</b> On Android, SDL's Java glue
/// spawns "SDLThread", which enters <c>libmain.so</c>'s <c>SDL_main</c> and from there the
/// managed entry point. Everything here therefore happens on the same thread as the engine
/// loop.
/// </para>
/// </remarks>
public sealed class Sdl3WindowBackend : IWindowBackend
{
    private IntPtr _window;
    private IntPtr _glContext;
    private bool _isClosing;

    // SDL keeps the raw function pointer; a collected delegate would jump into freed
    // memory at the next lifecycle transition - a crash on resume, long after the cause.
    private static SDL_EventFilter? s_lifecycleWatch;

    private const int SDL_GL_CONTEXT_PROFILE_ES = 0x0004;

    /// <summary>
    /// Logical window size - the same coordinate space SDL reports mouse positions in.
    /// </summary>
    /// <remarks>
    /// Differs from <see cref="FramebufferSize"/> only on a HiDPI display; identical
    /// everywhere else, Android included. The distinction matters because
    /// <c>Platform._shallReturnBecauseUI</c> compares a MOUSE position against a rectangle
    /// derived from this, and SDL reports mouse positions in logical units. Returning pixels
    /// here would put the UI hit-test off by the display scale on a retina Mac while looking
    /// perfectly correct on every other machine (WP-3.2).
    /// </remarks>
    public Vector2 Size
    {
        get
        {
            if (_window == IntPtr.Zero) return Vector2.Zero;
            SDL_GetWindowSize(_window, out int w, out int h);
            return new Vector2(w, h);
        }
    }

    /// <summary>
    /// Drawable size in pixels.
    /// </summary>
    /// <remarks>
    /// On Android this equals <see cref="Size"/>: the SDL window IS the drawable, with no
    /// separate logical size. On a HiDPI desktop display the two legitimately differ, which
    /// is why they are now separate queries (WP-3.2) rather than one value returned twice.
    /// <para>
    /// Feed this to the RENDERER and <see cref="Size"/> to anything comparing against a mouse
    /// position. Getting that backwards is the exact drift <c>Platform._applyFramebufferSize</c>
    /// exists to prevent, and it is invisible on any non-retina machine.
    /// </para>
    /// </remarks>
    public Vector2 FramebufferSize
    {
        get
        {
            if (_window == IntPtr.Zero) return Vector2.Zero;
            SDL_GetWindowSizeInPixels(_window, out int w, out int h);
            return new Vector2(w, h);
        }
    }

    public Func<string, nint> GetProcAddress => name => SDL_GL_GetProcAddress(name);

    public bool IsClosing => _isClosing;

    public Platform.UnderlyingFrameworks UnderlyingFramework => Platform.UnderlyingFrameworks.Sdl;

    public Action? OnLoad { get; set; }
    public Action<Vector2>? OnResize { get; set; }
    public Action<double>? OnUpdate { get; set; }
    public Action<double>? OnRender { get; set; }
    public Action? OnClosing { get; set; }
    public Action<bool>? OnFocusChanged { get; set; }
    public Action? BeforeEvents { get; set; }
    public Action? ReleaseMainThreadWaiters { get; set; }
    public Action<Vector2, Vector2>? OnMouseMoved { get; set; }
    public Action<Vector2, Vector2>? OnMouseWheel { get; set; }
    public Action<int, Vector2>? OnMousePressed { get; set; }
    public Action<int, Vector2>? OnMouseReleased { get; set; }
    public Action<int, Vector2>? OnGamepadStickMoved { get; set; }
    public Action<int, float>? OnGamepadTriggerMoved { get; set; }
    public Action<string, uint>? OnGamepadButtonPressed { get; set; }
    public Action<string, uint>? OnGamepadButtonReleased { get; set; }
    public Action<string, engine.inputs.ScanCode>? OnKeyPressed { get; set; }
    public Action<string, engine.inputs.ScanCode>? OnKeyReleased { get; set; }
    public Action<string>? OnKeyCharacter { get; set; }

    /// <summary>
    /// Creates the window and the GLES 3.0 context.
    /// </summary>
    /// <remarks>
    /// Done in the constructor rather than in <see cref="Run"/> because
    /// <see cref="GetProcAddress"/> has to be usable before Platform's load handler runs -
    /// that handler is what calls <c>GL.GetApi</c>.
    /// </remarks>
    /// <summary>
    /// Frame cap, in frames per second. 0 disables it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not optional, and not a nicety.</b> Every existing launcher configured its Silk
    /// view with <c>options.FramesPerSecond = 60; options.VSync = false;</c> and Silk's
    /// <c>Run()</c> enforced it. The first version of this backend enforced nothing and ran
    /// the loop flat out.
    /// </para>
    /// <para>
    /// On a device that manifests as more than wasted battery: anything that accumulates
    /// per FRAME rather than per elapsed second gets applied several times too often. It was
    /// first seen as a car that "spun like wild" the moment the accelerate control was
    /// touched - which looks exactly like a touch-handling bug and is not one.
    /// </para>
    /// </remarks>
    public int TargetFramesPerSecond { get; set; } = 60;

    private const int SDL_GL_CONTEXT_PROFILE_CORE = 0x0001;

    /*
     * Same two settings engine.Resource.ShaderSource reads. Defaults match the Android
     * values this backend shipped with, so an unconfigured caller behaves as before WP-3.2.
     */
    private static void _readGlSettings(out bool isGles, out int major, out int minor)
    {
        string api = engine.GlobalSettings.Get("platform.threeD.API");
        string version = engine.GlobalSettings.Get("platform.threeD.API.version");

        isGles = api != "OpenGL";
        major = isGles ? 3 : 4;
        minor = isGles ? 0 : 1;

        if (!string.IsNullOrEmpty(version) && version.Length >= 2
            && int.TryParse(version.Substring(0, 1), out int parsedMajor)
            && int.TryParse(version.Substring(1, 1), out int parsedMinor))
        {
            major = parsedMajor;
            minor = parsedMinor;
        }
    }

    /// <summary>
    /// Creates the window and its GL context.
    /// </summary>
    /// <remarks>
    /// <b>The GL profile comes from <c>platform.threeD.API</c> and
    /// <c>platform.threeD.API.version</c>, not hardcoded (WP-3.2).</b> Both launchers already
    /// set them before constructing a backend - "OpenGLES"/"310" on Android, "OpenGL"/"410"
    /// on macOS, "OpenGL"/"430" on Windows and Linux - and <c>ShaderSource</c> compiles
    /// shaders against those same two values. Asking SDL for a different context than the
    /// shaders are written for would present as shaders failing to compile, with nothing in
    /// the message mentioning the window.
    /// <para>
    /// <paramref name="width"/> and <paramref name="height"/> are logical units, or 0 to let
    /// the platform decide. Android always decides: SDL binds to the activity's existing
    /// surface and ignores them.
    /// </para>
    /// </remarks>
    public Sdl3WindowBackend(string title, int width = 0, int height = 0, bool isResizable = false)
    {
        /*
         * SDL_INIT_GAMEPAD is required for gamepad events to be delivered AT ALL - without
         * it SDL never enumerates pads and never sends GAMEPAD_ADDED, so the whole gamepad
         * path below is dead with no error anywhere (WP-3.1).
         */
        /*
         * HINTS BEFORE SDL_Init, NOT AFTER.
         *
         * SDL reads SDL_HINT_TOUCH_MOUSE_EVENTS while initialising its mouse subsystem, which
         * happens inside SDL_Init(SDL_INIT_VIDEO). WP-3.1 set it afterwards, so on Android SDL
         * had already decided to synthesise mouse events from touch. Every finger then arrives
         * twice: once from GameSurface.OnTouch, and again as a synthetic mouse event that
         * Platform now translates - and the two carry DIFFERENT coordinate spaces, normalised
         * 0..1 from the surface versus window pixels from SDL.
         */
        SDL_SetHint(SDL_HINT_TOUCH_MOUSE_EVENTS, "0");
        SDL_SetHint(SDL_HINT_MOUSE_TOUCH_EVENTS, "0");

        /*
         * SDL_INIT_GAMEPAD is required for gamepad events to be delivered AT ALL - without
         * it SDL never enumerates pads and never sends GAMEPAD_ADDED, so the whole gamepad
         * path below is dead with no error anywhere (WP-3.1).
         */
        if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO | SDL_InitFlags.SDL_INIT_EVENTS
                      | SDL_InitFlags.SDL_INIT_GAMEPAD))
        {
            throw new InvalidOperationException($"SDL_Init failed: {SDL_GetError()}");
        }

        _readGlSettings(out bool isGles, out int major, out int minor);

        // Must be set BEFORE the window exists; SDL bakes them into the config it picks.
        SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_PROFILE_MASK,
            isGles ? SDL_GL_CONTEXT_PROFILE_ES : SDL_GL_CONTEXT_PROFILE_CORE);
        SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_MAJOR_VERSION, major);
        SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_MINOR_VERSION, minor);
        SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_DEPTH_SIZE, 16);

        /*
         * HIGH_PIXEL_DENSITY makes the drawable a true pixel buffer on a retina display
         * rather than an upscaled logical one. It is what makes Size and FramebufferSize
         * legitimately differ - see the Size property.
         */
        SDL_WindowFlags flags = SDL_WindowFlags.SDL_WINDOW_OPENGL
                                | SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY;
        if (isResizable)
        {
            flags |= SDL_WindowFlags.SDL_WINDOW_RESIZABLE;
        }

        // On Android the size arguments are ignored - SDL binds to the activity's existing
        // surface - but SDL_WINDOW_OPENGL is what makes it an EGL surface.
        _window = SDL_CreateWindow(title, width, height, flags);
        if (_window == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SDL_CreateWindow failed: {SDL_GetError()}");
        }

        _glContext = SDL_GL_CreateContext(_window);
        if (_glContext == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"SDL_GL_CreateContext failed for {(isGles ? "GLES" : "GL")} {major}.{minor}: "
                + $"{SDL_GetError()}");
        }

        /*
         * Every launcher configured its Silk view with VSync = false and drove the cap
         * itself via FramesPerSecond; TargetFramesPerSecond does that here. Leaving the swap
         * interval at the driver default would silently stack vsync pacing on top of it.
         */
        SDL_GL_SetSwapInterval(0);

        Trace($"SDL3 window {width}x{height}, {(isGles ? "GLES" : "GL")} {major}.{minor}, "
              + $"resizable={isResizable}.");
    }

    /// <summary>
    /// Registers the app-lifecycle watch. See KI-8.
    /// </summary>
    /// <remarks>
    /// The six lifecycle events are never queued - <c>SDL_SendAppEvent</c> special-cases them
    /// and calls watchers in the same call stack, because by the time a polling loop could
    /// see them the process may already be frozen. Polling for them yields nothing, ever.
    /// </remarks>
    public unsafe void Subscribe()
    {
        s_lifecycleWatch = LifecycleWatch;
        if (!SDL_AddEventWatch(s_lifecycleWatch, IntPtr.Zero))
        {
            throw new InvalidOperationException($"SDL_AddEventWatch failed: {SDL_GetError()}");
        }
    }

    private unsafe bool LifecycleWatch(IntPtr userdata, SDL_Event* evt)
    {
        switch ((SDL_EventType)evt->type)
        {
            case SDL_EventType.SDL_EVENT_WILL_ENTER_BACKGROUND:
                // The last moment at which anything is guaranteed to run. Wuka's save hook
                // belongs here, NOT in the poll loop.
                OnFocusChanged?.Invoke(false);
                break;

            case SDL_EventType.SDL_EVENT_DID_ENTER_FOREGROUND:
                OnFocusChanged?.Invoke(true);
                break;

            case SDL_EventType.SDL_EVENT_TERMINATING:
                _isClosing = true;
                OnClosing?.Invoke();
                break;
        }

        // true = keep the event; a watch must not swallow events.
        return true;
    }

    public void SwapBuffers()
    {
        if (_window != IntPtr.Zero)
        {
            SDL_GL_SwapWindow(_window);
        }
    }

    /// <summary>
    /// Desktop fullscreen. Effectively a no-op on Android, where the activity is already
    /// fullscreen and SDL ignores the request.
    /// </summary>
    /// <remarks>
    /// Deliberately SDL's borderless "fullscreen desktop" behaviour - what SDL3 gives a
    /// window with no explicit fullscreen mode set - rather than a videomode switch. KI-1 is
    /// that a Release build starts fullscreen and hangs on macOS, with the mode switch a
    /// prime suspect; not reproducing that mechanism is the point.
    /// </remarks>
    public void SetFullscreen(bool isFullscreen)
    {
        if (_window == IntPtr.Zero) return;

        if (!SDL_SetWindowFullscreen(_window, isFullscreen))
        {
            Warning($"Could not set fullscreen to {isFullscreen}: {SDL_GetError()}");
        }
    }

    /// <summary>
    /// Raises or dismisses the on-screen keyboard. Installed by the platform launcher,
    /// because doing this needs Android APIs that <c>Splash.OpenGL</c> cannot reference.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT <c>SDL_StartTextInput</c>, which is the obvious call and the wrong
    /// one here. SDL3's <c>SDLActivity.showTextInput</c> creates its own
    /// <c>SDLDummyEdit</c> view, calls <c>requestFocus()</c> on it and shows the keyboard
    /// against THAT - so the IME would bind to <c>SDLInputConnection</c> instead of our
    /// <c>GameSurface.OnCreateInputConnection</c>. That is exactly the composition path
    /// <c>KarawanInputConnection</c> was written to replace: SDL translates IME
    /// <c>commitText</c>/<c>setComposingText</c> into backspace+retype sequences, which eat
    /// into committed text because the widget does not track the composing region. See
    /// docs/SYSTEMS/PLATFORMS/ANDROID.md, "IME Composition Bug (Fixed)".
    ///
    /// So the handler binds the IME to the game surface directly, keeping the focus - and
    /// therefore the InputConnection - where the ported code already expects it.
    /// </remarks>
    /// <summary>
    /// Cursor visibility, plus relative-motion mode, which are one gesture here.
    /// </summary>
    /// <remarks>
    /// Silk's <c>CursorMode.Raw</c> means hidden AND relative, so both halves have to move
    /// together or mouse-look breaks in a way that looks like a sensitivity bug. Relative
    /// mode is per-window in SDL3.
    ///
    /// A no-op on Android, which has no cursor - SDL reports failure there and it is not
    /// worth a warning per toggle.
    /// </remarks>
    public void SetMouseVisible(bool isVisible)
    {
        if (isVisible)
        {
            SDL_ShowCursor();
        }
        else
        {
            SDL_HideCursor();
        }

        if (_window != IntPtr.Zero)
        {
            SDL_SetWindowRelativeMouseMode(_window, !isVisible);
        }
    }

    public Action<bool>? SoftKeyboardHandler { get; set; }


    public void SetKeyboardVisible(bool isVisible)
    {
        var handler = SoftKeyboardHandler;
        if (null != handler)
        {
            /*
             * Android. The handler binds the IME to GameSurface rather than letting SDL
             * raise its own SDLDummyEdit - see the remarks above, and KI-10.
             */
            handler(isVisible);
            return;
        }

        /*
         * Desktop, and NOT a no-op however much it looks like one.
         *
         * The old remarks on IWindowBackend.SetKeyboardVisible said a desktop platform
         * "has a real keyboard and nothing visible happens either way". That was true
         * under SDL2, where text input is on by default and Silk's BeginInput/EndInput
         * merely toggled it. It is FALSE under SDL3: text input is per-window and OFF
         * until SDL_StartTextInput is called, so SDL_EVENT_TEXT_INPUT never fires and
         * INPUT_KEY_CHARACTER is never raised. Typing into a focused field produces
         * nothing at all.
         *
         * That regressed when desktop left the Silk backend (WP-3.2, sealed by WP-3.5
         * deleting GlWindowBackend and with it the BeginInput forward). It stayed
         * invisible because key events kept working perfectly - WASD, F8 and every
         * binding are scancode-driven and travel a different path - so only text entry
         * was dead, and only if someone opened a field and typed. Same shape as KI-11.
         *
         * SDL_StartTextInput is the right call HERE and the wrong one on Android, which
         * is why this sits below the handler check rather than replacing it.
         */
        if (_window == IntPtr.Zero) return;

        if (isVisible)
        {
            if (!SDL_StartTextInput(_window))
            {
                Warning($"SDL_StartTextInput failed: {SDL_GetError()}");
            }
        }
        else
        {
            SDL_StopTextInput(_window);
        }
    }


    /// <summary>
    /// The layout-dependent label for a physical key. See IWindowBackend for why this is
    /// a separate channel from the scancode and from text input.
    /// </summary>
    /// <remarks>
    /// Scancode to keycode to name, which is the whole point: SDL_GetScancodeName would be
    /// simpler and would answer the wrong question - it returns the POSITION's name ("W"
    /// for SDL_SCANCODE_W on every layout on earth), which is what we already have.
    /// SDL_GetKeyFromScancode applies the user's active layout, so the AZERTY user sees
    /// "Z" for the key they will actually press.
    ///
    /// SDL_KMOD_NONE, not the live modifier state: the label must not change to "%" while
    /// the user happens to be holding shift as they open the screen.
    ///
    /// Empty is SDL's answer for "no name", and is normalised to null so the caller falls
    /// back to the positional name rather than rendering a blank row.
    /// </remarks>
    public string? GetKeyDisplayName(engine.inputs.ScanCode scanCode)
    {
        uint key = SDL_GetKeyFromScancode((SDL_Scancode)(int)scanCode, SDL_Keymod.SDL_KMOD_NONE, false);
        if (0 == key)
        {
            return null;
        }

        string name = SDL_GetKeyName(key);
        return string.IsNullOrEmpty(name) ? null : name;
    }


    public void Run()
    {
        OnLoad?.Invoke();

        Vector2 lastSize = FramebufferSize;
        OnResize?.Invoke(lastSize);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        TimeSpan prev = stopwatch.Elapsed;

        while (!_isClosing)
        {
            // The frame budget is measured from HERE, not from where dt is taken below:
            // event pumping is part of the frame, and charging it to nobody would let the
            // loop drift above the cap under input load.
            TimeSpan frameStarted = stopwatch.Elapsed;

            // Mirrors GlWindowBackend.Run, which mirrors the pre-WP-3.3 loop. The
            // release points are not decoration - see IWindowBackend.
            ReleaseMainThreadWaiters?.Invoke();

            BeforeEvents?.Invoke();

            PumpEvents();

            if (_isClosing) break;

            TimeSpan now = stopwatch.Elapsed;
            double dt = (now - prev).TotalSeconds;
            prev = now;

            ReleaseMainThreadWaiters?.Invoke();
            OnUpdate?.Invoke(dt);

            if (_isClosing) break;

            OnRender?.Invoke(dt);

            LimitFrameRate(stopwatch, frameStarted);
        }

        ReleaseMainThreadWaiters?.Invoke();
    }

    /// <summary>
    /// Sleep out the remainder of the frame budget, replacing what Silk's <c>Run()</c> did
    /// from <c>ViewOptions.FramesPerSecond</c>.
    /// </summary>
    /// <remarks>
    /// Sleeps rather than spins: this is a phone, and a spin-wait would cost battery and
    /// heat for nothing. Millisecond granularity is coarse, but it was coarse under Silk
    /// too, and the engine already scales its own work by elapsed time.
    /// </remarks>
    private void LimitFrameRate(System.Diagnostics.Stopwatch stopwatch, TimeSpan frameStarted)
    {
        int fps = TargetFramesPerSecond;
        if (fps <= 0) return;

        double budgetMs = 1000.0 / fps;
        double usedMs = (stopwatch.Elapsed - frameStarted).TotalMilliseconds;
        int sleepMs = (int)(budgetMs - usedMs);

        if (sleepMs > 0)
        {
            System.Threading.Thread.Sleep(sleepMs);
        }
    }

    private unsafe void PumpEvents()
    {
        while (SDL_PollEvent(out SDL_Event ev))
        {
            switch ((SDL_EventType)ev.type)
            {
                case SDL_EventType.SDL_EVENT_QUIT:
                    _isClosing = true;
                    OnClosing?.Invoke();
                    break;

                case SDL_EventType.SDL_EVENT_WINDOW_RESIZED:
                case SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED:
                    OnResize?.Invoke(FramebufferSize);
                    break;

                case SDL_EventType.SDL_EVENT_KEY_DOWN:
                {
                    var (scanCode, code) = Sdl3KeyCodes.Translate(ev.key.scancode);
                    if (null != code) OnKeyPressed?.Invoke(code, scanCode);
                    break;
                }

                case SDL_EventType.SDL_EVENT_KEY_UP:
                {
                    var (scanCode, code) = Sdl3KeyCodes.Translate(ev.key.scancode);
                    if (null != code) OnKeyReleased?.Invoke(code, scanCode);
                    break;
                }

                case SDL_EventType.SDL_EVENT_TEXT_INPUT:
                {
                    // Raw UTF-8 owned by SDL. This is the IME path on Android.
                    string? text = Marshal.PtrToStringUTF8((IntPtr)ev.text.text);
                    if (!string.IsNullOrEmpty(text)) OnKeyCharacter?.Invoke(text);
                    break;
                }

                case SDL_EventType.SDL_EVENT_MOUSE_MOTION:
                    /*
                     * x/y are clamped to the window and, in relative mouse mode, warped;
                     * xrel/yrel are the true motion and are the only thing mouse-look may
                     * use. See IWindowBackend.OnMouseMoved.
                     */
                    OnMouseMoved?.Invoke(
                        new Vector2(ev.motion.x, ev.motion.y),
                        new Vector2(ev.motion.xrel, ev.motion.yrel));
                    break;

                case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN:
                {
                    int button = _toEngineMouseButton(ev.button.button);
                    if (button >= 0)
                    {
                        OnMousePressed?.Invoke(button, new Vector2(ev.button.x, ev.button.y));
                    }

                    break;
                }

                case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
                {
                    int button = _toEngineMouseButton(ev.button.button);
                    if (button >= 0)
                    {
                        OnMouseReleased?.Invoke(button, new Vector2(ev.button.x, ev.button.y));
                    }

                    break;
                }

                case SDL_EventType.SDL_EVENT_MOUSE_WHEEL:
                    /*
                     * SDL flips x/y itself when direction is FLIPPED (natural scrolling),
                     * so the sign here is already what the user meant.
                     */
                    OnMouseWheel?.Invoke(
                        new Vector2(ev.wheel.mouse_x, ev.wheel.mouse_y),
                        new Vector2(ev.wheel.x, ev.wheel.y));
                    break;

                /*
                 * Gamepads must be OPENED before SDL sends any of their events - being
                 * merely connected is not enough. Missing this is silent: the pad shows up
                 * in the device list and never produces input.
                 */
                case SDL_EventType.SDL_EVENT_GAMEPAD_ADDED:
                    _openGamepad(ev.gdevice.which);
                    break;

                case SDL_EventType.SDL_EVENT_GAMEPAD_REMOVED:
                    _closeGamepad(ev.gdevice.which);
                    break;

                case SDL_EventType.SDL_EVENT_GAMEPAD_AXIS_MOTION:
                    _onGamepadAxis((SDL_GamepadAxis)ev.gaxis.axis, ev.gaxis.value);
                    break;

                case SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_DOWN:
                {
                    string? name = Sdl3GamepadCodes.ToEngineButtonName((SDL_GamepadButton)ev.gbutton.button);
                    if (null != name)
                    {
                        OnGamepadButtonPressed?.Invoke(name, Sdl3GamepadCodes.ToEngineButtonOrdinal(name));
                    }

                    break;
                }

                case SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_UP:
                {
                    string? name = Sdl3GamepadCodes.ToEngineButtonName((SDL_GamepadButton)ev.gbutton.button);
                    if (null != name)
                    {
                        OnGamepadButtonReleased?.Invoke(name, Sdl3GamepadCodes.ToEngineButtonOrdinal(name));
                    }

                    break;
                }

                // FINGER_* deliberately absent - GameSurface.OnTouch already pushes touch.
            }
        }
    }


    /*
     * SDL numbers mouse buttons from 1 (SDL_BUTTON_LEFT); the engine uses Silk's
     * MouseButton ordinals from 0, and the number reaches game code as the event Code
     * string. Anything past middle-click has no Silk equivalent and is dropped, exactly as
     * unmapped keys are.
     */
    private static int _toEngineMouseButton(byte sdlButton) => sdlButton switch
    {
        1 => 0, // SDL_BUTTON_LEFT   -> MouseButton.Left
        3 => 1, // SDL_BUTTON_RIGHT  -> MouseButton.Right
        2 => 2, // SDL_BUTTON_MIDDLE -> MouseButton.Middle
        _ => -1
    };


    /*
     * Sticks arrive as two independent axis events, but the engine's contract is a whole
     * Vector2 per stick - InputController._onStickMoved reads both components off one
     * event. So the last value of each axis is held and the pair is re-emitted whenever
     * either half moves.
     *
     * That is also why a stick is ONE bindable Control ("GamepadStick:0") rather than two
     * axes: no event ever carries a single axis, so there is nothing to bind one to.
     */
    private readonly float[] _stickAxes = new float[4];

    private void _onGamepadAxis(SDL_GamepadAxis axis, short value)
    {
        switch (axis)
        {
            case SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX:
                _stickAxes[0] = Sdl3GamepadCodes.StickAxisToEngine(value, false);
                OnGamepadStickMoved?.Invoke(0, new Vector2(_stickAxes[0], _stickAxes[1]));
                break;

            case SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY:
                _stickAxes[1] = Sdl3GamepadCodes.StickAxisToEngine(value, true);
                OnGamepadStickMoved?.Invoke(0, new Vector2(_stickAxes[0], _stickAxes[1]));
                break;

            case SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHTX:
                _stickAxes[2] = Sdl3GamepadCodes.StickAxisToEngine(value, false);
                OnGamepadStickMoved?.Invoke(1, new Vector2(_stickAxes[2], _stickAxes[3]));
                break;

            case SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHTY:
                _stickAxes[3] = Sdl3GamepadCodes.StickAxisToEngine(value, true);
                OnGamepadStickMoved?.Invoke(1, new Vector2(_stickAxes[2], _stickAxes[3]));
                break;

            /*
             * Trigger indices are contract: since WP-6.4 part 2 they are the identity of a
             * Control ("GamepadTrigger:0"), so index 0 is what nogame.bindings.json binds
             * to "brake" and index 1 to "accelerate". Renumbering here silently swaps two
             * pedals; it does not fail to compile.
             */
            case SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFT_TRIGGER:
                OnGamepadTriggerMoved?.Invoke(0, Sdl3GamepadCodes.TriggerAxisToEngine(value));
                break;

            case SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHT_TRIGGER:
                OnGamepadTriggerMoved?.Invoke(1, Sdl3GamepadCodes.TriggerAxisToEngine(value));
                break;
        }
    }


    private readonly Dictionary<uint, IntPtr> _openGamepads = new();

    private void _openGamepad(uint instanceId)
    {
        if (_openGamepads.ContainsKey(instanceId))
        {
            return;
        }

        IntPtr pad = SDL_OpenGamepad(instanceId);
        if (IntPtr.Zero == pad)
        {
            Warning($"Could not open gamepad {instanceId}: {SDL_GetError()}");
            return;
        }

        _openGamepads[instanceId] = pad;
        Trace($"Gamepad {instanceId} opened.");
    }


    private void _closeGamepad(uint instanceId)
    {
        if (!_openGamepads.Remove(instanceId, out IntPtr pad))
        {
            return;
        }

        SDL_CloseGamepad(pad);
        Trace($"Gamepad {instanceId} closed.");
    }

    /// <summary>
    /// Sets the window icon from raw RGBA8888 pixels.
    /// </summary>
    /// <remarks>
    /// Replaces Silk's <c>IWindow.SetWindowIcon</c>, which is otherwise the last desktop
    /// feature that would be silently lost by moving off the Silk view (WP-3.2). SDL copies
    /// the pixels into its own surface, so the caller's array can be released immediately -
    /// but only once SDL_SetWindowIcon has returned, hence the pin for the whole call.
    /// </remarks>
    public unsafe void SetWindowIcon(byte[] rgba, int width, int height)
    {
        if (_window == IntPtr.Zero || null == rgba || width <= 0 || height <= 0)
        {
            return;
        }

        fixed (byte* pPixels = rgba)
        {
            SDL_Surface* surface = SDL_CreateSurfaceFrom(
                width, height, SDL_PixelFormat.SDL_PIXELFORMAT_RGBA32, (IntPtr)pPixels, width * 4);
            if (null == surface)
            {
                Warning($"Could not create icon surface: {SDL_GetError()}");
                return;
            }

            if (!SDL_SetWindowIcon(_window, (IntPtr)surface))
            {
                Warning($"Could not set the window icon: {SDL_GetError()}");
            }

            SDL_DestroySurface((IntPtr)surface);
        }
    }

    public void Close()
    {
        _isClosing = true;
    }

    public void Dispose()
    {
        if (_glContext != IntPtr.Zero)
        {
            SDL_GL_DestroyContext(_glContext);
            _glContext = IntPtr.Zero;
        }

        if (_window != IntPtr.Zero)
        {
            SDL_DestroyWindow(_window);
            _window = IntPtr.Zero;
        }

        SDL_Quit();
    }
}
