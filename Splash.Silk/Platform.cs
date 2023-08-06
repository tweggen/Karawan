using System;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using builtin.tools.Lindenmayer;
using engine;
using engine.news;
using Microsoft.Win32.SafeHandles;
using static engine.Logger;

using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Input.Sdl;
using Silk.NET.SDL;
using Trace = System.Diagnostics.Trace;

namespace Splash.Silk
{

    public class Platform : engine.IPlatform
    {
        private object _lo = new object();
        private engine.Engine _engine;
        private engine.ControllerState _controllerState;
        private Vector2 _vMouseMove;
        private static Vector2 _lastMousePosition;

        private SilkThreeD _silkThreeD;
        private InstanceManager _instanceManager;
        private LightManager _lightManager;
        private SilkRenderer _renderer;
        private bool _isRunning = true;

        private LogicalRenderer _logicalRenderer;

        private IView _iView;
        private IInputContext _iInputContext;
        private GL _gl;

        private float _lookSensitivity = 1f;


        public void SetEngine(engine.Engine engine)
        {
            lock (_lo)
            {
                _engine = engine;
            }
        }
        

        private void _physFrameReadKeyEvents()
        {
#if false
            /*
             * Read input devices.
             * #2 Key events
             */
            while (true)
            {
                var keyCode = Raylib_CsLo.Raylib.GetKeyPressed();
                if (0 == keyCode)
                {
                    break;
                }
                if (keyCode == 65)
                {
                    // TXWTODO: Forward 
                }
            }
#endif
        }

        
        private void _physFrameReadMouseMove()
        {
#if false
            Vector2 vnew = Raylib_CsLo.Raylib.GetMouseDelta();
            lock (_lock)
            {
                _vMouseMove += vnew;
            }
#endif
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
        
        
        private readonly float ControllerWalkForwardFast = 255f;
        private readonly float ControllerWalkBackwardFast = 255f;
        private readonly float ControllerWalkForwardNormal = 200f;
        private readonly float ControllerWalkBackwardNormal = 200f;
        private readonly float ControllerFlyUpNormal = 200f;
        private readonly float ControllerFlyDownNormal = 200f;
        private readonly float ControllerTurnLeftRight = 200f;
        
        
        
        private void _onKeyDown(IKeyboard arg1, Key arg2, int arg3)
        {
            string code = "";
            switch (arg2)
            {
                case Key.ShiftLeft:
                    _controllerState.WalkFast = true;
                    code = "(shiftleft)";
                    break;
                case Key.Q:
                    _controllerState.FlyUp = (int) ControllerFlyUpNormal;
                    code = "Q";
                    break;
                case Key.Z:
                    _controllerState.FlyDown = (int)ControllerFlyDownNormal;
                    code = "Z";
                    break;
                case Key.W:
                    _controllerState.WalkForward = _controllerState.WalkFast?(int)ControllerWalkForwardFast:(int)ControllerWalkForwardNormal;
                    code = "W";
                    break;
                case Key.S:
                    _controllerState.WalkBackward = _controllerState.WalkFast?(int)ControllerWalkBackwardFast:(int)ControllerWalkBackwardNormal;
                    code = "S";
                    break;
                case Key.A:
                    _controllerState.TurnLeft = (int)ControllerTurnLeftRight;
                    code = "A";
                    break;
                case Key.D:
                    _controllerState.TurnRight = (int)ControllerTurnLeftRight;
                    code = "D";
                    break;
                case Key.Tab:
                    _controllerState.ShowMap = true;
                    code = "(tab)";
                    break;
                case Key.Escape:
                    _controllerState.PauseMenu = true;
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
                _engine.TakeKeyEvent(new engine.news.KeyEvent("pressed", code));
            }
        }
        

        private void _onKeyUp(IKeyboard arg1, Key arg2, int arg3)
        {
            string code = "";
            switch (arg2)
            {
                case Key.ShiftLeft:
                    _controllerState.WalkFast = false;
                    code = "(shiftleft)";
                    break;
                case Key.Q:
                    _controllerState.FlyUp = 0;
                    code = "Q";
                    break;
                case Key.Z:
                    _controllerState.FlyDown = 0;
                    code = "Z";
                    break;
                case Key.W:
                    _controllerState.WalkForward = 0;
                    code = "W";
                    break;
                case Key.S:
                    _controllerState.WalkBackward = 0;
                    code = "S";
                    break;
                case Key.A:
                    _controllerState.TurnLeft = 0;
                    code = "A";
                    break;
                case Key.D:
                    _controllerState.TurnRight = 0;
                    code = "D";
                    break;
                case Key.Tab:
                    code = "(tab)";
                    _controllerState.ShowMap = false;
                    break;  
                case Key.Escape:
                    code = "(escape)";
                    _controllerState.PauseMenu = false;
                    break;
                case Key.F11:
                    code = "(F11)";
                    break;
                default:
                    break;
            }

            if (code.Length != 0)
            {
                _engine.TakeKeyEvent(new engine.news.KeyEvent("released", code));
            }
        }


        /**
         * Respond to a move, press position is relative view size (anamorphic),
         * vRel is movement (relative to viewY resolution)
         */
        private void _handleTouchMove(Vector2 vPress, Vector2 vRel)
        {
            /*
             * Pressed in the left half of the screen?
             */
            if (vPress.X <= 0.5)
            {
                if (vRel.Y < 0)
                {
                    /*
                     * The user dragged up compare to the press position
                     */
                    _controllerState.WalkForward = (int)(Single.Min(ControllerYMax, -vRel.Y)/ControllerYMax * ControllerWalkForwardFast);
                    _controllerState.WalkBackward = 0;
                }
                else if (vRel.Y > 0)
                {
                    /*
                     * The user dragged down compared to the press position.
                     */
                    _controllerState.WalkBackward = (int)(Single.Min(ControllerYMax, vRel.Y)/ControllerYMax * ControllerWalkBackwardFast);
                    _controllerState.WalkForward = 0;
                }

                if (vRel.X < 0)
                {
                    _controllerState.TurnLeft = (int)(Single.Min(ControllerXMax, -vRel.X)/ControllerXMax * ControllerTurnLeftRight);
                    _controllerState.TurnRight = 0;
                }
                else if (vRel.X > 0)
                {
                    _controllerState.TurnRight = (int)(Single.Min(ControllerXMax, vRel.X) * ControllerTurnLeftRight);
                    _controllerState.TurnLeft = 0;
                }
            }
            else
            {
                var viewSize = _iView.Size;
                if (_lastTouchPosition == default)
                {
                    _lastTouchPosition = _currentMousePosition;
                }
                _vMouseMove += ((_currentMousePosition - _lastTouchPosition) / viewSize.Y) * 900f * _lookSensitivity;
            }
        }
        

        private readonly float ControllerYMax = 0.2f; 
        private readonly float ControllerXMax = 0.13f;

        private Vector2 _lastTouchPosition = default;

        private void _touchMouseController()
        {
            lock (_lo)
            {
                if (_isMouseButtonClicked)
                {
                    Vector2 currDist = _currentMousePosition - _mousePressPosition;
                    var viewSize = _iView.Size;

                    /*
                     * Compute movement relative to view height, 
                     */
                    float relY = (float)currDist.Y / (float)viewSize.Y;
                    float relX = (float)currDist.X / (float)viewSize.Y;

                    _handleTouchMove(
                        new Vector2(
                            _mousePressPosition.X / viewSize.X, 
                            _mousePressPosition.Y / viewSize.Y),
                        new Vector2(relX, relY));

                    _lastTouchPosition = _currentMousePosition;
                }
                else
                {
                    /*
                     * on any release, reset all controller movements.
                     */
                    _controllerState.WalkForward = 0;
                    _controllerState.WalkBackward = 0;
                    _controllerState.TurnRight = 0;
                    _controllerState.TurnLeft = 0;

                    _lastTouchPosition = default;
                }
            }
        }


        private void _desktopMouseController()
        {
            lock (_lo)
            {
                if (!_isMouseButtonClicked)
                {
                    if (_lastMousePosition == default)
                    {
                    }
                    else
                    {
                        var xOffset = (_currentMousePosition.X - _lastMousePosition.X) * _lookSensitivity;
                        var yOffset = (_currentMousePosition.Y - _lastMousePosition.Y) * _lookSensitivity;
                        _vMouseMove += new Vector2(xOffset, yOffset);
                    }
                    _lastMousePosition = _currentMousePosition;
                }
            }
        }


        private bool _isMouseButtonClicked = false;
        private Vector2 _mousePressPosition = new();
        private Vector2 _currentMousePosition = new();


        private void _onMouseMove(IMouse mouse, Vector2 position)
        {
            lock (_lo)
            {
                _currentMousePosition = mouse.Position;
            }
        }


        private void _onMouseWheel(IMouse mouse, ScrollWheel scrollWheel)
        {
            /*
             *  Translate mouse wheel to zooming in/out. 
             */
            var y = scrollWheel.Y;
            lock (_lo)
            {
                int currentZoomState = _controllerState.ZoomState;
                currentZoomState -= (int) y;
                currentZoomState = Int32.Max(-128, currentZoomState);
                currentZoomState = Int32.Min(16, currentZoomState);
                _controllerState.ZoomState = (sbyte) currentZoomState;
            }
        }


        private void _onMouseDown(IMouse mouse, MouseButton mouseButton)
        {
            if (mouseButton != MouseButton.Left)
            {
                return;
            }

            lock (_lo)
            {
                _mousePressPosition = mouse.Position;
                _currentMousePosition = mouse.Position;
                _isMouseButtonClicked = true;
            }

            _engine.TakeTouchPress(mouse.Position);
        }

        private void _onMouseUp(IMouse mouse, MouseButton mouseButton)
        {
            if (mouseButton != MouseButton.Left)
            {
                return;
            }

            lock (_lo)
            {
                _currentMousePosition = mouse.Position;
                _isMouseButtonClicked = false;
            }

            _engine.TakeTouchRelease(mouse.Position);
        }


        private bool _hadFocus = true;

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

            _hadFocus = true;

            _createTestTexture();
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
                _physFrameReadKeyEvents();
                _physFrameReadMouseMove();

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

                _iView.SwapBuffers();
                _silkThreeD.ExecuteGraphicsThreadActions(0.001f);
                ++_frameNo;
                _engine.OnPhysicalFrame((float)dt);

                break;

            }
        }

        private void _windowOnClose()
        {
            _isRunning = false;
        }
        

        private void _windowOnResize( Vector2D<int> size)
        { 
            if (size.X != 0 && size.Y != 0)
            {
                return;
            }
            _renderer.SetDimension(size.X, size.Y);
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


        public void GetMouseMove(out Vector2 vMouseMove)
        {
            lock (_lo)
            {
                vMouseMove = _vMouseMove;
                _vMouseMove = new Vector2(0f, 0f);
            }
        }
        

        public void GetControllerState(out ControllerState controllerState)
        {
            lock (_lo)
            {
                controllerState = _controllerState;
            }
        }


        public void CollectRenderData(engine.IScene scene)
        {
            _logicalRenderer.CollectRenderData(scene);
        }

        
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


        public void _onLogical(object? sender, float dt)
        {
            if (engine.GlobalSettings.Get("splash.touchControls") != "false")
            {
                _touchMouseController();
            }
            else
            {
                _desktopMouseController();
            }
        }


        public void SetupDone()
        {
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
            
            // TXWTODO: Test DEBUG and PLATFORM_ANDROID for format options.
            // disable and bind cursor.

            /*
             * Internal video implementation.
             */
            _silkThreeD = new SilkThreeD(_engine);

            /*
             * Internal helpers managing various entities.
             */
            _instanceManager = new(_silkThreeD);
            _instanceManager.Manage(_engine.GetEcsWorld());
            _lightManager = new(_engine, _silkThreeD);
            
            /*
             * Create the main screen renderer.
             */
            _logicalRenderer = new LogicalRenderer(
                _engine,
                _silkThreeD,
                _lightManager
            );

            _renderer = new SilkRenderer(
                _engine,
                _lightManager,
                _silkThreeD
            );

            _engine.LogicalFrame += _onLogical;
        }

        public void Sleep(double dt)
        {
            System.Threading.Thread.Sleep((int)(dt*1000f));
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
            _engine.LogicalFrame -= _onLogical;
        }

        public Platform(string[] args)
        {
            _controllerState = new();
            _vMouseMove = new Vector2(0f, 0f);

            // _controllerState.ZoomState = (sbyte) float.Parse(engine.GlobalSettings.Get("platform.initialZoomState"), CultureInfo.InvariantCulture);
        }

        static public engine.Engine EasyCreatePlatform(string[] args, out Splash.Silk.Platform platform)
        {
            platform = new Platform(args);
            engine.Engine engine = new engine.Engine(platform);
            engine.SetupDone();

            platform.SetEngine(engine);
            platform.SetupDone();
            engine.PlatformSetupDone();

            return engine;
        }


        static public engine.Engine EasyCreate(string[] args, IView iView)
        {
            var platform = new Platform(args);
            engine.Engine engine = new engine.Engine(platform);
            engine.SetupDone();

            platform.SetIView(iView);
            platform.SetEngine(engine);
            platform.SetupDone();
            engine.PlatformSetupDone();

            return engine;
        }
    }
}
