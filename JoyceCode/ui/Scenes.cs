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
        var sceneKeys = I.Get<SceneSequencer>().GetAvailableScenes();
        foreach (var sceneKey in sceneKeys)
        {
            ImGui.Text(sceneKey);
        }
    }

    public Scenes(Main uiMain) : base(uiMain)
    {
    }
}