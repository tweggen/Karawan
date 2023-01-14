using Raylib_CsLo;

namespace Karawan.platform.cs1
{
    class Platform 
        : engine.IPlatform
    {
        private engine.Engine _engine;

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

                Raylib.BeginDrawing();
                Raylib.ClearBackground(Raylib.SKYBLUE);
                Raylib.DrawFPS(10, 10);
                Raylib.DrawText("Raylib is easy!!!", 640, 360, 50, Raylib.RED);
                Raylib.EndDrawing();
            }
            Raylib.CloseWindow();
        }

        public void SetupDone()
        {
        }

        public Platform(string[] args)
        {
        }
    }
}
