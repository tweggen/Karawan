using System.Reflection;
using engine;
using ImGuiNET;
using static engine.Logger;

namespace joyce.ui;

public class Software : APart
{
    private Modules _uiModules;
    private Implementations _uiImplementations;
    
    public override void Render(float dt)
    {
        if (ImGui.TreeNode("Modules"))
        {
            _uiModules.Render(dt);
            ImGui.TreePop();
        }

        if (ImGui.TreeNode("Implementations"))
        {
            _uiImplementations.Render(dt);
            ImGui.TreePop();
        }
    }

    public Software(Main uiMain) : base(uiMain)
    {
        _uiModules = new Modules(uiMain);
        _uiImplementations = new Implementations(uiMain);
    }
}