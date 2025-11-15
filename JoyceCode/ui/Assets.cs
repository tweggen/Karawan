using engine;
using ImGuiNET;

namespace joyce.ui;

public class Assets : APart
{
    public override void Render(float dt)
    {
        var dictAssets = engine.Assets.GetAssets();
        foreach (var kvp in dictAssets)
        {
            ImGui.Text(kvp.Key);
            ImGui.SameLine();
            ImGui.Text(kvp.Value);
        }
    }
    
    
    public Assets(Main uiMain) : base(uiMain)
    {
    }
}