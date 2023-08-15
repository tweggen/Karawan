using System;
using System.Data;
using System.Numerics;
using engine;
using ImGuiNET;

namespace joyce.ui;

public class Main
{
    private object _lo = new();
    private Engine _engine;

    private int _currentEntityId = -1;
    
    public unsafe void Render(float dt)
    {
        ImGui.SetNextWindowPos(new Vector2(0, 20), ImGuiCond.Appearing);
        ImGui.SetNextWindowSize(ImGui.GetMainViewport().Size with {X = 500 } - new Vector2(0, 20), ImGuiCond.Appearing);
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
                        int id = entity.GetId();
                        ImGui.PushID(id);
                        
                        bool isSelected = _currentEntityId == id;
                        string entityString;
                        if (entity.Has<engine.joyce.components.EntityName>())
                        {
                            entityString = $"#{id} {entity.Get<engine.joyce.components.EntityName>()} ({entity.ToString()})";
                        }
                        else
                        {
                            entityString = entity.ToString();
                        }
                        if (ImGui.Selectable(entityString, isSelected))
                        {
                            _currentEntityId = entity.GetId();
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

            if (ImGui.CollapsingHeader("Inspector"))
            {
                if (-1 == _currentEntityId)
                {
                    ImGui.Text("(nothing selected)");
                }
                else
                {
                    DefaultEcs.Entity entity = _engine.GetEcsWorld().FindEntity(_currentEntityId);
                    if (!entity.IsAlive)
                    {
                        ImGui.Text("(entity has ceased)");
                    }
                    else
                    {
                        engine.EntityComponentTypeReader reader = new(entity);
                        _engine.GetEcsWorld().ReadAllComponentTypes(reader);
                        
                        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2f, 2f));
                        if (ImGui.BeginTable("split", 2, ImGuiTableFlags.BordersOuter | ImGuiTableFlags.Resizable))
                        {
                            int componentIndex = 0;
                            foreach (var (strType, componentInfo) in reader.DictComponentTypes)
                            {

                                ImGui.PushID(10 + componentIndex);
                                ImGui.TableNextRow();
                                ImGui.TableSetColumnIndex(0);
                                ImGui.AlignTextToFramePadding();
                                

                                ImGuiTreeNodeFlags treeNodeFlags =
                                    ImGuiTreeNodeFlags.Leaf
                                    | ImGuiTreeNodeFlags.NoTreePushOnOpen
                                    | ImGuiTreeNodeFlags.Bullet;
                                ImGui.TreeNodeEx("field", treeNodeFlags, componentInfo.Type.ToString());

                                ImGui.TableSetColumnIndex(1);
                                // ImGui.SetNextItemWidth(Single.MinValue);
                                ImGui.Text(componentInfo.ValueAsString);
                                ImGui.NextColumn();
                                
                                ++componentIndex;
                                ImGui.PopID();
                            }

                            ImGui.EndTable();
                        }
                    }

                    ImGui.PopStyleVar();
                }
            }

            if (ImGui.CollapsingHeader("Monitor"))
            {
                var frameTimings = _engine.FrameDurations;

                int count = frameTimings.Length;

                fixed (float* pTiming = &frameTimings[0])
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