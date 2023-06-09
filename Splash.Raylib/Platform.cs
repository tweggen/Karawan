﻿using System;
using System.Numerics;
using engine;
using Raylib_CsLo;
using static engine.Logger;

namespace Splash.Raylib
{
    public class Platform : engine.IPlatform
    {
        private object _lock = new object();
        private engine.Engine _engine;
        private engine.ControllerState _controllerState;
        private Vector2 _vMouseMove;

        private RaylibThreeD _raylibThreeD;
        private TextureManager _textureManager;
        private MaterialManager _materialManager;
        private MeshManager _meshManager;
        private LightManager _lightManager;
        private RaylibRenderer _renderer;
        private bool _isRunning = true;

        private LogicalRenderer _logicalRenderer;
        
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
            if(Raylib_CsLo.Raylib.IsKeyDown(KeyboardKey.KEY_LEFT_SHIFT)
                || Raylib_CsLo.Raylib.IsKeyDown(KeyboardKey.KEY_RIGHT_SHIFT))
            {
                _controllerState.WalkFast = true;
            }
            if(Raylib_CsLo.Raylib.IsKeyDown(KeyboardKey.KEY_W))
            {
                _controllerState.WalkForward = 200;
            }
            if(Raylib_CsLo.Raylib.IsKeyDown(KeyboardKey.KEY_S))
            {
                _controllerState.WalkBackward = 200;
            }
            if(Raylib_CsLo.Raylib.IsKeyDown(KeyboardKey.KEY_A))
            {
                _controllerState.TurnLeft = 200;
            }
            if(Raylib_CsLo.Raylib.IsKeyDown(KeyboardKey.KEY_D))
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
        }

        
        private void _physFrameReadMouseMove()
        {
            Vector2 vnew = Raylib_CsLo.Raylib.GetMouseDelta();
            lock (_lock)
            {
                _vMouseMove += vnew;
            }
        }


        public void Execute()
        {
            double lastFrame = 0, thisFrame = 0;

            // Main game loop
            lastFrame = Raylib_CsLo.Raylib.GetTime();

            while (!Raylib_CsLo.Raylib.WindowShouldClose()) // Detect window close button or ESC key
            {
                int sw = Raylib_CsLo.Raylib.GetRenderWidth();
                int sh = Raylib_CsLo.Raylib.GetRenderHeight();
                _renderer.SetDimension(sw, sh);
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
                thisFrame = Raylib_CsLo.Raylib.GetTime();
                lastFrame = thisFrame;
            }
            _isRunning = false;
            Raylib_CsLo.Raylib.CloseWindow();
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
            Raylib_CsLo.Raylib.SetTraceLogLevel(4 /* LOG_WARNING */);
            //Raylib_CsLo.Raylib.SetTraceLogCallback(_raylibTraceLog);

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

            _raylibThreeD = new RaylibThreeD(_engine);
            
            _materialManager = new(_raylibThreeD);
            _materialManager.Manage(_engine.GetEcsWorld());
            _meshManager = new(_engine, _raylibThreeD);
            _meshManager.Manage(_engine.GetEcsWorld());
            _lightManager = new(_engine, _raylibThreeD);
            
            _logicalRenderer = new LogicalRenderer(
                _engine,
                _materialManager,
                _meshManager,
                _lightManager
            );
            _renderer = new RaylibRenderer(
                _engine,
                _lightManager,
                _raylibThreeD
            );
        }

        public void Sleep(double dt)
        {
            Raylib_CsLo.Raylib.WaitTime(dt);
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

        static public engine.Engine EasyCreatePlatform(string[] args, out Splash.Raylib.Platform platform)
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
