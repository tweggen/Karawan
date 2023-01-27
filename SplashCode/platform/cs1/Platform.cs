using System;
using Raylib_CsLo;

namespace Karawan.platform.cs1
{
    public class Platform 
        : engine.IPlatform
    {
        private engine.Engine _engine;
        private splash.API _aSplash;

        public void SetEngine(engine.Engine engine)
        {
            _engine = engine;
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
                while(true) {
                    var keyCode = Raylib.GetKeyPressed();
                    if( 0==keyCode )
                    {
                        break;
                    }
                    if( keyCode == 65 )
                    {
                        // TXWTODO: Forward 
                    }
                }

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

        public engine.IUI CreateUI()
        {
            return new PlatformUI();
        }

        public void SetupDone()
        {
            var display = Raylib.GetCurrentMonitor();
#if DEBUG
            var width = 1280;
            var height = width / 16 * 9;
            bool isFullscreen = false;
#else
            var width = Raylib.GetMonitorWidth(display);
            var height = Raylib.GetMonitorHeight(display);
            bool isFullscreen = true;
#endif
            Raylib.InitWindow(width, height, "codename Karawan");
            if (isFullscreen)
            {
                Raylib.ToggleFullscreen();
            }
            Raylib.SetTargetFPS(60);

            _aSplash = new splash.API(_engine);
        }

        public Platform(string[] args)
        {
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
