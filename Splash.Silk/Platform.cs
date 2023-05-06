using System;
using System.Diagnostics;
using System.Numerics;
using engine;
using static engine.Logger;

using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Input.Sdl;
using Silk.NET.SDL;

namespace Splash.Silk
{

    public class Platform : engine.IPlatform
    {
        private object _lock = new object();
        private engine.Engine _engine;
        private engine.ControllerState _controllerState;
        private Vector2 _vMouseMove;
        private static Vector2 _lastMousePosition;

        private SilkThreeD _silkThreeD;
        private TextureManager _textureManager;
        private MaterialManager _materialManager;
        private MeshManager _meshManager;
        private LightManager _lightManager;
        private SilkRenderer _renderer;
        private bool _isRunning = true;

        private LogicalRenderer _logicalRenderer;

        private IView _iView;
        private IInputContext _iInputContext;
        private GL _gl;
        
        public void SetEngine(engine.Engine engine)
        {
            lock (_lock)
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

        private void _onKeyDown(IKeyboard arg1, Key arg2, int arg3)
        {
            switch (arg2)
            {
                case Key.W:
                    _controllerState.WalkForward = 200;
                    break;
                case Key.S:
                    _controllerState.WalkBackward = 200;
                    break;
                case Key.A:
                    _controllerState.TurnLeft = 200;
                    break;
                case Key.D:
                    _controllerState.TurnRight = 200;
                    break;
                default:
                    break;
            }
        }

        private void _onKeyUp(IKeyboard arg1, Key arg2, int arg3)
        {
            switch (arg2)
            {
                case Key.W:
                    _controllerState.WalkForward = 0;
                    break;
                case Key.S:
                    _controllerState.WalkBackward = 0;
                    break;
                case Key.A:
                    _controllerState.TurnLeft = 0;
                    break;
                case Key.D:
                    _controllerState.TurnRight = 0;
                    break;
                default:
                    break;
            }            
        }


        private void _onMouseMoveDesktop(IMouse mouse, Vector2 position)
        {
        }


        private void _touchMouseController()
        {
            if (_isMouseButtonClicked)
            {
                Vector2 currDist = _currentMousePosition - _mousePressPosition;
                var viewSize = _iView.Size;

                float relY = (float)currDist.Y / (float)viewSize.Y;
                float relX = (float)currDist.X / (float)viewSize.Y;

                if (relY < 0)
                {
                    _controllerState.WalkForward = (int)(Single.Min(0.5f, -relY) * 510f);
                    _controllerState.WalkBackward = 0;
                }
                else if (relY > 0)
                {
                    _controllerState.WalkBackward = (int)(Single.Min(0.5f, relY) * 510f);
                    _controllerState.WalkForward = 0;
                }
                if (relX < 0)
                {
                    _controllerState.TurnLeft = (int)(Single.Min(0.5f, -relX) * 510f);
                    _controllerState.TurnRight = 0;
                }
                else if (relX > 0)
                {
                    _controllerState.TurnRight = (int)(Single.Min(0.5f, relX) * 510f);
                    _controllerState.TurnLeft = 0;
                }
            } else
            {
                _controllerState.WalkForward = 0;
                _controllerState.WalkBackward = 0;
                _controllerState.TurnRight = 0;
                _controllerState.TurnLeft = 0;
            }
        }


        private void _desktopMouseController()
        {
            var lookSensitivity = 1f;
            if (!_isMouseButtonClicked)
            {
                if (_lastMousePosition == default) { _lastMousePosition = _currentMousePosition; }
                else
                {
                    var xOffset = (_currentMousePosition.X - _lastMousePosition.X) * lookSensitivity;
                    var yOffset = (_currentMousePosition.Y - _lastMousePosition.Y) * lookSensitivity;
                    _lastMousePosition = _currentMousePosition;
                    _vMouseMove += new Vector2(xOffset, yOffset);
                }
            }
        }


        private bool _isMouseButtonClicked = false;
        private Vector2 _mousePressPosition = new();
        private Vector2 _currentMousePosition = new();


        private void _onMouseMove(IMouse mouse, Vector2 position)
        {
            _currentMousePosition = mouse.Position;

        }


        private void _onMouseWheel(IMouse mouse, ScrollWheel scrollWheel)
        {
        }


        private void _onMouseDown(IMouse mouse, MouseButton mouseButton)
        {
            if (mouseButton != MouseButton.Left)
            {
                return;
            }
            _mousePressPosition = mouse.Position;
            _currentMousePosition = mouse.Position;
            _isMouseButtonClicked = true;
        }

        private void _onMouseUp(IMouse mouse, MouseButton mouseButton)
        {
            if (mouseButton != MouseButton.Left)
            {
                return;
            }
            _currentMousePosition = mouse.Position;
            _isMouseButtonClicked = false;
        }

        /**
         * Because Silk does not yet support touch events right now,
         * we peek touch events from the event queue before they are
         * discarded by the current input context implementation.
         */
        private void _peekSdlEvents()
        {
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

            for (int i = 0; i < _iInputContext.Mice.Count; i++)
            {
                _iInputContext.Mice[i].Cursor.CursorMode = CursorMode.Raw;
                _iInputContext.Mice[i].MouseDown += _onMouseDown;
                _iInputContext.Mice[i].MouseUp += _onMouseUp;
                _iInputContext.Mice[i].MouseMove += _onMouseMove;

                _iInputContext.Mice[i].Scroll += _onMouseWheel;
            }

            _gl = GL.GetApi(_iView);
            _silkThreeD.SetGL(_gl);
            _gl.ClearDepth(1f);
            _gl.ClearColor(0f, 0f, 0f, 0f);
            

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

                if (engine.GlobalSettings.Get("splash.touchControls") != "false")
                {
                    _touchMouseController();
                }
                else
                {
                    _desktopMouseController();
                }


                RenderFrame renderFrame = _logicalRenderer.DequeueRenderFrame();
                if (renderFrame == null)
                {
                    _silkThreeD.ExecuteGraphicsThreadActions(0.005f);
                    System.Threading.Thread.Sleep(1);
                    continue;
                }

                if (_iView.Size.X != 0 && _iView.Size.Y != 0)
                {
                    _renderer.SetDimension(_iView.Size.X, _iView.Size.Y);
                }

                _renderer.RenderFrame(renderFrame);
               
                _iView.SwapBuffers();
                _silkThreeD.ExecuteGraphicsThreadActions(0.001f);
                ++_frameNo;
                if (2 == _frameNo)
                {
                    _engine.StartTimeline();
                }
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
            lock (_lock)
            {
                vMouseMove = _vMouseMove;
                _vMouseMove = new Vector2(0f, 0f);
            }
        }
        

        public void GetControllerState(out ControllerState controllerState)
        {
            lock (_lock)
            {
                controllerState = _controllerState;
            }
        }


        public void CollectRenderData()
        {
            _logicalRenderer.CollectRenderData();
        }

        
        public void SetupDone()
        {
            string baseDirectory = System.AppContext.BaseDirectory;
            System.Console.WriteLine($"Running in directory {baseDirectory}" );
            
            _iView.Load += _windowOnLoad;
            _iView.Resize += _windowOnResize;
            _iView.Render += _windowOnRender;
            _iView.Update += _windowOnUpdate;
            _iView.Closing += _windowOnClose;
            
            // TXWTODO: Test DEBUG and PLATFORM_ANDROID for format options.
            // disable and bind cursor.

            _silkThreeD = new SilkThreeD(_engine);
            
            _materialManager = new(_silkThreeD);
            _materialManager.Manage(_engine.GetEcsWorld());
            _meshManager = new(_engine, _silkThreeD);
            _meshManager.Manage(_engine.GetEcsWorld());
            _lightManager = new(_engine, _silkThreeD);
            
            _logicalRenderer = new LogicalRenderer(
                _engine,
                _materialManager,
                _meshManager,
                _lightManager
            );

            _renderer = new SilkRenderer(
                _engine,
                _lightManager,
                _silkThreeD
            );

        }

        public void Sleep(double dt)
        {
            System.Threading.Thread.Sleep((int)(dt*1000f));
        }

        public bool IsRunning()
        {
            lock(_lock)
            {
                return _isRunning;
            }
        }

        public void SetIView(IView iView)
        {
            _iView = iView;
        }

        public Platform(string[] args)
        {
            _controllerState = new();
            _vMouseMove = new Vector2(0f, 0f);

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
