using System;
using System.Numerics;
using engine;
using static engine.Logger;

using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Splash.Silk
{
    public class Platform : engine.IPlatform
    {
        private object _lock = new object();
        private engine.Engine _engine;
        private engine.ControllerState _controllerState;
        private Vector2 _vMouseMove;

        private SilkThreeD _silkThreeD;
        private TextureManager _textureManager;
        private MaterialManager _materialManager;
        private MeshManager _meshManager;
        private LightManager _lightManager;
        private SilkRenderer _renderer;
        private bool _isRunning = true;

        private LogicalRenderer _logicalRenderer;

        private IWindow _iWindow;
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



        private void _windowOnLoad()
        {
            IInputContext input = _iWindow.CreateInput();
            for (int i = 0; i < input.Keyboards.Count; i++)
            {
                input.Keyboards[i].KeyDown += _onKeyDown;
                input.Keyboards[i].KeyUp += _onKeyUp;
            }

            _gl = GL.GetApi(_iWindow);
            _silkThreeD.SetGL(_gl);
            _gl.ClearColor(1, 1, 1, 1);
            _gl.ClearDepth(1f);
        }


        private void _windowOnUpdate(double dt)
        {
            
        }
        

        private void _windowOnRender(double dt)
        {
            _physFrameReadKeyEvents();
            _physFrameReadMouseMove();

            RenderFrame renderFrame = _logicalRenderer.DequeueRenderFrame();
            if (renderFrame != null)
            {
                _renderer.SetDimension(_iWindow.Size.X, _iWindow.Size.Y);
                _renderer.RenderFrame(renderFrame);
            } else
            {
                Warning("No new frame found.");
                System.Threading.Thread.Sleep(15);
            }
            
        }

        private void _windowOnClose()
        {
            _isRunning = false;
        }

        public void Execute()
        {
            _iWindow.Run();
#if false
            while (!Raylib_CsLo.Raylib.WindowShouldClose()) // Detect window close button or ESC key
            {
                _physFrameUpdateControllerState();
                _physFrameReadKeyEvents();
                _physFrameReadMouseMove();

                RenderFrame renderFrame = _logicalRenderer.DequeueRenderFrame();
                if (renderFrame != null)
                {
                    _renderer.RenderFrame(renderFrame);
                } else
                {
                    Warning("No new frame found.");
                    System.Threading.Thread.Sleep(15);
                }
            }
            _isRunning = false;
            Raylib_CsLo.Raylib.CloseWindow();
#endif
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
            // Raylib_CsLo.Raylib.SetTraceLogLevel(4 /* LOG_WARNING */);
            //Raylib_CsLo.Raylib.SetTraceLogCallback(_raylibTraceLog);
            
            var options = WindowOptions.Default;
            options.Size = new Vector2D<int>(1280, 720);
            options.Title = "codename Karawan";
            _iWindow = Window.Create(options);
            
            _iWindow.Load += _windowOnLoad;
            _iWindow.Render += _windowOnRender;
            _iWindow.Update += _windowOnUpdate;
            _iWindow.Closing += _windowOnClose;
            
#if false
            var display = Raylib_CsLo.Raylib.GetCurrentMonitor();
#if DEBUG
            var width = 1280;
            var height = width / 16 * 9;
            bool isFullscreen = false;
#else
            var width = Raylib_CsLo.Raylib.GetMonitorWidth(display);
            var height = Raylib_CsLo.Raylib.GetMonitorHeight(display);
            bool isFullscreen = false;
#endif
#if PLATFORM_ANDROID
            Raylib_CsLo.Raylib.InitWindow(0, 0, "codename Karawan"); //Make app window 1:1 to screen size https://github.com/raysan5/raylib/issues/1731
#else
            Trace("Calling InitWindow.");
            Raylib_CsLo.Raylib.InitWindow(width, height, "codename Karawan");
#endif
            if (isFullscreen)
            {
                Raylib_CsLo.Raylib.ToggleFullscreen();
            }
            Raylib_CsLo.Raylib.DisableCursor();
            Raylib_CsLo.Raylib.SetTargetFPS(60);
#endif

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
            Thread.Sleep((int)(dt*1000f));
            //Raylib_CsLo.Raylib.WaitTime(dt);
        }

        public bool IsRunning()
        {
            lock(_lock)
            {
                return _isRunning;
            }
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


        static public engine.Engine EasyCreate(string[] args)
        {
            var platform = new Platform(args);
            engine.Engine engine = new engine.Engine(platform);
            engine.SetupDone();

            platform.SetEngine(engine);
            platform.SetupDone();
            engine.PlatformSetupDone();

            return engine;
        }
    }
}
