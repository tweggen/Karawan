using System;
using System.Diagnostics;
using System.Numerics;
using builtin.controllers;
using engine;
using engine.news;
using ObjLoader.Loader.Common;
using Silk.NET.Core.Loader;
using static engine.Logger;

using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.OpenGL.Extensions.ImGui;

namespace Splash.Silk;


public class Platform : engine.IPlatform
{
    private object _lo = new object();
    private engine.Engine _engine;

    /**
     * Keeps all Splash implementations.
     */
    private Splash.Common _common;

    private SilkThreeD _silkThreeD;
    private InstanceManager _instanceManager;
    private CameraManager _cameraManager;
    private SilkRenderer _renderer;
    private InputMapper _inputMapper;

    private RenderStats _renderStats = new();
    private bool _isRunning = true;

    private LogicalRenderer _logicalRenderer;
    private readonly Stopwatch _frameTimingStopwatch = new();
    private readonly Stopwatch _renderSingleFrameStopwatch = new();
    private TimeSpan _prevFrame;

    private Splash.Silk.ImGui.Controller _imGuiController = null;

    /*
     * WP-3.3: this used to be a Silk.NET.Windowing.IView. It is now the backend seam, so
     * that Android can supply an SDL3 window instead. See IWindowBackend for why.
     */
    private IWindowBackend _backend;
    private IInputContext _iInputContext;
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


    // The body of this moved to SilkWindowBackend.SetFullscreen (WP-3.3): it manipulated a
    // Silk IWindow directly, which is exactly the coupling the backend seam removes.


    private string _convertKeyCodeFromPlatform(Key args)
    {
        string code = null;
        switch (args)
        {
            case Key.ShiftLeft:
                code = "(shiftleft)";
                break;
            case Key.ShiftRight:
                code = "(shiftright)";
                break;
            case Key.Space:
                code = " ";
                break;
            case Key.Number0:
                code = "0";
                break;
            case Key.Number1:
                code = "1";
                break;
            case Key.Number2:
                code = "2";
                break;
            case Key.Number3:
                code = "3";
                break;
            case Key.Number4:
                code = "4";
                break;
            case Key.Number5:
                code = "5";
                break;
            case Key.Number6:
                code = "6";
                break;
            case Key.Number7:
                code = "7";
                break;
            case Key.Number8:
                code = "8";
                break;
            case Key.Number9:
                code = "9";
                break;
            case Key.A:
                code = "a";
                break;
            case Key.D:
                code = "d";
                break;
            case Key.E:
                code = "e";
                break;
            case Key.F:
                code = "f";
                break;
            case Key.S:
                code = "s";
                break;
            case Key.Q:
                code = "q";
                break;
            case Key.W:
                code = "w";
                break;
            case Key.Z:
                code = "z";
                break;
            case Key.Enter:
                code = "(enter)";
                break;
            case Key.Tab:
                code = "(tab)";
                break;
            case Key.Escape:
                code = "(escape)";
                break;
            case Key.F8:
                code = "(F8)";
                break;
            case Key.F9:
                code = "(F9)";
                break;
            case Key.F10:
                code = "(F10)";
                break;
            case Key.F11:
                code = "(F11)";
                break;
            case Key.F12:
                code = "(F12)";
                break;
            case Key.Up:
                code = "(cursorup)";
                break;
            case Key.Down:
                code = "(cursordown)";
                break;
            case Key.Right:
                code = "(cursorright)";
                break;
            case Key.Left:
                code = "(cursorleft)";
                break;
            case Key.Delete:
                code = "(delete)";
                break;
            case Key.Backspace:
                code = "(backspace)";
                break;
            default:
                break;
        }

        return code;
    }

    
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
    

    private void _onKeyDown(IKeyboard arg1, Key arg2, int arg3)
    {
        string code = _convertKeyCodeFromPlatform(arg2);
        if (!code.IsNullOrEmpty())
        {
            switch (code)
            {
                case "(F11)":
                    _toggleFullscreen();
                    break;
                default:
                    _pushTranslate(new engine.news.Event(Event.INPUT_KEY_PRESSED, code));
                    break;
            }
        }
    }


    private void _onKeyChar(IKeyboard arg1, char arg3)
    {
        string keyCode = null;
        switch (arg3)
        {
            case '\b':
                keyCode = "(backspace)";
                break;
            case '\t':
                keyCode = "(tab)";
                break;
            case '\n':
            case '\r':
                keyCode = "(enter)";
                break;
            case '\x7f':
                keyCode = "(delete)";
                break;
            default:
                if (!char.IsControl(arg3))
                {
                    I.Get<EventQueue>().Push(new Event(Event.INPUT_KEY_CHARACTER, arg3.ToString()));
                }
                return;
        }

        // Emit press+release pair for translated control characters.
        _pushTranslate(new Event(Event.INPUT_KEY_PRESSED, keyCode));
        _pushTranslate(new Event(Event.INPUT_KEY_RELEASED, keyCode));
    }


    private void _onKeyUp(IKeyboard arg1, Key arg2, int arg3)
    {
        string code = _convertKeyCodeFromPlatform(arg2);
        if (!code.IsNullOrEmpty())
        {
            _pushTranslate(new engine.news.Event(Event.INPUT_KEY_RELEASED, code));
        }
    }


    /**
     * Caution:
     * - on Windows, with raw mouse, position is just an accumulated sum of the delta moves
     * - on Windows, using Teamviewer, the coordinates are insanely high.
     * - on Android, I didn't quite understand the math yet.
     */
    private void _onMouseMove(IMouse mouse, Vector2 position)
    {
        if (_shallReturnBecauseUI(mouse.Position)) return;

        I.Get<EventQueue>().Push(new Event(Event.INPUT_MOUSE_MOVED, "")
        {
            PhysicalPosition = position
        });
    }


    private void _onMouseWheel(IMouse mouse, ScrollWheel scrollWheel)
    {
        if (_shallReturnBecauseUI(mouse.Position)) return;

        I.Get<EventQueue>().Push(new Event(Event.INPUT_MOUSE_WHEEL, "")
        {
            PhysicalPosition = new(scrollWheel.X, scrollWheel.Y)
        });
    }

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
    

    private void _onMouseDown(IMouse mouse, MouseButton mouseButton)
    {
        if (_shallReturnBecauseUI(mouse.Position)) return;
        
        _fullToViewPosition(mouse.Position, out var pos, out var size, out var v2LogicalPosition);
        
        // Trace($"Position is {mouse.Position}");

        I.Get<EventQueue>().Push(
            new Event(Event.INPUT_MOUSE_PRESSED, $"{(int)mouseButton}")
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


    private void _onMouseUp(IMouse mouse, MouseButton mouseButton)
    {
        if (_shallReturnBecauseUI(mouse.Position)) return;

        _fullToViewPosition(mouse.Position, out var pos, out var size, out var v2LogicalPosition);

        I.Get<EventQueue>().Push(
            new Event(Event.INPUT_MOUSE_RELEASED, $"{(int)mouseButton}")
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


    private void _onGamepadThumbstickMoved(IGamepad gamepad, Thumbstick thumbstick)
    {
        _pushTranslate(new Event(Event.INPUT_GAMEPAD_STICK_MOVED, "")
            {
                PhysicalPosition = new(thumbstick.X, thumbstick.Y),
                Data1 = (uint) thumbstick.Index
            });
    }


    private void _onGamepadTriggerMoved(IGamepad gamepad, Trigger trigger)
    {
        // Trace($"trigger {trigger.Index}");
        I.Get<EventQueue>().Push(
            new Event(Event.INPUT_GAMEPAD_TRIGGER_MOVED, "")
            {
                PhysicalPosition = new(trigger.Position, 0f),
                Data1 = (uint) trigger.Index
            });
    }


    private void _onGamepadButtonDown(IGamepad gamepad, Button button)
    {
        // Trace($"button {button.Name}");
        _pushTranslate(
            new Event(Event.INPUT_GAMEPAD_BUTTON_PRESSED, $"{button.Name}")
            {
                Data1 = (uint) button.Name,
                Data2 = (uint) button.Index
            });
    }


    private void _onGamepadButtonUp(IGamepad gamepad, Button button)
    {
        _pushTranslate(
            new Event(Event.INPUT_GAMEPAD_BUTTON_RELEASED, $"{button.Name}")
            {
                Data1 = (uint) button.Name,
                Data2 = (uint) button.Index
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
        var maxMice = _iInputContext.Mice.Count;
        for (int i = 0; i < maxMice; i++)
        {
            _iInputContext.Mice[i].Cursor.CursorMode =
                value ? CursorMode.Normal : CursorMode.Raw;
        }
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
        var maxKeyboards = _iInputContext.Keyboards.Count;
        for (int i = 0; i < maxKeyboards; i++)
        {
            if (value)
            {
                _iInputContext.Keyboards[i].BeginInput();
            }
            else
            {
                _iInputContext.Keyboards[i].EndInput();
            }
        }
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
        
        /*
         * Instead of just instantiating a SdlInput as intended, we create an
         * input class of our own to intercept the touch events.
         */
        _iInputContext = _backend.SilkInputContext;

        /*
         * WP-3.3: a backend may have no Silk input context at all. The SDL3 backend
         * translates SDL events into engine.news.EventQueue itself - the queue is the
         * contract, not this wiring - so everything below is skipped there. Android never
         * relied on it for touch anyway: GameSurface.OnTouch has always pushed straight
         * into the queue.
         */
        if (null != _iInputContext)
        {
        for (int i = 0; i < _iInputContext.Keyboards.Count; i++)
        {
            _iInputContext.Keyboards[i].KeyDown += _onKeyDown;
            _iInputContext.Keyboards[i].KeyUp += _onKeyUp;
            _iInputContext.Keyboards[i].KeyChar += _onKeyChar;
        }

        for (int i = 0; i < _iInputContext.Gamepads.Count; i++)
        {
            _iInputContext.Gamepads[i].ButtonDown += _onGamepadButtonDown;
            _iInputContext.Gamepads[i].ButtonUp += _onGamepadButtonUp;
            _iInputContext.Gamepads[i].ThumbstickMoved += _onGamepadThumbstickMoved;
            _iInputContext.Gamepads[i].TriggerMoved += _onGamepadTriggerMoved;
        }
        
        int maxMice;
        bool useRawMouse;
        if (GlobalSettings.Get("Android") == "true")
        {
            maxMice = 1;
            useRawMouse = false;
        }
        else
        {
            maxMice = _iInputContext.Mice.Count;
            useRawMouse = true;
        }

        for (int i = 0; i < maxMice; i++)
        {
            if (useRawMouse)
            {
                _iInputContext.Mice[i].Cursor.CursorMode = CursorMode.Raw;
            }

            _iInputContext.Mice[i].MouseDown += _onMouseDown;
            _iInputContext.Mice[i].MouseUp += _onMouseUp;
            _iInputContext.Mice[i].MouseMove += _onMouseMove;

            _iInputContext.Mice[i].Scroll += _onMouseWheel;
        }
        } // end: backend supplies a Silk input context

        // TXWTODO: Create sort of "on new gl window" event.
        _gl = GL.GetApi(_backend.GetProcAddress);
        _silkThreeD.SetGL(_gl);
        _gl.ClearDepth(1f);
        _gl.ClearColor(0f, 0f, 0f, 0f);

        /*
         * ImGui stays Silk-only. Silk.NET.OpenGL.Extensions.ImGui takes an IView and an
         * IInputContext in its public constructor, so it cannot be built over the seam -
         * that entanglement is Phase 5's problem (WP-5.3). It is not a loss on Android:
         * models/game.launch.android.json sets "createUI": "false" and there is no Android
         * build of cimgui at all (WP-0.3 4.2).
         */
        if (engine.GlobalSettings.Get("nogame.CreateUI") != "false"
            && _backend is SilkWindowBackend silkBackend
            && null != _iInputContext)
        {
            _imGuiController = new (_gl, silkBackend.View, _iInputContext);
        }

        _hadFocus = true;

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
        _gl?.Dispose();
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

        // TXWTODO: We are abusing the global settings as global variables.
        _renderer.SetDimension(w, h);
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
         * inline here is supplied as callbacks instead; SilkWindowBackend.Run reproduces
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
            _backend.OnKeyPressed += code =>
            {
                if (code == "(F11)")
                {
                    _toggleFullscreen();
                }
                else
                {
                    _pushTranslate(new engine.news.Event(Event.INPUT_KEY_PRESSED, code));
                }
            };
            _backend.OnKeyReleased += code =>
                _pushTranslate(new engine.news.Event(Event.INPUT_KEY_RELEASED, code));
            _backend.OnKeyCharacter += text =>
                I.Get<EventQueue>().Push(new Event(Event.INPUT_KEY_CHARACTER, text));

            _backend.Subscribe();
        }

        // TXWTODO: Test DEBUG and PLATFORM_ANDROID for format options.
        // disable and bind cursor.

        I.Register<TextureGenerator>(() => new TextureGenerator());

        /*
         * Internal video implementation.
         */
        I.Register<IThreeD>(() => new SilkThreeD());
        _silkThreeD = I.Get<IThreeD>() as SilkThreeD;
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

        _renderer = new SilkRenderer();
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
            var gl = (_silkThreeD as SilkThreeD).GetGL();
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
    /// Kept for source compatibility with desktop launchers, which still create a Silk
    /// view. It wraps it in the backend seam (WP-3.3).
    /// </summary>
    public void SetIView(IView iView)
    {
        _backend = new SilkWindowBackend(iView);
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


    static public engine.Engine EasyCreatePlatform(string[] args, out Splash.Silk.Platform out_platform)
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
    /// Desktop entry point. Unchanged signature so Karawan and examples/Launcher keep
    /// compiling; internally it now wraps the view in a SilkWindowBackend.
    /// </summary>
    static public engine.Engine EasyCreate(string[] args, IView iView, out Splash.Silk.Platform out_platform)
        => EasyCreate(args, new SilkWindowBackend(iView), out out_platform);


    /// <summary>
    /// WP-3.3: create the engine over any window backend. This is the overload that lets a
    /// launcher exist without referencing Silk windowing at all.
    /// </summary>
    static public engine.Engine EasyCreate(string[] args, IWindowBackend backend, out Splash.Silk.Platform out_platform)
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
