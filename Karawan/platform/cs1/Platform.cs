
using Raylib_CsLo;

namespace Karawan.platform.cs1
{
    class Platform 
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
            Raylib.InitWindow(1280, 720, "Hello, Raylib-CsLo");
            Raylib.SetTargetFPS(60);
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
            _aSplash = new splash.API(_engine);
        }

        public Platform(string[] args)
        {
        }
    }
}
