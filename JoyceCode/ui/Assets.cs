using engine;
using ImGuiNET;

namespace joyce.ui;

public class Assets : APart
{
    public override void Render(float dt)
    {
        if (ImGui.TreeNode("Assets"))
        {
            var dictAssets = engine.Assets.GetAssets();
            foreach (var kvp in dictAssets)
            {
                ImGui.Text(kvp.Key);
                ImGui.SameLine();
                ImGui.Text(kvp.Value);
            }
            ImGui.TreePop();
        }
    }
    
    
    public Assets(Main uiMain) : base(uiMain)
    {
    }
}