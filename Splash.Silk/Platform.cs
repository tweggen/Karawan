using System;
using System.Diagnostics;
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

        private IView _iView;
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
            IInputContext input = _iView.CreateInput();
            for (int i = 0; i < input.Keyboards.Count; i++)
            {
                input.Keyboards[i].KeyDown += _onKeyDown;
                input.Keyboards[i].KeyUp += _onKeyUp;
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

                RenderFrame renderFrame = _logicalRenderer.DequeueRenderFrame();
                if (renderFrame == null)
                {
                    Thread.Sleep(1);
                    continue;
                }

                _renderer.SetDimension(_iView.Size.X, _iView.Size.Y);
                _renderer.RenderFrame(renderFrame);
                _iView.SwapBuffers();
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
            _gl.Viewport(size);
        }

        public void Execute()
        {
            _iView.Run();
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
            Thread.Sleep((int)(dt*1000f));
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
