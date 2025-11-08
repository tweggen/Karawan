using System.Reflection;
using engine;
using ImGuiNET;
using static engine.Logger;

namespace joyce.ui;

public class Implementations : APart
{
    public override void Render(float dt)
    {
        var types = I.Instance.GetTypes();
        foreach (var type in types)
        {
            ImGui.Text(type.ToString());
        }
    }

    public Implementations(Main uiMain) : base(uiMain)
    {
    }
}