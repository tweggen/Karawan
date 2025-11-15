using System;
using System.Numerics;
using engine.gongzuo;
using ImGuiNET;
namespace joyce.ui;

public class EntityInspector : APart
{
    private EntityState _sharedEntityState;
    
    public override void Render(float dt)
    {
        if (-1 == _sharedEntityState.CurrentEntityId)
        {
            ImGui.Text("(nothing selected)");
        }
        else
        {
            DefaultEcs.Entity entity = _engine.GetEcsWorldDangerous().FindEntity(_sharedEntityState.CurrentEntityId);
            if (!entity.IsAlive)
            {
                ImGui.Text("(entity has ceased)");
            }
            else
            {
                engine.EntityComponentTypeReader reader = new(entity);
                _engine.GetEcsWorldDangerous().ReadAllComponentTypes(reader);

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


                        string displayType = Property.DisplayType(componentInfo.Type);
                        bool treeNodeResult = ImGui.TreeNodeEx("field", 0, displayType);

                        ImGui.TableSetColumnIndex(1);
                        // ImGui.SetNextItemWidth(Single.MinValue);
                        ImGui.Text(componentInfo.ValueAsString);
                        ImGui.NextColumn();

                        if (treeNodeResult)
                        {
                            System.Reflection.FieldInfo[] fields = componentInfo.Type.GetFields();

                            foreach (var fieldInfo in fields)
                            {
                                string strValue = Property.FieldAsString(componentInfo, fieldInfo);
                                
                                ImGuiTreeNodeFlags treeNodeFlags =
                                    ImGuiTreeNodeFlags.Leaf
                                    | ImGuiTreeNodeFlags.NoTreePushOnOpen;

                                ImGui.TableNextRow();
                                ImGui.TableSetColumnIndex(0);
                                ImGui.AlignTextToFramePadding();

                                ImGui.TableSetColumnIndex(1);
                                ImGui.PushID(fieldInfo.Name);
                                ImGui.TreeNodeEx("value", treeNodeFlags, fieldInfo.Name);
                                ImGui.PopID();
                                ImGui.SameLine();
                                ImGui.Text(strValue);
                                
                                // ImGui.SetNextItemWidth(Single.MinValue);
                                ImGui.NextColumn();

                            }

                            ImGui.TreePop();
                        }

                        ImGui.PopID();

                        ++componentIndex;
                    }

                    ImGui.EndTable();
                }
            }

            ImGui.PopStyleVar();
        }
    }

    
    public EntityInspector(Main uiMain, EntityState sharedEntityState) : base(uiMain)
    {
        _sharedEntityState = sharedEntityState;
    }
}

