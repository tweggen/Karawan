using System.Runtime.InteropServices;
using static SDL3.SDL;

namespace Sdl3Spike;

/// <summary>
/// The actual question WP-2.1 exists to answer: on a real device, can we get a GLES 3.0
/// context out of SDL3 and resolve GL entry points through
/// <c>SDL_GL_GetProcAddress</c>?
/// </summary>
/// <remarks>
/// Every step logs, and the log lines are the spike's output. A spike that renders a
/// blue screen but cannot say which GL version it got has answered nothing - the plan
/// is explicit that the deliverable is an answer.
/// </remarks>
internal static class SpikeRenderer
{
    // Resolved through SDL_GL_GetProcAddress rather than DllImport("libGLESv3.so").
    // That is the mechanism Phase 5's generated bindings will use, so proving it here
    // de-risks WP-5.2 as a side effect.
    private delegate void GlClearColorFn(float r, float g, float b, float a);
    private delegate void GlClearFn(uint mask);
    private delegate IntPtr GlGetStringFn(uint name);
    private delegate void GlViewportFn(int x, int y, int w, int h);

    private const uint GL_COLOR_BUFFER_BIT = 0x00004000;
    private const uint GL_VENDOR = 0x1F00;
    private const uint GL_RENDERER = 0x1F01;
    private const uint GL_VERSION = 0x1F02;

    private const int SDL_GL_CONTEXT_PROFILE_ES = 0x0004;

    // Held in a static field for the process lifetime. SDL stores the raw function
    // pointer; if the GC collected the delegate the next lifecycle transition would
    // jump into freed memory - a crash on resume, long after the cause.
    private static SDL_EventFilter s_lifecycleWatch;

    /// <summary>
    /// The Android/iOS app-lifecycle events <b>cannot be observed through
    /// <c>SDL_PollEvent</c></b>. SDL_events.h says of every one of them: "This event must
    /// be handled in a callback set with SDL_AddEventWatch()."
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is not a timing hazard, it is absolute. <c>SDL_SendAppEvent</c> special-cases
    /// these six types and <b>never puts them on the event queue</b>
    /// (<c>src/events/SDL_events.c</c>): <i>"We won't actually queue this event, it needs
    /// to be handled in this call stack by an event watcher"</i>. It calls
    /// <c>SDL_CallEventWatchers</c> instead. So no amount of polling can ever see them.
    /// </para>
    /// <para>
    /// The reason SDL does this is Android's pause semantics, spelled out in
    /// <c>SDL_androidevents.c</c>: <i>"as soon as the enter background event has been
    /// queued, the app will block. The application should do any life cycle handling in
    /// an event filter while the event was being queued."</i> The app is about to stop
    /// getting CPU, so the notification has to be synchronous or it is worthless.
    /// </para>
    /// <para>
    /// <b>Threading:</b> the callback runs on the <b>SDL thread</b> - the same thread that
    /// called <c>SDL_PollEvent</c> - because the chain is
    /// <c>SDL_PollEvent</c> → <c>SDL_PumpEvents</c> → <c>Android_PumpEvents</c> →
    /// <c>Android_OnPause</c> → <c>SDL_SendAppEvent</c> → watchers, all one call stack.
    /// It is NOT the Android UI thread; the UI thread only enqueues a lifecycle token that
    /// the SDL thread picks up. So game state may be touched directly here, with no
    /// cross-thread hazard.
    /// </para>
    /// <para>
    /// <b>This is a direct constraint on WP-3.2.</b> <c>Wuka</c>'s
    /// <c>GameActivity.OnStop</c> saves the game (<c>I.Get&lt;engine.Saver&gt;()?.Save</c>).
    /// Once SDL3 owns the activity, that hook has to hang off an event watch - ported as a
    /// polled event it would never run at all, and the failure mode is "the game stopped
    /// saving", noticed days later.
    /// </para>
    /// </remarks>
    private static unsafe bool LifecycleWatch(IntPtr userdata, SDL_Event* evt, Action<string> log)
    {
        switch ((SDL_EventType)evt->type)
        {
            case SDL_EventType.SDL_EVENT_WILL_ENTER_BACKGROUND: log("WATCH: WILL_ENTER_BACKGROUND"); break;
            case SDL_EventType.SDL_EVENT_DID_ENTER_BACKGROUND:  log("WATCH: DID_ENTER_BACKGROUND"); break;
            case SDL_EventType.SDL_EVENT_WILL_ENTER_FOREGROUND: log("WATCH: WILL_ENTER_FOREGROUND"); break;
            case SDL_EventType.SDL_EVENT_DID_ENTER_FOREGROUND:  log("WATCH: DID_ENTER_FOREGROUND"); break;
            case SDL_EventType.SDL_EVENT_TERMINATING:           log("WATCH: TERMINATING"); break;
            case SDL_EventType.SDL_EVENT_LOW_MEMORY:            log("WATCH: LOW_MEMORY"); break;
        }

        // true = keep the event in the queue. A watch must not swallow events.
        return true;
    }

    // unsafe: SDL_Event's text payload is a raw byte*.
    public static unsafe void Run(Action<string> log)
    {
        log("SDL_Init(VIDEO|EVENTS)");
        if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO | SDL_InitFlags.SDL_INIT_EVENTS))
        {
            throw new InvalidOperationException($"SDL_Init failed: {SDL_GetError()}");
        }

        // Registered immediately after SDL_Init, before the window exists, so no
        // lifecycle transition can slip past during startup.
        s_lifecycleWatch = (userdata, evt) => LifecycleWatch(userdata, evt, log);
        if (!SDL_AddEventWatch(s_lifecycleWatch, IntPtr.Zero))
        {
            throw new InvalidOperationException($"SDL_AddEventWatch failed: {SDL_GetError()}");
        }

        // GLES 3.0 must be requested BEFORE the window is created; SDL bakes the
        // attributes into the EGL config it picks.
        Check(SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_PROFILE_MASK, SDL_GL_CONTEXT_PROFILE_ES), "profile=ES");
        Check(SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_MAJOR_VERSION, 3), "major=3");
        Check(SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_MINOR_VERSION, 0), "minor=0");
        Check(SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_DEPTH_SIZE, 16), "depth=16");

        // On Android the size arguments are ignored - SDL binds to the Activity's
        // existing surface - but SDL_WINDOW_OPENGL is what makes it an EGL surface.
        IntPtr window = SDL_CreateWindow("SDL3 Spike", 0, 0, SDL_WindowFlags.SDL_WINDOW_OPENGL);
        if (window == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SDL_CreateWindow failed: {SDL_GetError()}");
        }

        IntPtr ctx = SDL_GL_CreateContext(window);
        if (ctx == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SDL_GL_CreateContext failed: {SDL_GetError()}");
        }

        var glClearColor = Load<GlClearColorFn>("glClearColor");
        var glClear = Load<GlClearFn>("glClear");
        var glGetString = Load<GlGetStringFn>("glGetString");
        var glViewport = Load<GlViewportFn>("glViewport");

        // THE answer line. If this says GLES 3.x, WP-2.1 has succeeded.
        log($"GL_VENDOR   = {Str(glGetString, GL_VENDOR)}");
        log($"GL_RENDERER = {Str(glGetString, GL_RENDERER)}");
        log($"GL_VERSION  = {Str(glGetString, GL_VERSION)}");

        SDL_GetWindowSizeInPixels(window, out int pw, out int ph);
        log($"drawable    = {pw}x{ph}");
        glViewport(0, 0, pw, ph);

        bool running = true;
        int frame = 0;

        while (running)
        {
            while (SDL_PollEvent(out SDL_Event ev))
            {
                switch ((SDL_EventType)ev.type)
                {
                    case SDL_EventType.SDL_EVENT_QUIT:
                    case SDL_EventType.SDL_EVENT_TERMINATING:
                        running = false;
                        break;

                    // GATE-A evidence. These are logged rather than acted on: the human
                    // running the spike needs to see that multi-touch, rotation and IME
                    // actually reach us, which is the part of AC-2.4 that cannot be
                    // asserted from a build.
                    case SDL_EventType.SDL_EVENT_FINGER_DOWN:
                        log($"FINGER_DOWN  id={ev.tfinger.fingerID} x={ev.tfinger.x:F3} y={ev.tfinger.y:F3}");
                        break;
                    case SDL_EventType.SDL_EVENT_FINGER_UP:
                        log($"FINGER_UP    id={ev.tfinger.fingerID}");
                        break;
                    case SDL_EventType.SDL_EVENT_TEXT_INPUT:
                        // ev.text.text is a raw byte* to a UTF-8 string owned by SDL.
                        // This is THE line GATE-A hinges on: it is the evidence that the
                        // Android soft keyboard reached us through SDL3's reworked text
                        // input path, which ADR claim 8 flags as the likeliest failure in
                        // the whole migration.
                        log($"TEXT_INPUT   '{Marshal.PtrToStringUTF8((IntPtr)ev.text.text)}'");
                        break;
                    case SDL_EventType.SDL_EVENT_KEY_DOWN:
                        log($"KEY_DOWN     scancode={ev.key.scancode} key={ev.key.key}");
                        break;
                    case SDL_EventType.SDL_EVENT_WINDOW_RESIZED:
                        SDL_GetWindowSizeInPixels(window, out pw, out ph);
                        glViewport(0, 0, pw, ph);
                        log($"RESIZED      {pw}x{ph}");
                        break;
                        // NOTE: the app lifecycle events (WILL_ENTER_BACKGROUND,
                        // DID_ENTER_FOREGROUND, TERMINATING, LOW_MEMORY) are deliberately
                        // NOT handled here. They cannot be - see _lifecycleWatch below.
                }
            }

            // Slow colour cycle, so "is it actually redrawing?" is answerable by looking
            // at the screen rather than by trusting the absence of errors.
            float t = frame / 120.0f;
            glClearColor(0.5f + 0.5f * MathF.Sin(t), 0.2f, 0.5f + 0.5f * MathF.Cos(t), 1.0f);
            glClear(GL_COLOR_BUFFER_BIT);
            SDL_GL_SwapWindow(window);

            if (frame == 0)
            {
                log("first frame presented");
            }

            frame++;
        }

        log($"exiting after {frame} frames");
        SDL_Quit();
    }

    private static void Check(SDLBool result, string what)
    {
        if (!result)
        {
            throw new InvalidOperationException($"SDL_GL_SetAttribute({what}) failed: {SDL_GetError()}");
        }
    }

    private static T Load<T>(string name) where T : Delegate
    {
        IntPtr p = SDL_GL_GetProcAddress(name);
        if (p == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SDL_GL_GetProcAddress('{name}') returned null");
        }

        return Marshal.GetDelegateForFunctionPointer<T>(p);
    }

    private static string Str(GlGetStringFn glGetString, uint name)
    {
        IntPtr p = glGetString(name);
        return p == IntPtr.Zero ? "<null>" : Marshal.PtrToStringAnsi(p);
    }
}
