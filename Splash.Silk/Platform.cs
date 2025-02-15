using System.Diagnostics;
using System.Numerics;
using builtin.controllers;
using engine;
using engine.news;
using ObjLoader.Loader.Common;
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


    private IView _iView;
    private IInputContext _iInputContext;
    private GL _gl;
    
    private engine.WorkerQueue _platformThreadActions = new("platformThread");

    private ImGuiController _imGuiController = null;
    
    private bool _mouseEnabled = false;

    public bool MouseEnabled
    {
        get => _mouseEnabled;
        set => _platformThreadActions.Enqueue(() => _setMouseEnabled(value));
    }


    private bool _keyboardEnabled = false;

    public bool KeyboardEnabled
    {
        get => _keyboardEnabled;
        set => _platformThreadActions.Enqueue(() => _setKeyboardEnabled(value));
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
        if (null == _iView)
        {
            return;
        }

        IWindow iWindow = _iView as IWindow;
        if (null == iWindow)
        {
            return;
        }

        try
        {
            if (isFullscreen)
            {
                iWindow.Size = new Vector2D<int>(1280, 720);
                iWindow.WindowState = WindowState.Fullscreen;
            }
            else
            {
                //iWindow.Size = new Vector2D<int>(1280, 720);
                iWindow.WindowState = WindowState.Normal;
            }
        }
        catch (Exception e)
        {
            Error($"Exception while setting fullscreen to {isFullscreen}");
            // TXWTODO: FIx exception when de-fullscreening.
            // Setting fullscreeen might fail.
        }
    }


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

    
    private void _pushTranslate(in Event ev)
    {
        // TXWTODO: Have a locally resolved input manager variable.
        _inputMapper.EmitPlusTranslation(ev);
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
        I.Get<EventQueue>().Push(new Event(Event.INPUT_KEY_CHARACTER, arg3.ToString()));
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
        I.Get<EventQueue>().Push(new Event(Event.INPUT_MOUSE_MOVED, "")
        {
            Position = position
        });
    }


    private void _onMouseWheel(IMouse mouse, ScrollWheel scrollWheel)
    {
        I.Get<EventQueue>().Push(new Event(Event.INPUT_MOUSE_WHEEL, "")
        {
            Position = new(scrollWheel.X, scrollWheel.Y)
        });
    }

    private void _getActualViewRectangle(out Vector2 ul, out Vector2 lr)
    {
        _engine.GetViewRectangle(out ul, out lr);
        if (Vector2.Zero == lr)
        {
            lr = new Vector2(_iView.Size.X, _iView.Size.Y) - Vector2.One;
        }
    }

    private void _fullToViewPosition(in Vector2 i, out Vector2 o, out Vector2 s)
    {
        _getActualViewRectangle(out var ul, out var lr);
        o = i - ul;
        s = lr - ul + Vector2.One;
    }


    private void _onMouseDown(IMouse mouse, MouseButton mouseButton)
    {
        _fullToViewPosition(mouse.Position, out var pos, out var size);

        // Trace($"Position is {mouse.Position}");

        I.Get<EventQueue>().Push(
            new Event(Event.INPUT_MOUSE_PRESSED, $"{(int)mouseButton}")
            {
                Position = pos,
                Size = size,
                Data1 = (uint) mouseButton
            });
        I.Get<EventQueue>().Push(
            new Event(Event.INPUT_TOUCH_PRESSED, "")
            {
                Position = pos,
                Size = size
            });
    }


    private void _onMouseUp(IMouse mouse, MouseButton mouseButton)
    {
        _fullToViewPosition(mouse.Position, out var pos, out var size);

        I.Get<EventQueue>().Push(
            new Event(Event.INPUT_MOUSE_RELEASED, $"{(int)mouseButton}")
            {
                Position = pos,
                Size = size,
                Data1 = (uint) mouseButton
            });
        I.Get<EventQueue>().Push(
            new Event(Event.INPUT_TOUCH_RELEASED, "")
            {
                Position = pos,
                Size = size
            });
    }


    private void _onGamepadThumbstickMoved(IGamepad gamepad, Thumbstick thumbstick)
    {
        _pushTranslate(new Event(Event.INPUT_GAMEPAD_STICK_MOVED, "")
            {
                Position = new(thumbstick.X, thumbstick.Y),
                Data1 = (uint) thumbstick.Index
            });
    }


    private void _onGamepadTriggerMoved(IGamepad gamepad, Trigger trigger)
    {
        // Trace($"trigger {trigger.Index}");
        I.Get<EventQueue>().Push(
            new Event(Event.INPUT_GAMEPAD_TRIGGER_MOVED, "")
            {
                Position = new(trigger.Position, 0f),
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

#if DEBUG
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform
                .Linux))
        {
            value = true;
        }
#endif

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
        _iInputContext = _iView.CreateInput();
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

        // TXWTODO: Create sort of "on new gl window" event.
        _gl = GL.GetApi(_iView);
        _silkThreeD.SetGL(_gl);
        _gl.ClearDepth(1f);
        _gl.ClearColor(0f, 0f, 0f, 0f);

        if (engine.GlobalSettings.Get("nogame.CreateUI") != "false")
        {
            _imGuiController = new ImGuiController(_gl, _iView, _iInputContext);
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
        
        RenderFrame renderFrame;
        while (true)
        {
            _triggerWaitMonitor();

            if (null != _logicalRenderer)
            {
                renderFrame = _logicalRenderer.WaitNextRenderFrame();
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

        if (_iView.Size.X != 0 && _iView.Size.Y != 0)
        {
            _renderer.SetDimension(_iView.Size.X, _iView.Size.Y);
        }

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

        _iView.SwapBuffers();
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
        // Trace($"after {(tsNow-_prevFrame).TotalMilliseconds} Took {_renderSingleFrameStopwatch.Elapsed.TotalMilliseconds}, got {msGotFrame} dr {msRendered-msGotFrame} aftergfx {msAfterGraphicsThread-msRendered} afterpf {msAfterPlatformThread-msAfterGraphicsThread} ");
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


    private void _windowOnResize(Vector2D<int> size)
    {
        if (size.X != 0 && size.Y != 0)
        {
            // TXWTODO: We are abusing the global settings as global variables.
            _renderer.SetDimension(size.X, size.Y);
            engine.GlobalSettings.Set("view.size", $"{size.X}x{size.Y}");
            I.Get<EventQueue>().Push(new Event(Event.VIEW_SIZE_CHANGED, "")
            {
                Position = new(size.X, size.Y)
            });
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
        _inputMapper = I.Get<builtin.controllers.InputMapper>();


        /*
         * Instead of just calling _iView.Run(),
         * we run the same thing explicitely. That way we can inject calls.
         */
        IView iView = _iView;
        iView.Run(delegate
        {
            _triggerWaitMonitor();

            if (BeforeDoEvent != null)
            {
                BeforeDoEvent();
            }

            try
            {
                iView.DoEvents();
            }
            catch (Exception e)
            {
                /*
                 * Catching exception that might come from unknown keys in ImLib
                 */
            }

            if (!iView.IsClosing)
            {
                _triggerWaitMonitor();
                iView.DoUpdate();
            }

            if (!iView.IsClosing)
            {
                iView.DoRender();
            }
        });
        iView.DoEvents();
        
        _triggerWaitMonitor();
        
        iView.Reset();
        iView = null;
        _iView.Dispose();
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

        if (_iView.GetType().FullName.Contains("Glfw"))
        {
            _underlyingFrameworks = UnderlyingFrameworks.Glfw;
        } else if (_iView.GetType().FullName.Contains("Sdl"))
        {
            _underlyingFrameworks = UnderlyingFrameworks.Sdl;
        }
        
        /*
         * First, event handling from UI.
         */
        _iView.Load += _windowOnLoad;
        _iView.Resize += _windowOnResize;
        _iView.Render += _windowOnRender;
        _iView.Update += _windowOnUpdate;
        _iView.Closing += _windowOnClose;
        _iView.FocusChanged += _windowOnFocusChanged;
        // TXWTODO: Add this handler to handle window resizes.
        // _iView.FramebufferResize +=

        // TXWTODO: Test DEBUG and PLATFORM_ANDROID for format options.
        // disable and bind cursor.

        I.Register<TextureGenerator>(() => new TextureGenerator());

        /*
         * Internal video implementation.
         */
        I.Register<IThreeD>(() => new SilkThreeD());
        _silkThreeD = I.Get<IThreeD>() as SilkThreeD;
        _silkThreeD.SetupDone();

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

        _renderer = new SilkRenderer();
    }


    public bool IsRunning()
    {
        lock (_lo)
        {
            return _isRunning;
        }
    }


    public void SetIView(IView iView)
    {
        _iView = iView;
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


    static public engine.Engine EasyCreate(string[] args, IView iView, out Splash.Silk.Platform out_platform)
    {
        var platform = new Platform(args);
        out_platform = platform;
        I.Register<engine.Engine>(() => new engine.Engine(platform));
        engine.Engine e = I.Get<engine.Engine>();
        e.SetupDone();

        platform.SetIView(iView);
        platform.SetEngine(e);
        platform.SetupDone();
        e.PlatformSetupDone();

        return e;
    }
}
