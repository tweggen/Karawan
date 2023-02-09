using System;
using System.Numerics;
using engine;
using Raylib_CsLo;

namespace Karawan.platform.cs1
{
    public class Platform 
        : engine.IPlatform
    {
        private object _lock = new object();
        private engine.Engine _engine;
        private engine.ControllerState _controllerState;
        private Vector2 _vMouseMove;

        private splash.API _aSplash;

        public void SetEngine(engine.Engine engine)
        {
            lock (_lock)
            {
                _engine = engine;
            }
        }

        private void _physFrameUpdateControllerState()
        {
            /*
             * Read input devices.
             * #1 Key States, filling the controller data structure.
             */
            _controllerState.Reset();
            if(Raylib.IsKeyDown(KeyboardKey.KEY_LEFT_SHIFT)
                || Raylib.IsKeyDown(KeyboardKey.KEY_RIGHT_SHIFT))
            {
                _controllerState.WalkFast = true;
            }
            if(Raylib.IsKeyDown(KeyboardKey.KEY_W))
            {
                _controllerState.WalkForward = 200;
            }
            if(Raylib.IsKeyDown(KeyboardKey.KEY_S))
            {
                _controllerState.WalkBackward = 200;
            }
            if(Raylib.IsKeyDown(KeyboardKey.KEY_A))
            {
                _controllerState.TurnLeft = 200;
            }
            if(Raylib.IsKeyDown(KeyboardKey.KEY_D))
            {
                _controllerState.TurnRight = 200;
            }
        }

        private void _physFrameReadKeyEvents()
        {
            /*
             * Read input devices.
             * #2 Key events
             */
            while (true)
            {
                var keyCode = Raylib.GetKeyPressed();
                if (0 == keyCode)
                {
                    break;
                }
                if (keyCode == 65)
                {
                    // TXWTODO: Forward 
                }
            }
        }

        
        private void _physFrameReadMouseMove()
        {
            Vector2 vnew = Raylib.GetMouseDelta();
            lock (_lock)
            {
                _vMouseMove += vnew;
            }
        }


        public void Execute()
        {
            double lastFrame = 0, thisFrame = 0;

            // Main game loop
            lastFrame = Raylib.GetTime();
            Raylib.BeginDrawing();
            _engine.OnPhysicalFrame(1/60);
            Raylib.EndDrawing();

            while (!Raylib.WindowShouldClose()) // Detect window close button or ESC key
            {
                _physFrameUpdateControllerState();
                _physFrameReadKeyEvents();
                _physFrameReadMouseMove();

                /*
                 * Call the render operations.
                 */
                thisFrame = Raylib.GetTime();
                Raylib.BeginDrawing();
                _engine.OnPhysicalFrame( (float)(thisFrame-lastFrame) );
                Raylib.EndDrawing();
                lastFrame = thisFrame;
            }
            Raylib.CloseWindow();
        }

        public void Render3D()
        {
            _aSplash.Render();
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

        public engine.IUI CreateUI()
        {
            return new PlatformUI();
        }

        public void SetupDone()
        {
            string baseDirectory = System.AppContext.BaseDirectory;
            System.Console.WriteLine($"Running in directory {baseDirectory}" );

            var display = Raylib.GetCurrentMonitor();
#if DEBUG
            var width = 1280;
            var height = width / 16 * 9;
            bool isFullscreen = false;
#else
            var width = Raylib.GetMonitorWidth(display);
            var height = Raylib.GetMonitorHeight(display);
            bool isFullscreen = false;
#endif
            Raylib.InitWindow(width, height, "codename Karawan");
            if (isFullscreen)
            {
                Raylib.ToggleFullscreen();
            }
            Raylib.DisableCursor();
            Raylib.SetTargetFPS(60);

            _aSplash = new splash.API(_engine);
        }

        public Platform(string[] args)
        {
            _controllerState = new();
            _vMouseMove = new Vector2(0f, 0f);
        }

        static public engine.Engine EasyCreatePlatform(string[] args, out Karawan.platform.cs1.Platform platform)
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
