using Raylib_CsLo;

namespace Karawan.platform.cs1
{
    internal class PlatformUI : engine.IUI
    {
        public void Render()
        {
            var sw = Raylib.GetScreenWidth();
            var sh = Raylib.GetScreenHeight();
            Raylib.DrawRectangle(sw/16, sh/9,
                14*sw/16, 7*sh/9, Raylib.Fade(Raylib.RAYWHITE, 0.5f));
            int result = RayGui.GuiMessageBox(
                new Rectangle((float)(Raylib.GetScreenWidth() / 2 - 125), (float)(Raylib.GetScreenHeight() / 2 - 50),
                250, 100),
                RayGui.GuiIconText(0, "Close Window"), "Do you really want to exit?", "Yes;No");
        }


        public engine.IUINode CreateUI(string strUISpec)
        {

        }

        public PlatformUI()
        {

        }
    }
}
