using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Input;
using static SDL3.SDL;

namespace Splash.Silk;

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

    public Vector2 Size => FramebufferSize;

    /// <summary>
    /// Drawable size in pixels.
    /// </summary>
    /// <remarks>
    /// <c>Size</c> and <c>FramebufferSize</c> are deliberately the same here. On Android the
    /// SDL window IS the drawable - there is no separate logical window size - and reporting
    /// a different pair would desynchronise the renderer's projection from the hit-test
    /// rectangle, which is precisely the drift <c>_applyFramebufferSize</c> warns about.
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

    /// <summary>
    /// Always null: this backend feeds <c>engine.news.EventQueue</c> itself.
    /// </summary>
    /// <remarks>
    /// Touch is NOT translated here. <c>Wuka.GameSurface.OnTouch</c> has always pushed touch
    /// events into the queue from the Android view layer, and it still does. Translating
    /// SDL's <c>FINGER_*</c> events as well would deliver every touch twice - which is why
    /// the old raw-SDL2 <c>PeepEvents</c> block in <c>GameActivity</c> was left disabled
    /// behind <c>#if false</c>.
    /// </remarks>
    public IInputContext? SilkInputContext => null;

    public Platform.UnderlyingFrameworks UnderlyingFramework => Platform.UnderlyingFrameworks.Sdl;

    public Action? OnLoad { get; set; }
    public Action<Vector2>? OnResize { get; set; }
    public Action<double>? OnUpdate { get; set; }
    public Action<double>? OnRender { get; set; }
    public Action? OnClosing { get; set; }
    public Action<bool>? OnFocusChanged { get; set; }
    public Action? BeforeEvents { get; set; }
    public Action? ReleaseMainThreadWaiters { get; set; }
    public Action<string>? OnKeyPressed { get; set; }
    public Action<string>? OnKeyReleased { get; set; }
    public Action<string>? OnKeyCharacter { get; set; }

    /// <summary>
    /// Creates the window and the GLES 3.0 context.
    /// </summary>
    /// <remarks>
    /// Done in the constructor rather than in <see cref="Run"/> because
    /// <see cref="GetProcAddress"/> has to be usable before Platform's load handler runs -
    /// that handler is what calls <c>GL.GetApi</c>.
    /// </remarks>
    public Sdl3WindowBackend(string title)
    {
        if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO | SDL_InitFlags.SDL_INIT_EVENTS))
        {
            throw new InvalidOperationException($"SDL_Init failed: {SDL_GetError()}");
        }

        // Must be set BEFORE the window exists; SDL bakes them into the EGL config it picks.
        SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_PROFILE_MASK, SDL_GL_CONTEXT_PROFILE_ES);
        SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_MAJOR_VERSION, 3);
        SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_MINOR_VERSION, 0);
        SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_DEPTH_SIZE, 16);

        // On Android the size arguments are ignored - SDL binds to the activity's existing
        // surface - but SDL_WINDOW_OPENGL is what makes it an EGL surface.
        _window = SDL_CreateWindow(title, 0, 0, SDL_WindowFlags.SDL_WINDOW_OPENGL);
        if (_window == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SDL_CreateWindow failed: {SDL_GetError()}");
        }

        _glContext = SDL_GL_CreateContext(_window);
        if (_glContext == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SDL_GL_CreateContext failed: {SDL_GetError()}");
        }
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

    /// <summary>No-op: Android decides fullscreen, and the activity is already fullscreen.</summary>
    public void SetFullscreen(bool isFullscreen)
    {
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
            // Mirrors SilkWindowBackend.Run, which mirrors the pre-WP-3.3 loop. The
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
        }

        ReleaseMainThreadWaiters?.Invoke();
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
                    string? code = Sdl3KeyCodes.ToEngineCode(ev.key.scancode);
                    if (null != code) OnKeyPressed?.Invoke(code);
                    break;
                }

                case SDL_EventType.SDL_EVENT_KEY_UP:
                {
                    string? code = Sdl3KeyCodes.ToEngineCode(ev.key.scancode);
                    if (null != code) OnKeyReleased?.Invoke(code);
                    break;
                }

                case SDL_EventType.SDL_EVENT_TEXT_INPUT:
                {
                    // Raw UTF-8 owned by SDL. This is the IME path on Android.
                    string? text = Marshal.PtrToStringUTF8((IntPtr)ev.text.text);
                    if (!string.IsNullOrEmpty(text)) OnKeyCharacter?.Invoke(text);
                    break;
                }

                // FINGER_* deliberately absent - GameSurface.OnTouch already pushes touch.
            }
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
