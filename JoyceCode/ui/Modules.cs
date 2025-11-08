using System.Reflection;
using engine;
using ImGuiNET;
using static engine.Logger;

namespace joyce.ui;

public class Modules : APart
{
    public override void Render(float dt)
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
    }

    public Modules(Main uiMain) : base(uiMain)
    {
    }

}