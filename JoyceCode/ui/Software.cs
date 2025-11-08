using System.Reflection;
using engine;
using engine.physics.systems;
using ImGuiNET;
using static engine.Logger;

namespace joyce.ui;

public class Software : APart
{
    public override void Render(float dt)
    {
        if (ImGui.TreeNode("Modules"))
        {
            var modules = _engine.GetModules();
            foreach (var module in modules)
            {
                if (ImGui.TreeNode(module.GetType().ToString()))
                {
                    foreach (PropertyInfo property in module.GetType().GetProperties())
                    {
                        string propName = property.Name;
                        object propValue = property.GetValue(module);
                        if (propValue != null)
                        {
                            Main.PropEdit(propName, propValue,
                                (key, newValue) => property.SetValue(module, newValue));
                        }
                        else
                        {
                            ImGui.Text($"{propName}: null");
                        }
                    }

                    ImGui.TreePop();
                }
            }

            ImGui.TreePop();
        }

        if (ImGui.TreeNode("Implementations"))
        {
            var types = I.Instance.GetTypes();
            foreach (var type in types)
            {
                ImGui.Text(type.ToString());
            }

            ImGui.TreePop();
        }
    }

    public Software(Main uiMain) : base(uiMain
    {
    }
}