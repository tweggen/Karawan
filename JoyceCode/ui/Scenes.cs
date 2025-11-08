using System.Collections.Generic;
using System.Reflection;
using engine;
using engine.world;
using ImGuiNET;
using static engine.Logger;

namespace joyce.ui;

public class Scenes : APart
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
                Main.PropEdit(kvp.Key,kvp.Value, (key, newValue) => Props.Set(key, newValue) );
            }

            ImGui.TreePop();
        }
    }

    public Scenes(Main uiMain) : base(uiMain)
    {
    }
}