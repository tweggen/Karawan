using System.Numerics;
using engine;
using engine.news;
using static engine.Logger;

using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.OpenGL.Extensions.ImGui;

namespace Splash.Silk
{

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
        private RenderStats _renderStats = new();
        private bool _isRunning = true;

        private LogicalRenderer _logicalRenderer;

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
        
        
        private void _onKeyDown(IKeyboard arg1, Key arg2, int arg3)
        {
            string code = "";
            switch (arg2)
            {
                case Key.ShiftLeft:
                    code = "(shiftleft)";
                    break;
                case Key.Q:
                    code = "Q";
                    break;
                case Key.Z:
                    code = "Z";
                    break;
                case Key.W:
                    code = "W";
                    break;
                case Key.S:
                    code = "S";
                    break;
                case Key.A:
                    code = "A";
                    break;
                case Key.D:
                    code = "D";
                    break;
                case Key.Tab:
                    code = "(tab)";
                    break;
                case Key.Escape:
                    code = "(escape)";
                    break;
                case Key.F11:
                    code = "(F11)";
                    _toggleFullscreen();
                    break;
                default:
                    break;
            }

            if (code.Length != 0)
            {
                I.Get<EventQueue>().Push(new engine.news.Event(Event.INPUT_KEY_PRESSED, code));
            }
        }
        

        private void _onKeyUp(IKeyboard arg1, Key arg2, int arg3)
        {
            string code = "";
            switch (arg2)
            {
                case Key.ShiftLeft:
                    code = "(shiftleft)";
                    break;
                case Key.Q:
                    code = "Q";
                    break;
                case Key.Z:
                    code = "Z";
                    break;
                case Key.W:
                    code = "W";
                    break;
                case Key.S:
                    code = "S";
                    break;
                case Key.A:
                    code = "A";
                    break;
                case Key.D:
                    code = "D";
                    break;
                case Key.Tab:
                    code = "(tab)";
                    break;  
                case Key.Escape:
                    code = "(escape)";
                    break;
                case Key.F11:
                    code = "(F11)";
                    break;
                default:
                    break;
            }
            

            if (code.Length != 0)
            {
                I.Get<EventQueue>().Push(new engine.news.Event(Event.INPUT_KEY_RELEASED, code));
            }
        }

        
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

            I.Get<EventQueue>().Push(
                new Event(Event.INPUT_MOUSE_PRESSED, $"{(int)mouseButton}")
                {
                    Position = pos,
                    Size = size
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
                    Size = size
                });
            I.Get<EventQueue>().Push(
                new Event(Event.INPUT_TOUCH_RELEASED, "")
                {
                    Position = pos,
                    Size = size
                });
        }


        private bool _hadFocus = true;


        private void _setMouseEnabled(bool value)
        {
            if (GlobalSettings.Get("Android") == "true") return;
            
            _mouseEnabled = value;
            var maxMice = _iInputContext.Mice.Count;
            for (int i = 0; i < maxMice; i++)
            {
                _iInputContext.Mice[i].Cursor.CursorMode =
                    value ? CursorMode.Normal : CursorMode.Raw;
            }
        }
        

        private void _windowOnLoad()
        {
            /*
             * Instead of just instantiating a SdlInput as intended, we create an
             * input class of our own to intercept the touch events.
             */
            _iInputContext = _iView.CreateInput();
            for (int i = 0; i < _iInputContext.Keyboards.Count; i++)
            {
                _iInputContext.Keyboards[i].KeyDown += _onKeyDown;
                _iInputContext.Keyboards[i].KeyUp += _onKeyUp;
            }

            int maxMice;
            bool useRawMouse;
            if(GlobalSettings.Get("Android") == "true")
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
            
            _engine.CallOnPlatformAvailable();

            // _createTestTexture();
        }


        private void _windowOnUpdate(double dt)
        {
            
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
            while (true)
            {
                // _physFrameReadKeyEvents();

                Engine.EngineState engineState = _engine.State;
                RenderFrame renderFrame = _logicalRenderer.DequeueRenderFrame();
                if (renderFrame == null)
                {
                    _silkThreeD.ExecuteGraphicsThreadActions(0.005f);
                    if (engineState <= Engine.EngineState.Running)
                    {
                        System.Threading.Thread.Sleep(1);
                    } else
                    {
                        System.Threading.Thread.Sleep(10);
                    }
                    break;
                }

                if (_iView.Size.X != 0 && _iView.Size.Y != 0)
                {
                    _renderer.SetDimension(_iView.Size.X, _iView.Size.Y);
                }

                _renderer.RenderFrame(renderFrame);
                _renderStats.PushFrame(renderFrame.FrameStats);
                I.Get<EventQueue>().Push(new Event(Event.RENDER_STATS, _renderStats.GetAverage().ToString()));

                if (null != _imGuiController)
                {
                    _imGuiController.Update((float)dt);
                    _engine.CallOnImGuiRender((float)dt);
                    _imGuiController.Render();
                }
                
                _iView.SwapBuffers();
                _silkThreeD.ExecuteGraphicsThreadActions(0.001f);
                ++_frameNo;
                _engine.CallOnPhysicalFrame((float)dt);
                _platformThreadActions.RunPart(1000f);

                break;

            }
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
        }
        

        private void _windowOnResize( Vector2D<int> size)
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

        public void Execute()
        {
            /*
             * Instead of just calling _iView.Run(),
             * we run the same thing explicitely. That way we can inject calls.
             */
            IView iView = _iView;
            iView.Initialize();
            iView.Run(delegate
            {
                iView.DoEvents();
                if (!iView.IsClosing)
                {
                    iView.DoUpdate();
                }

                if (!iView.IsClosing)
                {
                    iView.DoRender();
                }
            });
            iView.DoEvents();
            iView.Reset();
            iView = null;
            _iView.Dispose();
        }


        public void CollectRenderData(engine.IScene scene)
        {
            _logicalRenderer.CollectRenderData(scene);
        }


#if false
        /**
         * For testing purposes, create a rendering target texture.
         * We later render a thing with it.
         */
        private engine.joyce.Renderbuffer _jRenderbuffer;
        private ARenderbuffer _aRenderbuffer;
        private void _createTestTexture()
        {
            _jRenderbuffer = new engine.joyce.Renderbuffer("mapbuffer", 1024, 1024);
            _aRenderbuffer = _silkThreeD.CreateRenderbuffer(_jRenderbuffer);
            SkRenderbuffer skRenderbuffer = _aRenderbuffer as SkRenderbuffer;
            skRenderbuffer.Upload(_gl,  _silkThreeD.TextureManager );
        }
#endif

        /**
         * Call this after all dependencies are created.
         */
        public void SetupDone()
        {
            _common = new();
            engine.GlobalSettings.Set("view.size", "320x200");
            
            string baseDirectory = System.AppContext.BaseDirectory;
            System.Console.WriteLine($"Running in directory {baseDirectory}" );
            
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

            _renderer = new SilkRenderer();
        }

        
        public bool IsRunning()
        {
            lock(_lo)
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


        static public engine.Engine EasyCreate(string[] args, IView iView)
        {
            var platform = new Platform(args);
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
}
