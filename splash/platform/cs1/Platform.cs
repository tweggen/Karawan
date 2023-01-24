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
            _engine.OnPhysicalFrame(1/60);

            bool showMessageBox = false;

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
                        showMessageBox = !showMessageBox;
                    }
                }

                thisFrame = Raylib.GetTime();
                _engine.OnPhysicalFrame( (float)(thisFrame-lastFrame) );

                _aSplash.Render();
                lastFrame = thisFrame;
            }
            Raylib.CloseWindow();
        }

        public void SetupDone()
        {
            var display = Raylib.GetCurrentMonitor();
            Raylib.InitWindow(Raylib.GetMonitorWidth(display), Raylib.GetMonitorHeight(display), "codename Karawan");
            //Raylib.ToggleFullscreen();
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
