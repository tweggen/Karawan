#if false
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace Splash.Silk;

public class SkDriveControl : IDisposable
{
    private ImGuiController _imguiController;

    private GL _gl;
    private IView _iView;
    private IInputContext _iInputContext;

    private bool _isWindowOpen = true;
    
    public void OnRender(double dt)
    {
        _imguiController.Update((float)dt);
        ImGuiNET.ImGui.Begin("TestWindow", ref _isWindowOpen);
        if (ImGuiNET.ImGui.Button("Left"))
        {
        }
        if (ImGuiNET.ImGui.Button("Right"))
        {
        }
        if (ImGuiNET.ImGui.Button("Fwrd"))
        {
        }
        if (ImGuiNET.ImGui.Button("Back"))
        {
        }
        ImGuiNET.ImGui.End();
        _imguiController.Render();
    }

    public void Dispose()
    {
        _imguiController.Dispose();
        _imguiController = null;
        ImGuiNET.ImGui.DestroyContext();
    }

    public SkDriveControl(GL gl, IView iView, IInputContext iInputContext)
    {
        _gl = gl;
        _iView = iView;
        _iInputContext = iInputContext;
        _imguiController = new ImGuiController(_gl, _iView, _iInputContext);
        ImGuiNET.ImGui.CreateContext();
    }
}
#endif