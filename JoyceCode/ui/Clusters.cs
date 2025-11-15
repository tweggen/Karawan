using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using engine;
using engine.world;
using ImGuiNET;
using static engine.Logger;

namespace joyce.ui;

public class Clusters : APart
{
    private string _currentClusterId = "";

    public override void Render(float dt)
    {
        if (ImGui.BeginListBox("##ClusterList", new Vector2(450f, 700f)))
        {
            var clusterList = new List<ClusterDesc>(I.Get<ClusterList>().GetClusterList());
            clusterList.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            foreach (var clusterDesc in clusterList)
            {
                var id = clusterDesc.IdString;
                ImGui.PushID(id);

                bool isSelected = _currentClusterId == id;
                string clusterString = clusterDesc.Name;

                if (ImGui.Selectable(clusterString, isSelected))
                {
                    _currentClusterId = id;
                }
                        
                if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(0))
                {
                    _engine.QueueMainThreadAction(() =>
                    {
                        clusterDesc.FindStartPosition(out var v3Start, out var qStart);
                        _engine.BeamTo(v3Start + clusterDesc.Pos, qStart);
                    });
                }

                if (isSelected)
                {
                    ImGui.SetItemDefaultFocus();
                }

                ImGui.PopID();
            }
            ImGui.EndListBox();
        }
    }

    public Clusters(Main uiMain) : base(uiMain)
    {
    }
}