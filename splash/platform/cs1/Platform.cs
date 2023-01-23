
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
            // Main game loop
            while (!Raylib.WindowShouldClose()) // Detect window close button or ESC key
            {
                _engine.OnPhysicalFrame( 1 / 60 );
                _aSplash.Render();
            }
            Raylib.CloseWindow();
        }

        public void SetupDone()
        {
            var display = Raylib.GetCurrentMonitor();
            Raylib.InitWindow(Raylib.GetMonitorWidth(display), Raylib.GetMonitorHeight(display), "codename Karawan");
            Raylib.ToggleFullscreen();
            Raylib.SetTargetFPS(60);

            _aSplash = new splash.API(_engine);
        }

        public Platform(string[] args)
        {
        }
    }
}
