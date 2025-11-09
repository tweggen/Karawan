using engine;
using ImGuiNET;
using static engine.Logger;

namespace joyce.ui;

public class Config : APart
{
    public override void Render(float dt)
    {
        if (ImGui.TreeNode("Global"))
        {
            var dict = engine.GlobalSettings.Instance().Dictionary;
            foreach (var kvp in dict)
            {
                ImGui.Text(kvp.Key);
                ImGui.SameLine();
                ImGui.Text(kvp.Value);
            }

            ImGui.TreePop();
        }

        if (ImGui.TreeNode("Props"))
        {
            var dict = engine.Props.Instance().Dictionary;
            foreach (var kvp in dict)
            {
                Property.Edit(kvp.Key,kvp.Value, (key, newValue) => Props.Set(key, newValue) );
            }

            ImGui.TreePop();
        }
    }

    public Config(Main uiMain) : base(uiMain)
    {
    }
}