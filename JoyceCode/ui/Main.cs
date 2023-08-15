using System.Numerics;
using engine;
using ImGuiNET;

namespace joyce.ui;

public class Main
{
    private object _lo = new();
    private Engine _engine;
    
    public unsafe void Render(float dt)
    {
        ImGui.SetNextWindowPos(new Vector2(0, 20), ImGuiCond.Appearing);
        ImGui.SetNextWindowSize(ImGui.GetMainViewport().Size with {X = 400 } - new Vector2(0, 20), ImGuiCond.Appearing);
        if (ImGui.Begin("selector", ImGuiWindowFlags.NoCollapse))
        {
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    ImGui.MenuItem("New", false);
                    ImGui.MenuItem("Open...", false);
                    ImGui.MenuItem("Save", false);
                    ImGui.MenuItem("Save as...", false);
                    
                    ImGui.EndMenu();
                }

                ImGui.EndMainMenuBar();
            }

            if (ImGui.CollapsingHeader("Config"))
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
            }

            if (ImGui.CollapsingHeader("Entities"))
            {
                if (ImGui.BeginListBox("entities"))
                {
                    var entities = _engine.GetEcsWorld().GetEntities().AsEnumerable();
                    foreach (var entity in entities)
                    {
                        ImGui.Selectable(entity.ToString());
                    }

                    ImGui.EndListBox();
                }
            }

            if (ImGui.CollapsingHeader("Monitor"))
            {
                var frameTimings = _engine.FrameDurations;

                int count = frameTimings.Count;
                float[] arrTimings = new float[frameTimings.Count];
                int idx = 0;
                foreach (float val in frameTimings)
                {
                    arrTimings[idx++] = val;
                }

                fixed (float* pTiming = &arrTimings[0])
                {
                    ImGui.PlotLines("Time per frame", ref *pTiming, count);
                }

            }

            ImGui.EndChild();
        }

        ImGui.End();
    }

    public Main(Engine engine)
    {
        _engine = engine;
    }
}