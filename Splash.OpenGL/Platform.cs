using System;
using System.Diagnostics;
using System.Numerics;
using builtin.controllers;
using engine;
using engine.news;
using ObjLoader.Loader.Common;
using static engine.Logger;

using Splash.API.OpenGL;

namespace Splash.OpenGL;


public class Platform : engine.IPlatform
{
    private object _lo = new object();
    private engine.Engine _engine;

    /**
     * Keeps all Splash implementations.
     */
    private Splash.Common _common;

    private GlThreeD _silkThreeD;
    private InstanceManager _instanceManager;
    private CameraManager _cameraManager;
    private GlRenderer _renderer;
    private InputMapper _inputMapper;

    private RenderStats _renderStats = new();
    private bool _isRunning = true;

    private LogicalRenderer _logicalRenderer;
    private readonly Stopwatch _frameTimingStopwatch = new();
    private readonly Stopwatch _renderSingleFrameStopwatch = new();
    private TimeSpan _prevFrame;

    private Splash.OpenGL.ImGui.Controller _imGuiController = null;

    /*
     * WP-3.3: this used to be a Silk.NET.Windowing.IView. It is now the backend seam, so
     * that Android can supply an SDL3 window instead. See IWindowBackend for why.
     */
    private IWindowBackend _backend;
    private GL _gl;
    
    private engine.scheduler.WorkerQueue _platformThreadActions = new("platformThread");

    /**
     * If mouse is enabled, we intercept mouse events when the pointer is outside the viewport area.
     */
    private bool _mouseEnabled = false;

    public bool MouseEnabled
    {
        get => _mouseEnabled;
        set => _platformThreadActions.Enqueue(() => _setMouseEnabled(value));
    }


    private bool _keyboardEnabled = false;
    private string _keyboardInputType = "text";

    public bool KeyboardEnabled
    {
        get => _keyboardEnabled;
        set => _platformThreadActions.Enqueue(() => _setKeyboardEnabled(value));
    }

    public string KeyboardInputType
    {
        get => _keyboardInputType;
        set => _keyboardInputType = value;
    }


    public void SetEngine(engine.Engine engine)
    {
        lock (_lo)
        {
            _engine = engine;
        }
    }


    private void _toggleFullscreen()
    {
        bool isFullscreen = _engine.IsFullscreen();
        _engine.SetFullscreen(!isFullscreen);
    }


    public void SetFullscreen(bool isFullscreen)
    {
        if (null == _backend)
        {
            return;
        }

        _backend.SetFullscreen(isFullscreen);
    }


    public string? GetKeyDisplayName(engine.inputs.ScanCode scanCode)
        => _backend?.GetKeyDisplayName(scanCode);

    
    /**
     * Push an event and optionally its translated logical version.
     * This replicates the InputMapper.EmitPlusTranslation functionality
     * but handles the case where InputMapper is not available (e.g., minimal examples).
     */
    private void _pushTranslate(in Event ev)
    {
        var eq = I.Get<EventQueue>();
        if (null == eq)
        {
            return;
        }
        
        // Always push the original event
        eq.Push(ev);
        
        // If InputMapper is available, also push the translated logical event
        if (null != _inputMapper)
        {
            Event? evLogical = _inputMapper.ToLogical(ev);
            if (null != evLogical)
            {
                eq.Push(evLogical);
            }
        }
    }


    /**
     * Caution:
     * - on Windows, with raw mouse, position is just an accumulated sum of the delta moves
     * - on Windows, using Teamviewer, the coordinates are insanely high.
     * - on Android, I didn't quite understand the math yet.
     */
    /*
     * WP-3.1: the bodies below are the CONTRACT - the engine.news.Event each raw input
     * produces. They are deliberately split from the Silk-specific signatures so that the
     * SDL3 backend can reach exactly the same code through IWindowBackend's callbacks.
     * Two paths building "the same" event independently is how they drift.
     */


    private void _pushMousePressed(int mouseButton, Vector2 mousePosition)
    {
        _imGuiController?.FeedMouseMoved(mousePosition);
        _imGuiController?.FeedMouseButton(mouseButton, true);
        if (_imGuiController?.WantCaptureMouse == true) return;
        if (_shallReturnBecauseUI(mousePosition)) return;

        _fullToViewPosition(mousePosition, out var pos, out var size, out var v2LogicalPosition);

        I.Get<EventQueue>().Push(
            new Event(Event.INPUT_MOUSE_PRESSED, $"{mouseButton}")
            {
                PhysicalPosition = pos,
                PhysicalSize = size,
                LogicalPosition = v2LogicalPosition,
                Data1 = (uint) mouseButton
            });
        I.Get<EventQueue>().Push(
            new Event(Event.INPUT_TOUCH_PRESSED, "")
            {
                PhysicalPosition = pos,
                PhysicalSize = size,
                LogicalPosition = v2LogicalPosition
            });
    }


    private void _pushMouseReleased(int mouseButton, Vector2 mousePosition)
    {
        _imGuiController?.FeedMouseButton(mouseButton, false);
        if (_shallReturnBecauseUI(mousePosition)) return;

        _fullToViewPosition(mousePosition, out var pos, out var size, out var v2LogicalPosition);

        I.Get<EventQueue>().Push(
            new Event(Event.INPUT_MOUSE_RELEASED, $"{mouseButton}")
            {
                PhysicalPosition = pos,
                PhysicalSize = size,
                LogicalPosition = v2LogicalPosition,
                Data1 = (uint) mouseButton
            });
        I.Get<EventQueue>().Push(
            new Event(Event.INPUT_TOUCH_RELEASED, "")
            {
                PhysicalPosition = pos,
                PhysicalSize = size,
                LogicalPosition = v2LogicalPosition 
            });
    }


    private void _pushMouseMoved(Vector2 position, Vector2 delta)
    {
        _imGuiController?.FeedMouseMoved(position);
        if (_shallReturnBecauseUI(position)) return;

        I.Get<EventQueue>().Push(new Event(Event.INPUT_MOUSE_MOVED, "")
        {
            PhysicalPosition = position,
            PhysicalDelta = delta
        });
    }


    private void _pushMouseWheel(Vector2 mousePosition, Vector2 delta)
    {
        if (_shallReturnBecauseUI(mousePosition)) return;

        I.Get<EventQueue>().Push(new Event(Event.INPUT_MOUSE_WHEEL, "")
        {
            PhysicalPosition = delta
        });
    }


    /*
     * The original tested _shallReturnBecauseUI(mouse.Position) while pushing the `position`
     * argument. Silk raises MouseMove after updating the property, so the two are the same
     * value and collapsing them changes nothing - but it is worth stating, because if they
     * ever diverged this is where a hit-test would silently disagree with the event it
     * guards.
     */



    private void _getActualViewRectangle(out Vector2 ul, out Vector2 lr)
    {
        _engine.GetViewRectangle(out ul, out lr);
        if (Vector2.Zero == lr)
        {
            lr = _backend.Size - Vector2.One;
        }
    }

    private void _fullToViewPosition(in Vector2 i, out Vector2 o, out Vector2 s, out Vector2 logical)
    {
        _getActualViewRectangle(out var ul, out var lr);
        o = i - ul;
        s = lr - ul + Vector2.One;
        logical = (s.X != 0f && s.Y != 0f) ? new(o.X / s.X, o.Y / s.Y) : Vector2.Zero;
    }


    private bool _shallReturnBecauseUI(in Vector2 v2MousePos)
    {
        if (false == _mouseEnabled) return false;
        _getActualViewRectangle(out var ul, out var lr);
        if (v2MousePos.X < ul.X || v2MousePos.X > lr.X || v2MousePos.Y < ul.Y || v2MousePos.Y > lr.Y)
        {
            return true;
        }
        return false;
    }


    private void _pushGamepadStickMoved(int stickIndex, Vector2 position)
    {
        _pushTranslate(new Event(Event.INPUT_GAMEPAD_STICK_MOVED, "")
            {
                PhysicalPosition = position,
                Data1 = (uint) stickIndex
            });
    }


    private void _pushGamepadTriggerMoved(int triggerIndex, float position)
    {
        I.Get<EventQueue>().Push(
            new Event(Event.INPUT_GAMEPAD_TRIGGER_MOVED, "")
            {
                PhysicalPosition = new(position, 0f),
                Data1 = (uint) triggerIndex
            });
    }


    private void _pushGamepadButtonPressed(string buttonName, uint buttonOrdinal, uint buttonIndex)
    {
        _pushTranslate(
            new Event(Event.INPUT_GAMEPAD_BUTTON_PRESSED, buttonName)
            {
                Data1 = buttonOrdinal,
                Data2 = buttonIndex
            });
    }


    private void _pushGamepadButtonReleased(string buttonName, uint buttonOrdinal, uint buttonIndex)
    {
        _pushTranslate(
            new Event(Event.INPUT_GAMEPAD_BUTTON_RELEASED, buttonName)
            {
                Data1 = buttonOrdinal,
                Data2 = buttonIndex
            });
    }






    private bool _hadFocus = true;


    private void _setMouseEnabled(bool value)
    {
        /*
         * We better not set the mouse to raw on android.
         */
        if (GlobalSettings.Get("Android") == "true") return;
        if (
                GlobalSettings.Get("debug.option.mouseEnabled") == "true"
#if DEBUG
            || System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux)
#endif
            )
        {
            value = true;
        }


        _mouseEnabled = value;

        /*
         * Each backend owns the gesture now. Guarding the null input context here - as
         * WP-3.1 did - was not enough: it stopped the crash but left the cursor never
         * configured at all on SDL3, which GATE-C saw as a visible cursor in fullscreen.
         */
        _backend.SetMouseVisible(value);
    }


    private void _setKeyboardEnabled(bool value)
    {
        /*
         * We better not set the mouse to raw on android.
         */
        //if (GlobalSettings.Get("Android") == "true") return;

#if DEBUG
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform
                .Linux))
        {
            value = true;
        }
#endif

        _keyboardEnabled = value;

        _backend.SetKeyboardVisible(value);
    }


    public enum UnderlyingFrameworks
    {
        Unknown,
        Glfw,
        Sdl
    }

    private UnderlyingFrameworks _underlyingFrameworks = UnderlyingFrameworks.Unknown;
    

    private void _windowOnLoad()
    {
        _frameTimingStopwatch.Start();
        _prevFrame = _frameTimingStopwatch.Elapsed;
        
        // TXWTODO: Create sort of "on new gl window" event.
        /*
         * GATE-F: optionally interpose on entry-point resolution.
         *
         * GLTrace hands back thunks instead of the driver's pointers, so every GL call
         * becomes observable without wrapping the binding - and because BOTH bindings
         * resolve through this one Func, the same tracer observes the Silk build and the
         * generated build identically. Off unless debug.option.glTraceAnchor is set, and
         * then it costs one dictionary lookup per entry point ONCE, at resolution.
         */
        Func<string, nint> getProc = _backend.GetProcAddress;
        if (!string.IsNullOrEmpty(engine.GlobalSettings.Get("debug.option.glTraceAnchor")))
        {
            getProc = GLTrace.Wrap(name => _backend.GetProcAddress(name));
            GlTraceAnchor.Install();
            Trace($"GL tracing armed for anchor "
                  + engine.GlobalSettings.Get("debug.option.glTraceAnchor"));
        }

        _gl = GL.GetApi(getProc);

        if (GLTrace.TracedCount > 0)
        {
            Trace($"GLTrace: {GLTrace.TracedCount} entry points traced, "
                  + $"{GLTrace.Untraced.Count} passed through untraced"
                  + (GLTrace.Untraced.Count > 0 ? ": " + string.Join(",", GLTrace.Untraced) : ""));
        }
        _silkThreeD.SetGL(_gl);

        /*
         * WP-5.3 / KI-11: the desktop debug UI is back.
         *
         * It went dark at WP-3.2 and was made explicit at WP-3.5: the controller wanted a
         * Silk IInputContext and a Silk IView, and no surviving backend has either. It now
         * takes IWindowBackend and is FED input from the callbacks below, so nothing about
         * it depends on which windowing library is underneath.
         *
         * Gated on nogame.CreateUI, the same setting that gates the debug UI module, and
         * skipped on GLES: there is no Android build of cimgui at all (PR #13 excluded the
         * native), so constructing it there would fail at the first draw rather than here.
         */
        if (engine.GlobalSettings.Get("nogame.CreateUI") == "true"
            && engine.GlobalSettings.Get("platform.threeD.API") == "OpenGL")
        {
            try
            {
                _imGuiController = new Splash.OpenGL.ImGui.Controller(_gl, _backend);
            }
            catch (Exception e)
            {
                /*
                 * Not fatal. A missing cimgui native or a context the backend cannot
                 * satisfy should cost the debug overlay, not the game - and the previous
                 * arrangement failed silently, which is how this went unnoticed for two
                 * work packages.
                 */
                Error($"Unable to create the ImGui controller, debug UI disabled: {e}");
                _imGuiController = null;
            }
        }
        _gl.ClearDepth(1f);
        _gl.ClearColor(0f, 0f, 0f, 0f);

        _hadFocus = true;

        /*
         * Apply the cursor state ONCE at startup, so the window begins in the state the
         * engine believes it is in rather than in whatever the windowing library defaults to.
         *
         * Without this the setter is the only thing that ever configures the cursor - and
         * nothing calls it: engine.Engine.SetMouseEnabled has no caller in the game, so
         * _mouseEnabled sits at its initial false while SDL and GLFW both show a cursor. That
         * is why GATE-C saw one in fullscreen.
         *
         * NOTE this now takes effect on BOTH backends, so the cursor is hidden by default.
         * If a menu needs one, the fix belongs in game code - Engine.SetMouseEnabled(true)
         * while the menu is open - not in a windowing default.
         */
        _backend.SetMouseVisible(_mouseEnabled);

#if DEBUG
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Linux))
        {
            _setMouseEnabled(true);
        }
#endif
        _engine.CallOnPlatformAvailable();
        _renderSingleFrameStopwatch.Start();
    }


    private void _windowOnUpdate(double dt)
    {

    }


    /**
     * Some platforms (I'm looking at you, windows) lack a reasonably short
     * system timer, so we have a condition variable that we trigger from time to time.
     */
    private void _triggerWaitMonitor()
    {
        lock (_engine.ShortSleep)
        {
            System.Threading.Monitor.Pulse(_engine.ShortSleep);
        }
    }


    private static int _frameNo = 0;

    /**
     * OnRender for silk.
     *
     * As silk busyloops for a new frame, we better wait for the better
     * part of it for a new frame.
     */
    private void _windowOnRender(double dt)
    {
        if (!_isRunning) return;
        
        TimeSpan tsNow = _frameTimingStopwatch.Elapsed;
        TimeSpan tsGotFrame;
        
        RenderFrame renderFrame;
        while (true)
        {
            _triggerWaitMonitor();

            if (null != _logicalRenderer)
            {
                renderFrame = _logicalRenderer.WaitNextRenderFrame();
                tsGotFrame = _frameTimingStopwatch.Elapsed;
            }
            else
            {
                renderFrame = null;
                // TXWTODO: Render black?
                return;
            }

            if (null == renderFrame)
            {
                Trace($"No frame.");
                if (false == _isRunning)
                {
                    return;
                }
            }
            else
            {
                break;
            }
        }

        _renderSingleFrameStopwatch.Reset();
        _renderSingleFrameStopwatch.Start();
        double msGotFrame = _renderSingleFrameStopwatch.Elapsed.TotalMilliseconds;

        _applyFramebufferSize(_backend.FramebufferSize);

        _renderer.RenderFrame(renderFrame);
        double msRendered = _renderSingleFrameStopwatch.Elapsed.TotalMilliseconds;

        _renderStats.PushFrame(renderFrame.FrameStats);
        I.Get<EventQueue>().Push(new Event(Event.RENDER_STATS, _renderStats.GetAverage().ToString()));

        if (null != _imGuiController)
        {
            _triggerWaitMonitor();

            _imGuiController.Update((float)dt);
            _engine.CallOnImGuiRender((float)dt);
            _imGuiController.Render();
        }

        _triggerWaitMonitor();

        _backend.SwapBuffers();
        double msSwap = _renderSingleFrameStopwatch.Elapsed.TotalMilliseconds;

        _triggerWaitMonitor();

        _silkThreeD.ExecuteGraphicsThreadActions(0.001f);
        double msAfterGraphicsThread = _renderSingleFrameStopwatch.Elapsed.TotalMilliseconds;

        _triggerWaitMonitor();

        ++_frameNo;
        _engine.CallOnPhysicalFrame((float)dt);

        _triggerWaitMonitor();

        _platformThreadActions.RunPart(1000f);
        double msAfterPlatformThread = _renderSingleFrameStopwatch.Elapsed.TotalMilliseconds;
        
        _renderSingleFrameStopwatch.Stop();
        // Trace($"after {(tsNow-_prevFrame).TotalMilliseconds} Took {_renderSingleFrameStopwatch.Elapsed.TotalMilliseconds}, waited {(tsGotFrame-tsNow).TotalMilliseconds} got {msGotFrame} dr {msRendered-msGotFrame} aftergfx {msAfterGraphicsThread-msRendered} afterpf {msAfterPlatformThread-msAfterGraphicsThread} ");
        _prevFrame = tsNow;
        
        _triggerWaitMonitor();
    }

    private void _windowOnClose()
    {
        if (null != _imGuiController)
        {
            _imGuiController?.Dispose();
        }

        _instanceManager?.Dispose();
        /*
         * WP-5.2: the generated binding is not IDisposable, and has nothing to dispose - it
         * holds a resolver delegate and lazily-created P/Invoke delegates, no GL objects
         * and no unmanaged allocation. Silk's GL implemented IDisposable for its own
         * loader bookkeeping, which does not exist here.
         */
        _isRunning = false;
        _logicalRenderer.ShallQuit = true;
    }


    private Vector2 _v2LastFramebufferSize = new(0, 0);

    /**
     * Publish the current framebuffer size to everybody deriving geometry from it.
     *
     * This must stay the single place where the view size is announced. The renderer
     * builds its projection from _renderer.SetDimension(), while everything doing the
     * inverse transformation (Camera3.GetViewSize() for click/touch hit testing,
     * InputController, the map module) reads the "view.size" global setting. If only
     * one of the two is updated, rendered geometry and hit rectangles use different
     * aspect ratios and drift apart the further you get from the center of the screen.
     *
     * Called per rendered frame rather than from the resize event alone: on Android the
     * SDL window is created at its final size, so IView.Resize never fires and the
     * announcement would otherwise never happen.
     */
    private void _applyFramebufferSize(in Vector2 fbSize)
    {
        // WP-3.3: was Vector2D<int> (a Silk type). Same values, same integer semantics -
        // the backend reports whole pixels - but expressible without Silk in the seam.
        int w = (int)fbSize.X;
        int h = (int)fbSize.Y;

        if (w == 0 || h == 0) return;
        if (fbSize == _v2LastFramebufferSize) return;
        _v2LastFramebufferSize = fbSize;

        /*
         * Pixels AND the logical size they correspond to. The renderer needs both: it
         * draws in pixels, but the engine's view rectangle - which decides where the 3D
         * viewport starts when the debug pane is open - is in logical units.
         */
        Vector2 logical = _backend.Size;

        // TXWTODO: We are abusing the global settings as global variables.
        _renderer.SetDimension(w, h, (int)logical.X, (int)logical.Y);
        engine.GlobalSettings.Set("view.size", $"{w}x{h}");
        I.Get<EventQueue>().Push(new Event(Event.VIEW_SIZE_CHANGED, "")
        {
            PhysicalPosition = new(w, h)
        });
    }


    private void _windowOnResize(Vector2 size)
    {
        if (size.X != 0 && size.Y != 0)
        {
            _applyFramebufferSize(_backend.FramebufferSize);
        }
    }


    private void _windowOnFocusChanged(bool haveFocus)
    {
        if (haveFocus)
        {
            if (!_hadFocus)
            {
                _hadFocus = true;
                if (GlobalSettings.Get("platform.suspendOnUnfocus") != "false")
                {
                    _engine.SetEngineState(Engine.EngineState.Starting);
                    _engine.SetEngineState(Engine.EngineState.Running);
                    _engine.Resume();
                }
            }
        }
        else
        {
            if (_hadFocus)
            {
                _hadFocus = false;
                if (GlobalSettings.Get("platform.suspendOnUnfocus") != "false")
                {
                    _engine.Suspend();
                    _engine.SetEngineState(Engine.EngineState.Stopping);
                    _engine.SetEngineState(Engine.EngineState.Stopped);
                }
            }
        }
    }


    public Action BeforeDoEvent = null;
    

    public void Execute()
    {
        // InputMapper is optional - minimal examples may not have it registered
        _inputMapper = I.TryGet<builtin.controllers.InputMapper>();


        /*
         * WP-3.3: the loop itself now lives in the backend, because Silk and SDL3 enter it
         * from opposite directions - Silk calls us back from IView.Run, while on Android
         * SDL owns the thread and we are already inside it via libmain.so. What used to be
         * inline here is supplied as callbacks instead; GlWindowBackend.Run reproduces
         * the previous body exactly, including where _triggerWaitMonitor was called.
         */
        _backend.BeforeEvents = () => BeforeDoEvent?.Invoke();
        _backend.ReleaseMainThreadWaiters = _triggerWaitMonitor;

        _backend.Run();
        _backend.Dispose();
    }


    public void CollectRenderData(engine.IScene scene)
    {
        _logicalRenderer.CollectRenderData(scene);
    }

    
    /**
     * Call this after all dependencies are created.
     */
    public void SetupDone()
    {
        _common = new();
        engine.GlobalSettings.Set("view.size", "320x200");

        string baseDirectory = System.AppContext.BaseDirectory;
        System.Console.WriteLine($"Running in directory {baseDirectory}");

        if (_backend != null)
        {
            /*
             * Which windowing library is underneath. Previously sniffed from the IView's
             * type name ("Glfw" / "Sdl"), which only ever worked because Silk names its
             * view types after their backend. The backend now says so directly.
             */
            _underlyingFrameworks = _backend.UnderlyingFramework;

            /*
             * First, event handling from UI.
             */
            _backend.OnLoad += _windowOnLoad;
            _backend.OnResize += _windowOnResize;
            _backend.OnRender += _windowOnRender;
            _backend.OnUpdate += _windowOnUpdate;
            _backend.OnClosing += _windowOnClose;
            _backend.OnFocusChanged += _windowOnFocusChanged;

            /*
             * Keyboard for backends without a Silk input context (SDL3). The backend has
             * already turned its own key representation into the engine's code string; what
             * happens to it from here is identical to the Silk path, including the
             * InputMapper logical translation, because both go through _pushTranslate.
             */
            _backend.OnKeyPressed += (code, scanCode) =>
            {
                if (code == "(F11)")
                {
                    _toggleFullscreen();
                }
                else
                {
                    _pushTranslate(new engine.news.Event(Event.INPUT_KEY_PRESSED, code)
                        { ScanCode = scanCode });
                }
            };
            _backend.OnKeyReleased += (code, scanCode) =>
                _pushTranslate(new engine.news.Event(Event.INPUT_KEY_RELEASED, code)
                    { ScanCode = scanCode });
            _backend.OnKeyCharacter += text =>
            {
                if (null != _imGuiController)
                {
                    foreach (char c in text) _imGuiController.PressChar(c);
                }

                I.Get<EventQueue>().Push(new Event(Event.INPUT_KEY_CHARACTER, text));
            };

            /*
             * Mouse and gamepad, same arrangement (WP-3.1). These land on exactly the code
             * the Silk handlers land on, so the produced events - and the InputMapper
             * translation that _pushTranslate performs for sticks and buttons - are the same
             * on both backends by construction rather than by inspection.
             */
            _backend.OnMouseMoved += _pushMouseMoved;
            _backend.OnMouseWheel += (pos, delta) => { _imGuiController?.FeedMouseWheel(delta); _pushMouseWheel(pos, delta); };
            _backend.OnMousePressed += _pushMousePressed;
            _backend.OnMouseReleased += _pushMouseReleased;

            _backend.OnGamepadStickMoved += _pushGamepadStickMoved;
            _backend.OnGamepadTriggerMoved += _pushGamepadTriggerMoved;

            /*
             * Index 0: the engine supports one gamepad, and the Silk path only ever reported
             * button.Index for a device that was already the active one. Nothing downstream
             * reads Data2.
             */
            _backend.OnGamepadButtonPressed += (name, ordinal) =>
                _pushGamepadButtonPressed(name, ordinal, 0);
            _backend.OnGamepadButtonReleased += (name, ordinal) =>
                _pushGamepadButtonReleased(name, ordinal, 0);

            _backend.Subscribe();
        }

        // TXWTODO: Test DEBUG and PLATFORM_ANDROID for format options.
        // disable and bind cursor.

        I.Register<TextureGenerator>(() => new TextureGenerator());

        /*
         * Internal video implementation.
         */
        I.Register<IThreeD>(() => new GlThreeD());
        _silkThreeD = I.Get<IThreeD>() as GlThreeD;
        _silkThreeD.SetupDone();

        if (_backend != null)
        {
            _engine.RunMainThread(() =>
            {
                /*
                 * Internal helpers managing various entities.
                 */
                _instanceManager = I.Get<InstanceManager>();
                _instanceManager.Manage(_engine.GetEcsWorld());
                _cameraManager = I.Get<CameraManager>();
                _cameraManager.Manage(_engine.GetEcsWorld());

                /*
                 * Create the main screen renderer.
                 */
                _logicalRenderer = I.Get<LogicalRenderer>();
            });
        }
        else
        {
            /*
             * Headless mode: no logical thread will process the queue,
             * so initialize managers synchronously.
             */
            _instanceManager = I.Get<InstanceManager>();
            _instanceManager.Manage(_engine.GetEcsWorldDangerous());
            _cameraManager = I.Get<CameraManager>();
            _cameraManager.Manage(_engine.GetEcsWorldDangerous());
            _logicalRenderer = I.Get<LogicalRenderer>();
        }

        _renderer = new GlRenderer();
    }


    public bool IsRunning()
    {
        lock (_lo)
        {
            return _isRunning;
        }
    }


    /**
     * Hand an externally-created GL context to the renderer.
     *
     * Internal on purpose (WP-0.2): this is the only entry point that puts a Silk.NET type in
     * Platform's API surface, and its sole caller is PreviewHelper in this same project.
     * Embedding hosts (Aihao) go through PreviewHelper.Initialize, which takes a
     * Func<string, nint> and never lets a GL object cross the assembly boundary.
     */
    internal void SetExternalGL(GL gl)
    {
        _silkThreeD.SetGL(gl);
        gl.ClearDepth(1f);
        gl.ClearColor(0.15f, 0.15f, 0.18f, 1f);
    }


    public InstanceManager InstanceManager => _instanceManager;


    public void RenderExternalFrame(in RenderFrame renderFrame, int viewportWidth, int viewportHeight,
        uint targetFbo, bool saveRestoreState = true)
    {
        _renderer.SetDimension(viewportWidth, viewportHeight);
        if (saveRestoreState)
        {
            var gl = (_silkThreeD as GlThreeD).GetGL();
            using (new GlStateSaver(gl))
            {
                _renderer.RenderFrameToFbo(renderFrame, targetFbo);
            }
        }
        else
        {
            _renderer.RenderFrameToFbo(renderFrame, targetFbo);
        }
        _silkThreeD.ExecuteGraphicsThreadActions(0.001f);
    }


    /// <summary>
    /// The WP-3.3 entry point: hand the platform a window backend directly. Android uses
    /// this with an SDL3 backend; there is no Silk view involved anywhere in that path.
    /// </summary>
    public void SetWindowBackend(IWindowBackend backend)
    {
        _backend = backend;
    }


    public void Dispose()
    {
    }


    public Platform(string[] args)
    {
    }


    static public engine.Engine EasyCreatePlatform(string[] args, out Splash.OpenGL.Platform out_platform)
    {
        var platform = new Platform(args);
        out_platform = platform;
        I.Register<engine.Engine>(() => new engine.Engine(platform));
        engine.Engine e = I.Get<engine.Engine>();
        e.SetupDone();

        platform.SetEngine(e);
        platform.SetupDone();
        e.PlatformSetupDone();

        return e;
    }


    /// <summary>
    /// WP-3.3: create the engine over any window backend. This is the overload that lets a
    /// launcher exist without referencing Silk windowing at all.
    /// </summary>
    static public engine.Engine EasyCreate(string[] args, IWindowBackend backend, out Splash.OpenGL.Platform out_platform)
    {
        var platform = new Platform(args);
        out_platform = platform;
        I.Register<engine.Engine>(() => new engine.Engine(platform));
        engine.Engine e = I.Get<engine.Engine>();
        e.SetupDone();

        platform.SetWindowBackend(backend);
        platform.SetEngine(e);
        platform.SetupDone();
        e.PlatformSetupDone();

        return e;
    }


    static public engine.Engine EasyCreateHeadless(string[] args, out Platform out_platform)
    {
        var platform = new Platform(args);
        out_platform = platform;
        I.Register<engine.Engine>(() => new engine.Engine(platform));
        engine.Engine e = I.Get<engine.Engine>();
        e.SetupDone();
        platform.SetEngine(e);
        platform.SetupDone();
        e.PlatformSetupDone();
        return e;
    }
}
