using System;
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

                Raylib.BeginDrawing();
                if (showMessageBox)
                {
                    Raylib.DrawRectangle(
                        0,
                        0,
                        (int)(Raylib.GetScreenWidth() * 0.8),
                        (int)(Raylib.GetScreenHeight() * 0.8),
                        Raylib.Fade(Raylib.RAYWHITE, 0.8f));
                    int result = RayGui.GuiMessageBox(
                        new Rectangle(
                            (float)Raylib.GetScreenWidth() / 2 - 125, (float)Raylib.GetScreenHeight() / 2 - 50, 250, 100),
                        RayGui.GuiIconText(5, "Close Window"), "Do you really want to exit?", "Yes;No");
                    if ((result == 0) || (result == 2)) showMessageBox = false;
                    else if (result == 1) showMessageBox = true;
                }
                Raylib.EndDrawing();

                lastFrame = thisFrame;
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
